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

        public JobTimer(ConfigHandler.OneJob job) 
        {
            this.Job = job;
        }

        public OneJob Job { get => job; set => job = value; }

        //gets raised frequently
        public void tick(object sender, System.Timers.ElapsedEventArgs e)
        {
         
            startJob(false);
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
            foreach (JobVM vm in this.Job.JobVMs)
            {
                SnapshotHandler ssHandler = new SnapshotHandler(vm, executionId);
                bool successful = ssHandler.performFullBackupProcess(ConsistencyLevel.ApplicationAware, true, true, this.job);
                if (!successful) executionSuccessful = false;
            }

            // set job execution state
            if (executionSuccessful)
            {
                JobExecutionProperties executionProps = new JobExecutionProperties();
                executionProps.stopTime = DateTime.Now;
                executionProps.isRunning = false;
                executionProps.transferRate = 0;
                executionProps.alreadyRead = 0;
                executionProps.alreadyWritten = 0;
                executionProps.successful = executionSuccessful;
                executionProps.warnings = 0;
                executionProps.errors = 0;
                Common.DBQueries.updateJobExecution(executionProps, executionId.ToString());
            }

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
