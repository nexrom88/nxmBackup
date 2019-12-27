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
        public List<GuestVolume> getMountedDrives()
        {
            List<GuestVolume> drives = new List<GuestVolume>();
            
            string scopeStr = @"\\.\root\cimv2";


            ManagementScope scope = new ManagementScope(scopeStr);
            scope.Connect();

            string queryString = "SELECT * FROM Win32_Volume WHERE DriveLetter IS NULL";
            SelectQuery query = new SelectQuery(queryString);
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
            {
                foreach (ManagementObject disk in searcher.Get())
                {
                    GuestVolume volume = new GuestVolume();
                    volume.path = disk["Name"].ToString();

                    if (disk["Label"] == null)
                    {
                        volume.caption = "Unbenanntes Laufwerk";
                    }
                    else
                    {
                        volume.caption = disk["Label"].ToString();
                    }
                    drives.Add(volume);

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

    public struct GuestVolume
    {
        public string path;
        public string caption;
    }
}
