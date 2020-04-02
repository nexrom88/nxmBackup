using System;
using System.Collections.Generic;

namespace JobEngine
{
    public class JobHandler
    {
        private List<System.Timers.Timer> timers = new List<System.Timers.Timer>();
        private List<JobTimer> jobTimers = new List<JobTimer>();

        //starts the job engine
        public void startJobEngine()
        {
            //read all jobs
            List<ConfigHandler.OneJob> jobs = ConfigHandler.JobConfigHandler.readJobs();

            //create one timer for each job
            foreach (ConfigHandler.OneJob job in jobs)
            {
                JobTimer timer = new JobTimer(job);
                this.jobTimers.Add(timer);
                System.Timers.Timer t = new System.Timers.Timer(60000);
                t.Elapsed += timer.tick;
                t.Start();
            }

        }

        //manually starts a given job
        public void startManually(int dbId, int executionId)
        {
            //search for job object
            foreach (JobTimer job in this.jobTimers)
            {
                if (job.Job.DbId == dbId)
                {
                    job.startJob(true, executionId);
                }
            }
        }
    }
}