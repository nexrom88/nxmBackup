using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    //defines rotation type
    public struct Rotation
    {
        public RotationType type;
        public int maxElementCount;
    }

    //defines rotation type
    public enum RotationType
    {
        merge, blockRotation
    }

    //defines when to start a backup
    public enum IntervalBase
    {
        hourly, daily, weekly
    }

    //defines the interval details
    public struct Interval
    {
        public IntervalBase intervalBase;
        public int minute;
        public int hour;
        public string day;
    }

    //defines one VM within a job
    public class JobVM
    {
        public string vmID;
        public string vmName;
        public List<VMHDD> vmHDDs;
    }

    //defines one HDD
    public class VMHDD
    {
        public string name;
        public string path;
        public int lbObjectID; //random value for LB hdd identification
        public System.IO.FileStream ldDestinationStream; //LB destination stream - gets set by LB worker
    }
}
