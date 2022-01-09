using System;
using System.Collections.Generic;

namespace JobEngine
{
    public class JobHandler
    {
        private List<JobTimer> jobTimers = new List<JobTimer>();

        //starts the job engine
        public bool startJobEngine()
        {
            //stop all already running timers
            stopAllTimers();

            //read all jobs
            ConfigHandler.JobConfigHandler.readJobsFromDB();
            List<ConfigHandler.OneJob> jobs = ConfigHandler.JobConfigHandler.Jobs;

            if (jobs == null) //DB error occured
            {
                return false;
            }

            //create one timer for each job
            foreach (ConfigHandler.OneJob job in jobs)
            {

                JobTimer timer = new JobTimer(job);
                this.jobTimers.Add(timer);
                System.Timers.Timer t = new System.Timers.Timer(60000);
                timer.underlyingTimer = t;
                t.Elapsed += timer.tick;
                t.Start();

            }
            return true;

        }

        //stops all timers
        public void stopAllTimers()
        {
            foreach (JobTimer timer in this.jobTimers)
            {
                timer.underlyingTimer.Stop();
            }
        }

        //manually starts a given job
        public void startManually(int dbId)
        {
            //search for job object
            foreach (JobTimer job in this.jobTimers)
            {
                if (job.Job.DbId == dbId)
                {
                    job.startJob(true);
                }
            }
        }
    }
}