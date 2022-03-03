using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class StopLBController : ApiController
    {
        // POST api/<controller>
        public void Post([FromBody] string value)
        {
            int jobID = int.Parse(value);

            if (ConfigHandler.JobConfigHandler.Jobs == null)
            {
                return;
            }

            //iterate through all jobs
            foreach (ConfigHandler.OneJob job in ConfigHandler.JobConfigHandler.Jobs)
            {
                if (job.DbId == jobID && job.LiveBackupWorker != null)
                {
                    job.LiveBackupWorker.stopLB();
                    job.LiveBackupActive = false;
                }
            }
        }
    }
}