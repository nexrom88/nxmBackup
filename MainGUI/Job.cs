using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MainGUI
{
    class Job
    {        
        string name = "";
        string type = "";
        string currentStatus = "";
        bool lastRunSuccessful = false;
        DateTime nextRun;
        DateTime lastRun;


        public Job()
        {


        }
        public Job(string name, string type, string currentStatus, bool lastRunSuccessful, DateTime nextRun, DateTime lastRun)
        {
            Name = name;
            Type = type;
            CurrentStatus = currentStatus;
            LastRunSuccessful = lastRunSuccessful;
            NextRun = nextRun;
            LastRun = lastRun;
        }

        public string Name { get => name; set => name = value; }
        public string Type { get => type; set => type = value; }
        public string CurrentStatus { get => currentStatus; set => currentStatus = value; }
        public bool LastRunSuccessful { get => lastRunSuccessful; set => lastRunSuccessful = value; }
        public DateTime NextRun { get => nextRun; set => nextRun = value; }
        public DateTime LastRun { get => lastRun; set => lastRun = value; }


    }
}
