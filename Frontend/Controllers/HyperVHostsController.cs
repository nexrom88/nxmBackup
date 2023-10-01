using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Results;

namespace Frontend.Controllers
{
    public class HyperVHostsController : ApiController
    {
        [Frontend.Filter.AuthFilter]

        // gets all configured hyperv hosts
        public HttpResponseMessage Get()
        {
            HttpResponseMessage response = new HttpResponseMessage();

            //read configured hyperv hosts
            Common.HyperVHost[] hosts = Common.DBQueries.readHyperVHosts(false);

            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(hosts));
            return response;
        }

        
    }
}