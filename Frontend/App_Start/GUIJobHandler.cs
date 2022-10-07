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

        
    }
}