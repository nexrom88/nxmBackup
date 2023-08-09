using Common;
using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Web;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class LoginController : ApiController
    {
        // POST api/<controller>
        public HttpResponseMessage Post([FromBody] string value)
        {
            HttpResponseMessage response;

            //check value
            if (value == null || value == "undefined")
            {
                response = new HttpResponseMessage();
                response.StatusCode = HttpStatusCode.Forbidden;
                return response;
            }

            //auth data is user:pass::otp base64 decoded

            string[] splitter = value.Split(":".ToCharArray());
            string username = splitter[0];
            username = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(username));
            string password = splitter[1];
            password = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(password));
            string otp = "";

            //otp available?
            if (splitter.Length == 3)
            {
                otp = splitter[2];
                otp = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(otp));
            }

            //check credentials local
            bool authorized = checkCredentials(username, password, ContextType.Machine);

            //check credentials against ad
            if (!authorized)
            {
                authorized = checkCredentials(username, password, ContextType.Domain);
            }


            //check user data
            if (authorized)
            {
                //check otp if necessary
                if (MFAHandler.isActivated())
                {
                    if (!checkOTP(otp))
                    {
                        //given otp is wrong
                        response = new HttpResponseMessage();
                        response.StatusCode = HttpStatusCode.Forbidden;
                        return response;
                    }
                }

                //create guid session string
                string guid = Guid.NewGuid().ToString();

                //add session id to authenticated session list
                App_Start.Authentication.addSession(guid);

                //build response
                response = new HttpResponseMessage();
                var cookie = new CookieHeaderValue("session_id", guid);
                cookie.Path = "/";
                cookie.Domain = Request.RequestUri.Host;
                response.Headers.AddCookies(new CookieHeaderValue[] { cookie });
                return response;
            }
            else
            {
                //login failed
                response = new HttpResponseMessage();
                response.StatusCode = HttpStatusCode.Forbidden;
                return response;
            }
        }

        //checks whether the given otp is valid
        private bool checkOTP(string otp)
        {
            return MFAHandler.verifyOTP(otp);
        }

        //checks the given credentials
        private bool checkCredentials(string username, string password, System.DirectoryServices.AccountManagement.ContextType type)
        {
            try
            {
                //try validating credentials
                bool loginSucceeded;
                using (PrincipalContext context = new PrincipalContext(type))
                {
                    loginSucceeded = context.ValidateCredentials(username, password);
                }

                if (!loginSucceeded)
                {
                    return false;
                }

                //check for admin rights
                using (var pc = new PrincipalContext(type))
                {
                    using (var up = UserPrincipal.FindByIdentity(pc, username))
                    {
                        SecurityIdentifier adminSID = new SecurityIdentifier("S-1-5-32-544"); //sid for admin group
                        return up.GetAuthorizationGroups().Any(group => group.Sid.CompareTo(adminSID) == 0);
                    }
                }

            }catch(Exception ex)
            {
                return false;
            }

        }

    }
}