using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    //defines wmi scope options
    public struct WMIConnectionOptions
    {
        public string host;
        public string user;
        public string password;
    }

    //defines properties for enryption
    public struct EncryptionProperties
    {
        public bool useEncryption;
        public byte[] aesKey;
    }

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
        hourly, daily, weekly, never
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
        public string hostID;
        public string host;
        public List<VMHDD> vmHDDs;

        private ConnectionOptions connectionOptions;

        public ConnectionOptions getHostAuthData()
        {
            //read wmi connection options when not already done
            if (connectionOptions == null)
            {
                WMIConnectionOptions options = DBQueries.getHostByID(int.Parse(this.hostID), true);

                //build wmi native object
                return WMIHelper.buildConnectionOptions(options);
            }
            else
            {
                return connectionOptions;
            }
        }
    }

    //defines one HDD
    public class VMHDD
    {
        public string name;
        public string path;
        public int lbObjectID;
    }

    public struct TransferDetails
    {
        public UInt64 bytesProcessed;
        public UInt64 bytesTransfered;
        public bool successful;
        public bool retryFullBackupOnFail;
    }
}
