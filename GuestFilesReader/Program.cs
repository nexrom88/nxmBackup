using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Management;
using System.Management.Automation;
using Common;

namespace GuestFilesReader
{
    class Program
    {
        static void Main(string[] args)
        {
            string vhdFile = args[0];
            //mountVHD(vhdFile);
            List<string> drives = getMountedDrives();
            //string[] entries = System.IO.Directory.GetFileSystemEntries(drives[0]);
            
        }

        //mounts vhdx file without driveletter using powershell
        private static void mountVHD(string vhdFile)
        {
            ManagementScope scope = new ManagementScope(@"root\virtualization", null);
            using (ManagementObject imageManagementService = WmiUtilities.GetImageManagementService(scope))
            using (ManagementBaseObject inParams = imageManagementService.GetMethodParameters("AttachVirtualHardDisk"))
            {
            }

        }

        //gets all current mounted drives with no drive letter
        private static List<string> getMountedDrives()
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
    }
}
