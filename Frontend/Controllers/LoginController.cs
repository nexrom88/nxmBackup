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
            //auth data is user:pass base64 decoded
            byte[] authBytes = Convert.FromBase64String(value);
            string authString = System.Text.Encoding.UTF8.GetString(authBytes);
            string[] splitter = authString.Split(":".ToCharArray());
            string username = splitter[0];
            string password = splitter[1];

            //check credentials local
            bool authorized = checkCredentials(username, password, ContextType.Machine);

            //check credentials against ad
            if (!authorized)
            {
                authorized = checkCredentials(username, password, ContextType.Domain);
            }

            HttpResponseMessage response;
            //check user data
            if (authorized)
            {
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