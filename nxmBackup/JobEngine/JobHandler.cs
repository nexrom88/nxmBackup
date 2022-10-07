using Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Threading;

namespace JobEngine
{
    public class JobHandler
    {
        private static List<JobTimer> jobTimers = new List<JobTimer>();
        private static List<RunningJobThread> jobThreads = new List<RunningJobThread>();
        private static object lockObj = new object();

        //adds a running job thread to list
        public static void addRunningJobThread(int jobID, Thread thread)
        {
            lock (lockObj)
            {
                RunningJobThread runningJob = new RunningJobThread();
                runningJob.Thread = thread;
                runningJob.jobID = jobID;
                jobThreads.Add(runningJob);
            }
        }

        //checks whether a given job id has a running thread
        public static bool checkRunningJobThread(int jobID)
        {
            lock (lockObj)
            {
                foreach (RunningJobThread th in jobThreads)
                {
                    if (th.jobID == jobID && th.Thread != null && th.Thread.ThreadState == ThreadState.Running)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        //removes a running job thread from list
        public static void removeRunningJobThread(int jobID)
        {
            lock (lockObj)
            {
                for (int i = 0; i < jobThreads.Count; i++)
                {
                    if (jobThreads[i].jobID == jobID)
                    {
                        jobThreads.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        //starts the job engine
        public bool startJobEngine()
        {
            //stop all already running timers
            stopAllTimers();
            
            //build a temporary list for new jobTimers
            List<JobTimer> newTimers = new List<JobTimer>();

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
                //check if job is already within list, then reuse it
                JobTimer oldTimer = null;
                foreach (JobTimer timer in jobTimers)
                {
                    if (timer.Job.DbId == job.DbId)
                    {
                        oldTimer = timer;
                        break;
                    }
                }


                //check if lb is running for the given job
                foreach (nxmBackup.HVBackupCore.LiveBackupWorker worker in nxmBackup.HVBackupCore.LiveBackupWorker.ActiveWorkers)
                {
                    if (worker.JobID == job.DbId)
                    {
                        job.LiveBackupActive = true;
                        break;
                    }
                }


                JobTimer newTimer;

                if (oldTimer == null) { //old timer not found?

                    newTimer = new JobTimer(job);
                    //add target credential if necessary
                    if (job.TargetType == "smb")
                    {
                        Common.CredentialCacheManager.add(job.TargetPath, job.TargetUsername, job.TargetPassword);
                    }
                    else if (job.TargetType == "nxmstorage")
                    {
                        NxmStorageData nxmData =  WebClientWrapper.translateNxmStorageData(job.TargetUsername, job.TargetPassword);

                        if (nxmData != null)
                        {
                            job.TargetPassword = nxmData.share_password;
                            job.TargetPath = nxmData.share + @"\" + nxmData.share_user + @"\nxmStorage";
                            job.TargetUsername = nxmData.share_user;

                            //add retrieved credentials to cache
                            Common.CredentialCacheManager.add(job.TargetPath, job.TargetUsername, job.TargetPassword);
                        }
                    }
                }
                else //timer qlready exists
                {
                    newTimer = oldTimer;
                }

                //add job timer
                newTimers.Add(newTimer);
                System.Timers.Timer t = new System.Timers.Timer(60000);
                newTimer.underlyingTimer = t;
                t.Elapsed += newTimer.tick;
                t.Start();

            }

            jobTimers = newTimers;
            return true;

        }

        //checks if a given job is currently running
        public bool isJobRunning(int jobID)
        {
            foreach (JobTimer timer in jobTimers)
            {
                if (timer.Job.DbId == jobID)
                {
                    return timer.Job.IsRunning;
                }
            }
            return false;
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

        //stops a given job
        public void stopJob(int jobID)
        {
            //search for job object
            foreach (JobTimer job in jobTimers)
            {
                if (job.Job.DbId == jobID)
                {
                    if (JobHandler.checkRunningJobThread(jobID))
                    {
                        job.stopJob();
                        return;
                    }
                    else
                    {
                        //job found but thread does not exist => force stop
                        DBQueries.forceStopExecution(jobID);
                        job.Job.IsRunning = false;
                    }
                    
                }
            }

            //no job found, set db value to "stopped"
            DBQueries.forceStopExecution(jobID);
        }

    }

    public class RunningJobThread
    {
        public Thread Thread { get; set; }
        public int jobID { get; set; }
    }
}