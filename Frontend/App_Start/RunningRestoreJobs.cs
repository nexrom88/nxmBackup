using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Frontend.App_Start
{
    public class RunningRestoreJobs
    {
        private static Object mutex = new object();

        private static HVRestoreCore.LiveRestore currentLiveRestore;
        public static HVRestoreCore.LiveRestore CurrentLiveRestore
        {
            get
            {
                lock (mutex)
                {
                    return currentLiveRestore;
                }
            }

            set
            {
                lock (mutex)
                {
                    currentLiveRestore = value;
                }
            }
        }
    }
}