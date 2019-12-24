using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;

namespace GuestFilesReader
{
    public class GuestFilesHandler
    {
        private string vhdPath;
        private HyperVBackupRCT.VirtualDiskHandler diskHandler;

        public GuestFilesHandler(string vhdPath)
        {
            this.vhdPath = vhdPath;
        }

        //mounts vhdx file without driveletter
        public void mountVHD()
        {
            diskHandler = new HyperVBackupRCT.VirtualDiskHandler(this.vhdPath);
            diskHandler.open(HyperVBackupRCT.VirtualDiskHandler.VirtualDiskAccessMask.AttachReadOnly);
            diskHandler.attach(HyperVBackupRCT.VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER | HyperVBackupRCT.VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY);

        }

        //gets all current mounted drives with no drive letter
        public List<string> getMountedDrives()
        {
            List<string> drives = new List<string>();

            string scopeStr = @"\\.\root\cimv2";


            ManagementScope scope = new ManagementScope(scopeStr);
            scope.Connect();

            string queryString = "SELECT * FROM Win32_Volume WHERE DriveLetter IS NULL";
            SelectQuery query = new SelectQuery(queryString);
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
            {
                foreach (ManagementObject disk in searcher.Get())
                {
                    string mountPoint = disk["Name"].ToString();
                    drives.Add(mountPoint);

                }
            }
            return drives;

        }

        //detaches the vhd
        public void detach()
        {
            this.diskHandler.detach();
        }

    }
}
