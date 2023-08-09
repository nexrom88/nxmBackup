using Common;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class MFAActivatedController : ApiController
    {
        //returns whether 2fa is activated or not
        public HttpResponseMessage Get()
        {
            HttpResponseMessage response = new HttpResponseMessage();
            
            if (MFAHandler.isActivated())
            {
                response.StatusCode = HttpStatusCode.OK;
            }
            else
            {
                response.StatusCode=HttpStatusCode.NotFound;
            }
            return response;

        }
    }
}
