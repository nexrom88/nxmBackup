using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class ChecknxmStorageCredentialsController : ApiController
    {
       
        public HttpResponseMessage Post([FromBody] NxmStorageCredentials credentials)
        {
            //translate credentials
            Common.NxmStorageData data = Common.WebClientWrapper.translateNxmStorageData(credentials.Username, credentials.Password);
            
            HttpResponseMessage response = new HttpResponseMessage();

            //when translation result is null -> invalid credentials
            if (data == null)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
            }
            else
            {
                response.StatusCode = HttpStatusCode.OK;
            }
            return response;

        }

        
    }

    public class NxmStorageCredentials
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}