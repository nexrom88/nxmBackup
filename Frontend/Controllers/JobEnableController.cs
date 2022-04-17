using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class JobEnableController : ApiController
    {

        // POST api/<controller>
        public void Post([FromBody] JobEnabled value)
        {
            //set job enabled
            ConfigHandler.JobConfigHandler.setJobEnabled(value.jobID, value.enabled);

            //reload jobs
            App_Start.GUIJobHandler.initJobs();
        }

        public class JobEnabled
        {
            public int jobID { get; set; }
            public bool enabled { get; set; }
        }

    }
}