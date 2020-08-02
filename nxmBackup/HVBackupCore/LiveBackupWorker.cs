using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.ComponentModel;

namespace nxmBackup.HVBackupCore
{
    public class LiveBackupWorker
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice([In] string lpDeviceName, [Out] StringBuilder lpTargetPath, [In] int ucchMax);

        private ConfigHandler.OneJob selectedJob;
        private bool isRunning = false;
        private MFUserMode.MFUserMode um;
        private Thread lbReadThread;
        private string destGUIDFolder;

        public LiveBackupWorker(ConfigHandler.OneJob job)
        {
            this.selectedJob = job;
        }

        //starts LB
        public bool startLB()
        {

            isRunning = true;

            //connect to km and shared memory
            this.um = new MFUserMode.MFUserMode();
            bool status = this.um.connectToKM("\\nxmLBPort", "\\BaseNamedObjects\\nxmmflb");

            //quit when connection not successful
            if (!status)
            {
                isRunning = false;
                return false;
            }

            //start lb reading thred
            this.lbReadThread = new Thread(() => readLBMessages());
            this.lbReadThread.Start();

            //iterate through all vms
            foreach (Common.JobVM vm in this.selectedJob.JobVMs)
            {
                //add vm-LB to backup config.xml
                initFileStreams(vm);

                //iterate through all hdds
                foreach (Common.VMHDD hdd in vm.vmHDDs)
                {
                    byte[] data = new byte[261];
                    data[0] = 1;
                    byte[] objectIDBuffer = BitConverter.GetBytes(hdd.lbObjectID);
                    for (int i = 0; i < 4; i++)
                    {
                        data[i + 1] = objectIDBuffer[i];
                    }
                    string path = replaceDriveLetterByDevicePath(hdd.path);
                    byte[] pathBuffer = Encoding.ASCII.GetBytes(path);
                    for (int i = 0; i < pathBuffer.Length; i++)
                    {
                        data[i + 5] = pathBuffer[i];
                    }

                    //write data buffers to km
                    um.writeMessage(data);
                }
            }

            return true;
        }

        //returns filestreams for destination files
        private void initFileStreams(Common.JobVM vm)
        {
            //add new backup
            Guid g = Guid.NewGuid();
            string guidFolder = g.ToString();
           
            //create lb folder
            string destFolder = System.IO.Path.Combine(this.selectedJob.BasePath, this.selectedJob.Name + "\\" + vm.vmID + "\\" + guidFolder + ".nxm");
            System.IO.Directory.CreateDirectory(destFolder);

            //create destination files and their corresponding streams
            for (int i = 0; i < vm.vmHDDs.Count; i++)
            {
                string fileName = System.IO.Path.GetFileName(vm.vmHDDs[i].path) + ".lb";
                fileName = System.IO.Path.Combine(destFolder, fileName);
                System.IO.FileStream stream = new System.IO.FileStream(fileName, System.IO.FileMode.Create);

                //build new hdd struct, otherwise it cannot be changed within Arraylist
                Common.VMHDD newHDD = new Common.VMHDD();
                newHDD.lbObjectID = vm.vmHDDs[i].lbObjectID;
                newHDD.name = vm.vmHDDs[i].name;
                newHDD.path = vm.vmHDDs[i].path;
                newHDD.ldDestinationStream = stream;
                vm.vmHDDs.RemoveAt(i);
                vm.vmHDDs.Insert(i,newHDD);
            }

            this.destGUIDFolder = guidFolder;
        }

        //adds the vm-LB backup to destination config.xml file for the given vm
        public void addToBackupConfig()
        {
            //iterate through all vms
            foreach (Common.JobVM vm in this.selectedJob.JobVMs)
            {
                //read existing backup chain
                List<ConfigHandler.BackupConfigHandler.BackupInfo> currentChain = ConfigHandler.BackupConfigHandler.readChain(System.IO.Path.Combine(this.selectedJob.BasePath, this.selectedJob.Name + "\\" + vm.vmID));

                //get parent backup
                string parentInstanceID = currentChain[currentChain.Count - 1].instanceID;

                ConfigHandler.BackupConfigHandler.addBackup(System.IO.Path.Combine(this.selectedJob.BasePath, this.selectedJob.Name + "\\" + vm.vmID), this.destGUIDFolder, "lb", "nxm:" + this.destGUIDFolder, parentInstanceID, false);

            }

        }

        //reads km lb messages
        private void readLBMessages()
        {
            try
            {
                while (this.isRunning)
                {
                    MFUserMode.MFUserMode.LB_BLOCK lbBlock = this.um.handleLBMessage();

                    if (lbBlock.isValid)
                    {
                        Common.JobVM targetVM;
                        Common.VMHDD targetHDD;
                        //look for corresponding vm and hdd
                        foreach(Common.JobVM vm in this.selectedJob.JobVMs)
                        {
                            foreach(Common.VMHDD hdd in vm.vmHDDs)
                            {
                                if (hdd.lbObjectID == lbBlock.objectID)
                                {
                                    targetHDD = hdd;
                                    targetVM = vm;

                                    //save the block to backup destination
                                    storeLBBlock(lbBlock, vm, hdd);
                                }
                            }
                        }
                    }  
                }
            }catch(Exception ex)
            {
                ex = ex;
            }
        }

        //writes LB block to corresponding backup path
        private void storeLBBlock(MFUserMode.MFUserMode.LB_BLOCK lbBlock, Common.JobVM vm, Common.VMHDD hdd)
        {
            //write data to stream if stream exists
            if (hdd.ldDestinationStream != null)
            {
                byte[] buffer;

                //write timestamp
                ulong timeStamp = ulong.Parse(DateTime.Now.ToString("yyyyMMddHHmmss"));
                buffer = BitConverter.GetBytes(timeStamp);
                hdd.ldDestinationStream.Write(buffer, 0, 8);

                //write offset
                buffer = BitConverter.GetBytes(lbBlock.offset);
                hdd.ldDestinationStream.Write(buffer, 0, 8);

                //write length
                buffer = BitConverter.GetBytes(lbBlock.length);
                hdd.ldDestinationStream.Write(buffer, 0, 8);

                //write payload data
                hdd.ldDestinationStream.Write(lbBlock.buffer, 0, (int)lbBlock.length);

                //flush stream
                hdd.ldDestinationStream.Flush();
            }
        }

        //stops LB
        public void stopLB()
        {
            if (isRunning)
            {
                isRunning = false;

                //wait a while to not force the thread to exit
                Thread.Sleep(500);

                this.um.closeConnection();
                this.lbReadThread.Abort();

                //close all open filestreams
                foreach(Common.JobVM vm in this.selectedJob.JobVMs)
                {
                    foreach(Common.VMHDD hdd in vm.vmHDDs)
                    {
                        hdd.ldDestinationStream.Close();
                    }
                }
            }
        }

        //replaces the drive letter with nt device path
        private string replaceDriveLetterByDevicePath(string path)
        {
            StringBuilder builder = new StringBuilder(255);
            QueryDosDevice(path.Substring(0, 2), builder, 255);
            path = path.Substring(3);
            return builder.ToString() + "\\" + path;
        }

    }
}

//file structure
//8 bytes = offset
//8 bytes = length
//length bytes = payload data
