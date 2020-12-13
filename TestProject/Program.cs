﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using HyperVBackupRCT;
using nxmBackup.MFUserMode;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TestProject
{
    class Program
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice([In] string lpDeviceName, [Out] StringBuilder lpTargetPath, [In] int ucchMax);

        static void Main(string[] args)
        {
            //MFUserMode um = new MFUserMode();
            //if (um.connectToKM("\\nxmLRPort", "\\BaseNamedObjects\\nxmmflr"))
            //{

            //    byte[] data = Encoding.Unicode.GetBytes(replaceDriveLetterByDevicePath("c:\\test\\test.vhdx"));
            //    byte[] sendData = new byte[data.Length + 1];
            //    Array.Copy(data, sendData, data.Length);
            //    sendData[sendData.Length - 2] = 0;

            //    um.writeMessage(data);
            //    um.closeConnection();
            //}



            //IntPtr ptr = System.Diagnostics.Process.GetCurrentProcess().Handle;
            //CbStructure parsedFile = CBParser.parseCBFile(@"F:\nxm\Job_1VM\DF86F44C-037D-4111-8EF8-3DC2B3C2F553\31be3d96-d520-452e-b1f3-2b6fe88964cc.nxm\Win10.vhdx.cb", true);
            //string output = JsonConvert.SerializeObject(parsedFile);

            Common.JobVM vm = new Common.JobVM();
            vm.vmID = "0F387D95-6FDF-4CBA-B5AA-B8C2ABAF2F9B";
            HyperVBackupRCT.SnapshotHandler sh = new HyperVBackupRCT.SnapshotHandler(vm, -1);
            sh.cleanUp();


            //RestoreHelper.VMImporter.importVM(@"F:\target\Virtual Machines\78D3C2AC-AEE7-4752-8648-0C3BCA41AE1A.vmcx", @"F:\target", false);


            //Common.vhdxParser parser = new Common.vhdxParser(@"F:\target\Win10.vhdx");
            //Common.RawLog log = parser.getRawLog();
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

            //FileStream inStream = new FileStream(@"F:\nxm\Win10_neu\6097AA29-9454-4496-9580-6BDB637E07B2\8ee5b475-4343-4c2f-8e78-6a9503783c2b.nxm\Win10.vhdx.lb", FileMode.Open, FileAccess.Read);
            //HyperVBackupRCT.LBStructure retVal = HyperVBackupRCT.LBParser.parseLBFile(inStream, true);

            //foreach (HyperVBackupRCT.LBBlock block in retVal.blocks)
            //{
            //    if (block.offset < 32000)
            //    {
            //        inStream = inStream;
            //    }
            //}


            //ConfigHandler.OneJob dummyJob = new ConfigHandler.OneJob();
            //dummyJob.BasePath = "c:\\BasePath";
            //dummyJob.Name = "DummyJob";
            //Common.JobVM vm1 = new Common.JobVM();
            //Common.VMHDD hdd1 = new Common.VMHDD();
            //hdd1.lbObjectID = 1;
            //hdd1.name = "hdd1";
            //hdd1.path = "c:\\target\\file1.txt";
            //Common.VMHDD hdd2 = new Common.VMHDD();
            //hdd2.lbObjectID = 2;
            //hdd2.name = "hdd2";
            //hdd2.path = "c:\\test.mp3";
            //vm1.vmHDDs = new List<Common.VMHDD>();
            //vm1.vmHDDs.Add(hdd1);
            //vm1.vmHDDs.Add(hdd2);
            //dummyJob.JobVMs = new List<Common.JobVM>();
            //dummyJob.JobVMs.Add(vm1);

            //nxmBackup.HVBackupCore.LiveBackupWorker worker = new nxmBackup.HVBackupCore.LiveBackupWorker(dummyJob);
            //if (!worker.startLB())
            //{
            //    return;
            //}

            //Console.ReadLine();
            //worker.stopLB();

        }



        //replaces the drive letter with nt device path
        private static string replaceDriveLetterByDevicePath(string path)
        {
            StringBuilder builder = new StringBuilder(255);
            QueryDosDevice(path.Substring(0, 2), builder, 255);
            path = path.Substring(3);
            return builder.ToString() + "\\" + path;
        }


    }
}