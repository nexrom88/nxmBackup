using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Frontend.App_Start
{
    public class GUIJobHandler
    {
        public static JobEngine.JobHandler jobHandler = null;

        public static void initJobs()
        {
            //start job engine
            jobHandler = new JobEngine.JobHandler();

            if (!jobHandler.startJobEngine())
            {
                //db error occured while starting job engine
                jobHandler = null;
                return;
            }
        }

        //checks if a given job is currently running
        public static bool isJobRunning(int jobID)
        {
            if (jobHandler != null)
            {
                return jobHandler.isJobRunning(jobID);
            }
            else
            {
                return false;
            }
        }
    }
}