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
        public HttpResponseMessage Post([FromBody] string value)
        {
            HttpResponseMessage response = new HttpResponseMessage();
            int jobID = int.Parse(value);

            //check if job is running
            if (App_Start.GUIJobHandler.jobHandler.isJobRunning(jobID))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                return response;
            }

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

            response.StatusCode = HttpStatusCode.OK;
            return response;
        }
    }
}