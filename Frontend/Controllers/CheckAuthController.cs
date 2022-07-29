using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class CheckAuthController : ApiController
    {
        // this controller always tries to return HTTP 200, it's just here to check auth status
        public HttpResponseMessage Get()
        {
           HttpResponseMessage message = new HttpResponseMessage();
           message.StatusCode = HttpStatusCode.OK;
           return message;
        }

        
    }
}