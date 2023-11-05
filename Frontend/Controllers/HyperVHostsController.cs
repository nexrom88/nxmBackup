using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Results;
using static Frontend.Controllers.SettingsController;

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

        // creates a new HyperVHost
        public HttpResponseMessage Post([FromBody] HyperVHostCreateObject host)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            //convert given object to struct
            Common.HyperVHost newHost = new Common.HyperVHost();
            newHost.description = host.description;
            newHost.host = host.host;
            newHost.user = host.user;
            newHost.password = host.password;

            //write to db
            if (Common.DBQueries.addHyperVHost(newHost))
            {
                response.StatusCode = HttpStatusCode.OK;
            }
            else
            {
                response.StatusCode=HttpStatusCode.BadRequest;
            }
            return response;

        }

        //deletes a given host
        public HttpResponseMessage Delete([FromBody]string id)
        {
            HttpResponseMessage response = new HttpResponseMessage();
            //do not delete 'localhost'
            if (id == "1")
            {
                response.StatusCode = HttpStatusCode.MethodNotAllowed;
                return response;
            }
            else
            {
                if (DBQueries.deleteHyperVHost(id))
                {
                    response.StatusCode = HttpStatusCode.OK;
                    return response;
                }
                else
                {
                    //remove not possible
                    response.StatusCode = HttpStatusCode.MethodNotAllowed;
                    return response;
                }
            }
        }

        public class HyperVHostCreateObject
        {
            public string description { get; set; }
            public string host { get; set; }
            public string user { get; set; }
            public string password { get; set; }
        }
    }
}