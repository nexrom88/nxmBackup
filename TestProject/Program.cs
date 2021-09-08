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
using System.Runtime.InteropServices;

namespace TestProject
{
    class Program
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice([In] string lpDeviceName, [Out] StringBuilder lpTargetPath, [In] int ucchMax);

        static void Main(string[] args)
        {

            //UInt64 desiredOffset = 17096990720;
            //string file = @"F:\nxm\Fixed\1661C788-7F70-4203-8255-628F95087182\74fcbe14-b042-4802-ad18-93f46cfbe008.nxm\Win10_Fixed.vhdx.cb";
            //FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read);
            //BlockCompression.LZ4BlockStream blockStream = new BlockCompression.LZ4BlockStream(stream, BlockCompression.AccessMode.read, false, null);
            //blockStream.init();


            //CbStructure cbStructure = CBParser.parseCBFile(blockStream, false);


            //UInt64 smallestDist = Int64.MaxValue;
            //UInt64 smallestOffset;
            //UInt64 smallestIndex = 0;

            //UInt64 i = 0;
            //foreach (CbBlock block in cbStructure.blocks)
            //{
            //    foreach (VhdxBlockLocation loc in block.vhdxBlockLocations)
            //    {
            //        if (loc.vhdxOffset <= desiredOffset &&  (Int64)desiredOffset - (Int64)loc.vhdxOffset < (Int64)smallestDist)
            //        {
            //            smallestDist = (UInt64)Math.Abs((Int64)loc.vhdxOffset - (Int64)desiredOffset);
            //            smallestOffset = loc.vhdxOffset;
            //            smallestIndex = i;
            //        }
            //    }

            //    i++;
            //}


            //UInt32 bufferSize = 10000000;
            //byte[] buffer = new byte[bufferSize];
            //UInt64 bytesRead = 0;
            //UInt64 blockLength = cbStructure.blocks[(int)smallestIndex].changedBlockLength;
            //blockStream.Seek((long)cbStructure.blocks[(int)smallestIndex].cbFileOffset, SeekOrigin.Begin);

            //System.IO.FileStream outStream = new FileStream(@"f:\debug.bin", FileMode.Create, FileAccess.Write);

            ////read blocks
            //while (bytesRead + bufferSize <= blockLength) {
            //    blockStream.Read(buffer, 0, (int)bufferSize);
            //    outStream.Write(buffer, 0, buffer.Length);
            //    bytesRead += bufferSize;
            //}

            ////read last block
            //if (bytesRead < blockLength)
            //{
            //    buffer = new byte[blockLength - bytesRead];
            //    blockStream.Read(buffer, 0, buffer.Length);
            //    outStream.Write(buffer, 0, buffer.Length);
            //}
            //outStream.Close();
            //blockStream.Close();


            //string json = JsonConvert.SerializeObject(cbStructure.blocks);
            //json = json;




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

            //Common.JobVM vm = new Common.JobVM();
            //vm.vmID = "7A571DAE-9111-4576-897C-8E266EE51FFB"; //ubuntu
            //vm.vmID = "2F8C8382-4D06-4AD7-BCC3-0BFED03199AC"; //Lubuntu
            //vm.vmID = "3D898EAA-C76F-41D6-8EC5-DE18E3F01157"; //win10_1hdd
            //nxmBackup.HVBackupCore.SnapshotHandler sh = new nxmBackup.HVBackupCore.SnapshotHandler(vm, -1, false, null);
            //sh.cleanUp();


            //RestoreHelper.VMImporter.importVM(@"F:\target\Virtual Machines\78D3C2AC-AEE7-4752-8648-0C3BCA41AE1A.vmcx", @"F:\target", false);


            Common.vhdxParser parser = new Common.vhdxParser(@"d:\original_fixed.vhdx");
            Common.RawLog log = parser.getRawLog();
            Common.RegionTable regionTable = parser.parseRegionTable();
            Common.MetadataTable metadataTable = parser.parseMetadataTable(regionTable);
            uint lss = parser.getLogicalSectorSize(metadataTable);
            uint blockSize = parser.getBlockSize(metadataTable);

            UInt32 vhdxChunkRatio = (UInt32)((Math.Pow(2, 23) * (double)lss) / (double)blockSize);

            UInt64 dbc = (UInt64)Math.Ceiling((double)parser.getVirtualDiskSize(metadataTable) / (double)blockSize);

            UInt32 sectorBitmapBlocksCount = (UInt32)Math.Ceiling((double)dbc / (double)vhdxChunkRatio);

            Common.BATTable bat = parser.parseBATTable(regionTable, vhdxChunkRatio, sectorBitmapBlocksCount, true);



            UInt64 desiredOffset = 17083400192;
            for(int i = 0; i < bat.entries.Count; i++)
            {
                if (bat.entries[i].FileOffsetMB * 1048576 == desiredOffset)
                {
                    i = i;
                }
            }


            string json = JsonConvert.SerializeObject(bat.entries);
            json = json;

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
