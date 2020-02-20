using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using ConfigHandler;
using HyperVBackupRCT;

namespace JobEngine
{
    public class JobTimer
    {
        private ConfigHandler.OneJob job;
        private bool inProgress = false;
        private Common.Job.newEventDelegate newEvent;

        public JobTimer(ConfigHandler.OneJob job, Common.Job.newEventDelegate newEvent)
        {
            this.Job = job;
            this.newEvent = newEvent;
        }

        public OneJob Job { get => job; set => job = value; }

        //gets raised frequently
        public void tick(object sender, System.Timers.ElapsedEventArgs e)
        {
            startJob(false);
        }

        //starts the job
        public void startJob(bool userInitiated)
        {
            //check whether job is still in progress
            if (this.inProgress)
            {
                if (userInitiated)
                {
                    MessageBox.Show("Job wird bereits ausgeführt.");
                }
                return;
            }

            this.inProgress = true;

            //iterate vms within the current job
            foreach (JobVM vm in this.Job.JobVMs)
            {
                SnapshotHandler ssHandler = new SnapshotHandler(vm.vmName);                
                ssHandler.performFullBackupProcess(ConsistencyLevel.ApplicationAware, true, this.Job.BasePath, true, this.job);
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
