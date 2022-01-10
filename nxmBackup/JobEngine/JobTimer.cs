using System;
using System.Collections.Generic;
using System.Text;
using ConfigHandler;
using HyperVBackupRCT;
using Common;
using nxmBackup.HVBackupCore;

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
            if (this.job.LiveBackupWorker != null)
            {
                this.job.LiveBackupWorker.stopLB();
                this.job.LiveBackupWorker = null;
            }

            this.inProgress = true;

            //get new execution ID
            int executionId = Common.DBQueries.addJobExecution(job.DbId, "backup");
            bool executionSuccessful = true;

            //iterate vms within the current job

            UInt64 totalBytesTransfered = 0;
            UInt64 totalBytesProcessed = 0;
            foreach (JobVM vm in this.Job.JobVMs)
            {
                SnapshotHandler ssHandler = new SnapshotHandler(vm, executionId, this.Job.UseEncryption, this.Job.AesKey, this.job.UsingDedupe);

                //incremental allowed?
                bool incremental = this.Job.Incremental;

                SnapshotHandler.BackupJobResult jobResult = ssHandler.performFullBackupProcess(ConsistencyLevel.ApplicationAware, true, incremental, this.job);

                //update bytes counter
                totalBytesTransfered += jobResult.bytesTransfered;
                totalBytesProcessed += jobResult.bytesProcessed;

                if (!jobResult.successful) executionSuccessful = false;
            }

            // set job execution state
            JobExecutionProperties executionProps = new JobExecutionProperties();
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

            this.inProgress = false;
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
            }

            return false;
        }

    }
}
