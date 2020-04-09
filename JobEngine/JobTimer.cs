using System;
using System.Collections.Generic;
using System.Text;
using ConfigHandler;
using HyperVBackupRCT;

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
                Common.EventProperties props = new Common.EventProperties();
                props.text = "Skipping";


                return;
            }

            this.inProgress = true;

            //get new execution ID
            int executionId = Common.DBQueries.addJobExecution(job.DbId.ToString());

            //iterate vms within the current job
            foreach (ConfigHandler.JobVM vm in this.Job.JobVMs)
            {
                SnapshotHandler ssHandler = new SnapshotHandler(vm.vmID, executionId);
                ssHandler.performFullBackupProcess(ConsistencyLevel.ApplicationAware, true, true, this.job);
            }

            this.inProgress = false;
        }

        //checks whether the job has to start now or not
        private bool isDue()
        {
            DateTime now = DateTime.Now;

            switch (this.Job.Interval.intervalBase)
            {
                case ConfigHandler.IntervalBase.hourly: //hourly backup due?
                    return now.Minute == int.Parse(this.Job.Interval.minute);

                case ConfigHandler.IntervalBase.daily: //daily backup due?
                    return now.Minute == int.Parse(this.Job.Interval.minute) && now.Hour == int.Parse(this.Job.Interval.hour);

                case ConfigHandler.IntervalBase.weekly: //weekly backup due?
                    if(now.Minute == int.Parse(this.Job.Interval.minute) && now.Hour == int.Parse(this.Job.Interval.hour))
                    {
                        return now.DayOfWeek.ToString().ToLower() == this.Job.Interval.day;
                    }
                    return false;
            }

            return false;
        }

    }
}
