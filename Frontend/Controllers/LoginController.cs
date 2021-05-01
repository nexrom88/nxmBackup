using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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

            HttpResponseMessage response;
            //check user data
            if (username == "admin" && password == "pass")
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

    }
}