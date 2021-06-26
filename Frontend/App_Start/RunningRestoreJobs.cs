using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Frontend.App_Start
{
    public class RunningRestoreJobs
    {
        private static Object mutex = new object();

        public static long LastHeartbeat { get; set; }
        private static System.Timers.Timer heartbeatCheckTimer = new System.Timers.Timer();

        private static HVRestoreCore.FileLevelRestoreHandler currentFileLevelRestore;
        public static HVRestoreCore.FileLevelRestoreHandler CurrentFileLevelRestore
        {
            get
            {
                lock (mutex)
                {
                    return currentFileLevelRestore;
                }
            }

            set
            {
                lock (mutex)
                {
                    currentFileLevelRestore = value;
                }
            }
        }

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

        //starts the heartbeat check timer
        public static void startHeartBeatCheckTimer()
        {
            heartbeatCheckTimer.Elapsed += new System.Timers.ElapsedEventHandler(onHeartbeatCheck);
            heartbeatCheckTimer.Interval = 10000;
            heartbeatCheckTimer.Enabled = true;
        }

        private static void onHeartbeatCheck(object source, System.Timers.ElapsedEventArgs e)
        {
            //LR timeout?
            if (CurrentLiveRestore != null && CurrentLiveRestore.State == HVRestoreCore.LiveRestore.lrState.running)
            {
                long currentTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                if (currentTimestamp - LastHeartbeat >= 10)
                {
                    CurrentLiveRestore.StopRequest = true;
                    CurrentLiveRestore = null;
                }
            }

            //flr timeout?
            if (CurrentFileLevelRestore != null && CurrentFileLevelRestore.State.type == HVRestoreCore.FileLevelRestoreHandler.flrStateType.running)
            {
                long currentTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                if (currentTimestamp - LastHeartbeat >= 10)
                {
                    currentFileLevelRestore.StopRequest = true;
                    CurrentFileLevelRestore = null;
                }
            }

        }

    }
}