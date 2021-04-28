using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class JobDeleteController : ApiController
    {
        // POST api/<controller>
        public void Post([FromBody] string value)
        {
            ConfigHandler.JobConfigHandler.deleteJob(int.Parse(value));

            //reload jobs
            App_Start.GUIJobHandler.initJob();
        }
    }
}