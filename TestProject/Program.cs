using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using HyperVBackupRCT;
using nxmBackup.MFUserMode;
using System.ComponentModel;

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

            //Common.vhdxParser parser = new Common.vhdxParser(@"C:\VMs\Win10.vhdx");
            //Common.RegionTable regionTable = parser.parseRegionTable();
            //Common.MetadataTable metadataTable = parser.parseMetadataTable(regionTable);
            //byte[] id = parser.getVirtualDiskID(metadataTable);

            //MFUserMode. um = new MFUserMode.MFUserMode();
            //um.connectToKM("\\nxmLBPort", "\\BaseNamedObjects\\nxmmflb");
            //byte[] data = new byte[261];
            //data[0] = 1;
            //byte[] intDummy = BitConverter.GetBytes(666);
            //for (int i = 0; i < 4; i++)
            //{
            //    data[i + 1] = intDummy[i];
            //}
            //string path = "c:\\target\\test.txt";

            //string d = replaceDriveLetterByDevicePath(path);


            //byte[] strDummy = Encoding.ASCII.GetBytes("c:\\testt.vhdx");
            //for (int i = 0; i < strDummy.Length; i++)
            //{
            //    data[i + 5] = strDummy[i];
            //}
            //bool a = um.writeMessage(data);

            //data[0] = 2;
            //um.writeMessage(data);

            //MFUserMode.MFUserMode.LB_BLOCK block = um.handleLBMessage();

            //um.closeConnection();


            //SharedMemory sm = new SharedMemory();
            //sm.mapSharedBuffer();

            //Common.WMIHelper.listVMs();

            //DriveInfo[] drives = System.IO.DriveInfo.GetDrives();
            //drives = null;


            ConfigHandler.OneJob dummyJob = new ConfigHandler.OneJob();
            Common.JobVM vm1 = new Common.JobVM();
            Common.VMHDD hdd1 = new Common.VMHDD();
            hdd1.lbObjectID = 1;
            hdd1.name = "hdd1";
            hdd1.path = "c:\\target\\file1.txt";
            Common.VMHDD hdd2 = new Common.VMHDD();
            hdd2.lbObjectID = 2;
            hdd2.name = "hdd2";
            hdd2.path = "c:\\target\\file2.txt";
            vm1.vmHDDs = new List<Common.VMHDD>();
            vm1.vmHDDs.Add(hdd1);
            vm1.vmHDDs.Add(hdd2);
            dummyJob.JobVMs = new List<Common.JobVM>();
            dummyJob.JobVMs.Add(vm1);

            nxmBackup.HVBackupCore.LiveBackupWorker worker = new nxmBackup.HVBackupCore.LiveBackupWorker(dummyJob);
            worker.startLB();

            Console.ReadLine();
            worker.stopLB();

        }


        


        
    }
}
