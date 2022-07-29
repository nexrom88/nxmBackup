using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class JobDeleteController : ApiController
    {
        // POST api/<controller>
        public void Post([FromBody] string value)
        {
            int jobID = int.Parse(value);

            //stop lb if running
            foreach (nxmBackup.HVBackupCore.LiveBackupWorker worker in nxmBackup.HVBackupCore.LiveBackupWorker.ActiveWorkers)
            {
                if (worker.JobID == jobID)
                {
                    worker.stopLB();
                    break;
                }
            }

            ConfigHandler.JobConfigHandler.deleteJob(jobID);

            //reload jobs
            App_Start.GUIJobHandler.initJobs();
        }
    }
}