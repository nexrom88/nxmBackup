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

            //if restore -> check whether job is still running
            if (details.jobType == "restore")
            {
                RestoreEvents eventObj = new RestoreEvents();
                eventObj.isRunning = Common.DBQueries.isRestoreRunning(details.id);
                eventObj.events = retVal;
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(eventObj));
                return response;
            }
            else
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(retVal));
                return response;
            }


        }

        public struct BackupJobEventDetails
        {
            public int id { get; set; }
            public string jobType { get; set; }
        }


        public struct RestoreEvents
        {
            public bool isRunning;
            public List<Dictionary<string, object>> events;
        }
    }
}