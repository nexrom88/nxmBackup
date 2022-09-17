using Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;

namespace JobEngine
{
    public class JobHandler
    {
        private static List<JobTimer> jobTimers = new List<JobTimer>();

        //starts the job engine
        public bool startJobEngine()
        {
            //stop all already running timers
            stopAllTimers();
            jobTimers = new List<JobTimer>();

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

                //check if lb is running for the given job
                foreach (nxmBackup.HVBackupCore.LiveBackupWorker worker in nxmBackup.HVBackupCore.LiveBackupWorker.ActiveWorkers)
                {
                    if (worker.JobID == job.DbId)
                    {
                        job.LiveBackupActive = true;
                        break;
                    }
                }

                JobTimer timer = new JobTimer(job);

                //add target credential if necessary
                if (job.TargetType == "smb")
                {
                    Common.CredentialCacheManager.add(job.TargetPath, job.TargetUsername, job.TargetPassword);
                }
                else if (job.TargetType == "nxmstorage")
                {
                    //get smb credentials via api call
                    using (var client = new WebClient())
                    {
                        string userData = "user=" + job.TargetUsername + "&password=" + job.TargetPassword;
                        client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                        string rawDownloadedData;
                        try
                        {
                            rawDownloadedData = client.UploadString("https://nxmBackup.com/nxmstorageproxy/getshare.php", userData);
                            NxmStorageData nxmStorageData = new NxmStorageData();
                            Newtonsoft.Json.JsonConvert.PopulateObject(rawDownloadedData, nxmStorageData);
                            job.TargetPath = nxmStorageData.share + @"\home";
                            job.TargetPassword = nxmStorageData.share_password;
                            job.TargetUsername = nxmStorageData.share_user;
                        }
                        catch (Exception ex)
                        {
                            //nxmstorage login not possible
                            DBQueries.addLog("nxmstorage login failure", Environment.StackTrace, ex);

                        }
                        rawDownloadedData = "";
                    }
                }


                //add job timer
                jobTimers.Add(timer);
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
            foreach (JobTimer timer in jobTimers)
            {
                timer.underlyingTimer.Stop();
            }
        }

        //manually starts a given job
        public void startManually(int dbId)
        {
            //search for job object
            foreach (JobTimer job in jobTimers)
            {
                if (job.Job.DbId == dbId)
                {
                    job.startJob(true);
                }
            }
        }

        private class NxmStorageData{
            public string share { get; set; }
            public string share_user { get; set; }
            public string share_password { get; set; }
        }

    }
}