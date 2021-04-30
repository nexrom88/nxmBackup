using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class BackupJobEventController : ApiController
    {

        // gets the events for one job
        [Frontend.Filter.AuthFilter]
        public HttpResponseMessage Get(int id)
        {
            List<Dictionary<string, object>> retVal = Common.DBQueries.getEvents(id, "backup");
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(retVal));
            return response;
        }

    }
}