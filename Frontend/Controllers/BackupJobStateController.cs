using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class BackupJobStateController : ApiController
    {
        // gets the job state to fill job panel
        public HttpResponseMessage Get(int jobId)
        {
            List<ConfigHandler.OneJob> jobs = new List<ConfigHandler.OneJob>();
            ConfigHandler.JobConfigHandler.readJobsFromDB(jobs, jobId);

            bool liveBackupActive = false;

            //have to set livebackupactive? Look within loaded jobs
            foreach (ConfigHandler.OneJob job in ConfigHandler.JobConfigHandler.Jobs)
            {
                if (job.LiveBackupActive && job.DbId == jobId)
                {
                    liveBackupActive = true;
                }
            }

            //check that result is valid
            if (jobs == null || jobs.Count == 0)
            {
                return null;
            }

            //remove aeskey
            jobs[0].AesKey = null;

            //remove smb password
            jobs[0].TargetPassword = "";

            //just use first job from result list
            ConfigHandler.OneJob selectedJob = jobs[0];
            selectedJob.LiveBackupActive = liveBackupActive;

            HttpResponseMessage response = new HttpResponseMessage();
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(selectedJob));

            return response;
        }

       
    }
}