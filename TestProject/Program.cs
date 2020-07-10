using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using HyperVBackupRCT;
using MFUserMode;
using nxmBackup.MFUserMode;

namespace TestProject
{
    class Program
    {
        static void Main(string[] args)
        {
            //IntPtr ptr = System.Diagnostics.Process.GetCurrentProcess().Handle;
            //CbStructure parsedFile = CBParser.parseCBFile(@"F:\nxm\Job_1VM\DF86F44C-037D-4111-8EF8-3DC2B3C2F553\31be3d96-d520-452e-b1f3-2b6fe88964cc.nxm\Win10.vhdx.cb", true);
            //string output = JsonConvert.SerializeObject(parsedFile);

            //HyperVBackupRCT.SnapshotHandler sh = new HyperVBackupRCT.SnapshotHandler("DF86F44C-037D-4111-8EF8-3DC2B3C2F553", -1);
            //sh.cleanUp();

            Common.vhdxParser parser = new Common.vhdxParser(@"C:\VMs\Win10.vhdx");
            Common.RegionTable regionTable = parser.parseRegionTable();
            Common.MetadataTable metadataTable = parser.parseMetadataTable(regionTable);
            byte[] id = parser.getVirtualDiskID(metadataTable);

            //MFUserMode.MFUserMode um = new MFUserMode.MFUserMode();
            //um.connectToKM();
            //SharedMemory sm = new SharedMemory();
            //sm.mapSharedBuffer();

            //Common.WMIHelper.listVMs();
        }
        


        
    }
}
