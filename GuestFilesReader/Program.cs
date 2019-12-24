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
            string vhdFile = "C:\\restore\\Virtual Hard Disks\\Windows 10.vhdx";

            GuestFilesHandler gfHandler = new GuestFilesHandler(vhdFile);

            List<string> drives = gfHandler.getMountedDrives();

            gfHandler.mountVHD();

            List<string> newDrives = gfHandler.getMountedDrives();
            List<string> mountedDrives = new List<string>();

            foreach (string drive in newDrives)
            {
                if (!drives.Contains(drive))
                {
                    mountedDrives.Add(drive);
                }
            }

            gfHandler.detach();

            string[] entries = System.IO.Directory.GetFileSystemEntries(drives[0]);

            entries = null;
        }

       

        
    }
}
