using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class BackupJobEventController : ApiController
    {

        // gets the events for one job
        public HttpResponseMessage Get([FromUri] BackupJobEventDetails details)
        {
            List<Dictionary<string, object>> retVal = Common.DBQueries.getEvents(details.id, details.jobType);
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(retVal));
            return response;
        }

        public struct BackupJobEventDetails
        {
            public int id { get; set; }
            public string jobType { get; set; }
        }

    }
}