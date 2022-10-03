using System;
using System.Collections.Generic;
using System.Text;
using ConfigHandler;
using HyperVBackupRCT;
using Common;
using nxmBackup.HVBackupCore;
using System.Reflection;
using System.Linq;
using System.IO;

namespace JobEngine
{
    public class JobTimer
    {
        private ConfigHandler.OneJob job;
        private bool inProgress = false;
        public System.Timers.Timer underlyingTimer;

        public JobTimer(ConfigHandler.OneJob job) 
        {
            this.Job = job;

            //check if job is already running
            if (this.Job.IsRunning)
            {
                this.inProgress = true;
            }
        }

        public OneJob Job { get => job; set => job = value; }

        //gets raised frequently
        public void tick(object sender, System.Timers.ElapsedEventArgs e)
        {
            //just start job when not disabled
            if (this.job.Enabled)
            {
                startJob(false);
            }
        }

        //starts the job
        public void startJob(bool force)
        {

            //just check "job due" when it is not forced
            if (!force)
            {
                //exit if it is not already time for the job            
                if (!isDue())
                {
                    return;
                }
            }

            //check whether job is still in progress
            if (this.inProgress)
            {
                return;
            }

            //stop LB if in progress
            foreach(LiveBackupWorker worker in LiveBackupWorker.ActiveWorkers)
            {
                if (worker.JobID == this.Job.DbId)
                {
                    //lb worker found, now stop it
                    worker.stopLB();
                    break;
                }
            }

            this.inProgress = true;

            //get new execution ID
            int executionId = Common.DBQueries.addJobExecution(job.DbId, "backup");
            bool executionSuccessful = true;

            DateTime startTime = DateTime.Now;

            //iterate vms within the current job

            UInt64 totalBytesTransfered = 0;
            UInt64 totalBytesProcessed = 0;
            foreach (JobVM vm in this.Job.JobVMs)
            {
                SnapshotHandler ssHandler = new SnapshotHandler(vm, executionId, this.Job.UseEncryption, this.Job.AesKey, this.job.UsingDedupe);

                //incremental allowed?
                bool incremental = this.Job.Incremental;


                TransferDetails transferDetails = ssHandler.performFullBackupProcess(ConsistencyLevel.ApplicationAware, true, incremental, this.job);

                //update bytes counter
                totalBytesTransfered += transferDetails.bytesTransfered;
                totalBytesProcessed += transferDetails.bytesProcessed;

                if (!transferDetails.successful) executionSuccessful = false;
            }

            // set job execution state
            JobExecutionProperties executionProps = new JobExecutionProperties();
            executionProps.startStamp = startTime;
            executionProps.endStamp = DateTime.Now;
            executionProps.bytesProcessed = totalBytesProcessed;
            executionProps.bytesTransfered = totalBytesTransfered;
            executionProps.successful = executionSuccessful;
            executionProps.warnings = 0;            
            if (executionSuccessful)
            {
                executionProps.errors = 0;
            }
            else
            {
                executionProps.errors = 1;
            }
            Common.DBQueries.closeJobExecution(executionProps, executionId.ToString());

            //send notification mail
            if (this.job.MailNotifications)
            {
                sendNotificationMail(executionProps);
            }

            this.inProgress = false;
        }

        //sends the notification mail after job execution finished
        private bool sendNotificationMail(JobExecutionProperties properties)
        {
            //read html file from ressources
            string mailHTML = nxmBackup.Properties.Resources.mailNotification;

            Dictionary<string,string> settings = DBQueries.readGlobalSettings(true);

            //cancel if mailserver is not set
            if (settings["mailserver"] == "")
            {
                DBQueries.addLog("mail cannot be sent. No server given", Environment.StackTrace, null);
                return false;
            }

            Common.MailClient mailClient = new MailClient(settings["mailserver"], settings["mailuser"], settings["mailpassword"], settings["mailsender"], settings["mailssl"] == "true" ? true:false);


            //fill in placeholders within html form
            mailHTML = mailHTML.Replace("{{jobname}}", this.Job.Name);
            mailHTML = mailHTML.Replace("{{state}}", properties.successful? "Erfolgreich":"Fehler");
            mailHTML = mailHTML.Replace("{{starttime}}", properties.startStamp.ToString("dd.MM.yyyy HH:mm"));
            mailHTML = mailHTML.Replace("{{endtime}}", properties.endStamp.ToString("dd.MM.yyyy HH:mm"));
            mailHTML = mailHTML.Replace("{{transfered}}", Common.PrettyPrinter.prettyPrintBytes((long)properties.bytesTransfered));

            return mailClient.sendMail("nxmBackup - Jobbericht", mailHTML, true, settings["mailrecipient"]);
        }

        //checks whether the job has to start now or not
        private bool isDue()
        {
            DateTime now = DateTime.Now;

            switch (this.Job.Interval.intervalBase)
            {
                case IntervalBase.hourly: //hourly backup due?
                    return now.Minute == this.Job.Interval.minute;

                case IntervalBase.daily: //daily backup due?
                    return now.Minute ==this.Job.Interval.minute && now.Hour == this.Job.Interval.hour;

                case IntervalBase.weekly: //weekly backup due?
                    if(now.Minute == this.Job.Interval.minute && now.Hour == this.Job.Interval.hour)
                    {
                        return now.DayOfWeek.ToString().ToLower() == this.Job.Interval.day;
                    }
                    return false;
                case IntervalBase.never:
                    return false;
            }

            return false;
        }

    }
}
