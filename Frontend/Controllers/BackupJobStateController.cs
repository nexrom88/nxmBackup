using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class BackupJobStateController : ApiController
    {
        // gets the job state to fill job panel
        public HttpResponseMessage Get(int jobId)
        {
            List<ConfigHandler.OneJob> jobs = new List<ConfigHandler.OneJob>();
            ConfigHandler.JobConfigHandler.readJobsFromDB(jobs, jobId);

            //check that result is valid
            if (jobs == null || jobs.Count == 0)
            {
                return null;
            }

            //just use first job from result list
            ConfigHandler.OneJob selectedJob = jobs[0];

            HttpResponseMessage response = new HttpResponseMessage();
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(selectedJob));

            return response;
        }

       
    }
}