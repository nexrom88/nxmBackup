using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class LogoutController : ApiController
    {
        // GET api/<controller>
        public void Get()
        {
            CookieHeaderValue cookie = Request.Headers.GetCookies("session_id").FirstOrDefault();
            if (cookie != null)
            {
                string session = cookie["session_id"].Value;
                App_Start.Authentication.removeSession(session);
            }
        }

       
    }
}