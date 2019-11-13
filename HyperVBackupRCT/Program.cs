using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;

namespace HyperVBackupRCT
{
    [Obsolete]
    class Program
    {
        static void Main(string[] args)
        {
            startBackup("CentOS7", ConsistencyLevel.ApplicationAware);
            //cleanUp("CentOS7");
            Console.WriteLine("done");
            Console.ReadLine();
        }

        //starts the backup process
        private static void startBackup(string vmName, ConsistencyLevel cLevel)
        {
            //SnapshotHandler snapshotHandler = new SnapshotHandler(vmName);
            //ManagementObject snapshot = snapshotHandler.getSnapshots()[0];
            //ManagementObject refP = snapshotHandler.getReferencePoints()[0];
            //snapshotHandler.export("d:\\backup_inc", snapshot, refP);

            //RestoreHandler re = new RestoreHandler();
            //re.restore();

            //cleanUp(vmName);

        }

        
        
    }
}
