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

            //read running livebackup jobs
            List<LBTransfer> lbTransferList = new List<LBTransfer>();

            if (ConfigHandler.JobConfigHandler.Jobs != null)
            {
                foreach (ConfigHandler.OneJob job in ConfigHandler.JobConfigHandler.Jobs)
                {
                    if (job.LiveBackupActive && job.LiveBackupWorker != null)
                    {
                        LBTransfer lbTransfer = new LBTransfer();
                        lbTransfer.jobId = job.DbId;
                        lbTransfer.worker = job.LiveBackupWorker;
                        lbTransferList.Add(lbTransfer);
                    }
                }
            }


            //read all jobs
            ConfigHandler.JobConfigHandler.readJobsFromDB();
            List<ConfigHandler.OneJob> jobs = ConfigHandler.JobConfigHandler.Jobs;

            if (jobs == null) //DB error occured
            {
                return false;
            }

            //create one timer for each job, add credentials to cache and add possible running liveback jobs to struct
            foreach (ConfigHandler.OneJob job in jobs)
            {

                //migrate running livebackup jobs to new struct
                foreach (LBTransfer transfer in lbTransferList)
                {
                    if (transfer.jobId == job.DbId)
                    {

                        job.LiveBackupActive = true;
                        job.LiveBackupWorker = transfer.worker;
                    }
                }

                JobTimer timer = new JobTimer(job);

                //add target credential if necessary
                if (job.TargetType == "smb")
                {
                    Common.CredentialCacheManager.add(job.TargetPath, job.TargetUsername, job.TargetPassword);
                }


                //add job timer
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

        private struct LBTransfer
        {
            public int jobId;
            public nxmBackup.HVBackupCore.LiveBackupWorker worker;
        }
    }
}