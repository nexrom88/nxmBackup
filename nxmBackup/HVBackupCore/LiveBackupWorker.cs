using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using System.IO;

namespace nxmBackup.HVBackupCore
{
    public class LiveBackupWorker
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice([In] string lpDeviceName, [Out] StringBuilder lpTargetPath, [In] int ucchMax);

        private static List<LiveBackupWorker> workers = new List<LiveBackupWorker>();
        public static List<LiveBackupWorker> ActiveWorkers { get => workers; set => workers = value; }

        private int selectedJobID;
        private bool isRunning = false;
        private MFUserMode.MFUserMode um;
        private Thread lbReadThread;
        private string backupuuid;
        private Common.EventHandler eventHandler;
        private const int NO_RELATED_EVENT = -1;

        private int eventID;
        private UInt64 processedBytes = 0;
        private UInt64 lastShownBytes = 0; //for progress calculation
        private string lastPrettyPrintedBytes = ""; //for progress calculation

        public bool IsRunning { get => isRunning; set => isRunning = value; }
        public int JobID { get => selectedJobID; set => selectedJobID = value; }
        public List<LBHDDWorker> RunningHDDWorker { get => runningHDDWorker; set => runningHDDWorker = value; }
        

        private List<LBHDDWorker> runningHDDWorker = new List<LBHDDWorker> ();


        public LiveBackupWorker(int jobID, Common.EventHandler eventHandler)
        {
            this.eventHandler = eventHandler;
            this.JobID = jobID;
        }

        //starts LB
        public bool startLB()
        {
            
            isRunning = true;

            //raise event
            this.eventID = this.eventHandler.raiseNewEvent("LiveBackup läuft...", false, false, NO_RELATED_EVENT, Common.EventStatus.info);

            //load job object. It gets loaded dynamically because the object can change while lb is running
            ConfigHandler.OneJob jobObject = getJobObject();


            //connect to km and shared memory
            this.um = new MFUserMode.MFUserMode();
            bool status = this.um.connectToKM("\\nxmLBPort", "\\BaseNamedObjects\\nxmmflb");


            //quit when connection not successful
            if (!status || jobObject == null)
            {
                isRunning = false;
                this.eventHandler.raiseNewEvent("Livebackup konnte nicht gestartet werden", false, true, this.eventID, Common.EventStatus.error);
                this.eventHandler.raiseNewEvent("", true, false, this.eventID, Common.EventStatus.error);
                return false;
            }

                        

            //iterate through all vms
            foreach (Common.JobVM vm in jobObject.JobVMs)
            {
                //add vm-LB to backup config.xml
                initFileStreams(vm, jobObject);

                //iterate through all hdds
                foreach (Common.VMHDD hdd in vm.vmHDDs)
                {
                    //message for KM struct:
                    //1 byte = add(1), delete(2), stoplb(3)
                    //4 bytes = object id
                    //256 bytes = path string

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
                    this.um.writeMessage(data);
                }
            }

            //set start timestamp to db
            this.eventHandler.setLBStart();

            //start lb reading thred
            this.lbReadThread = new Thread(() => readLBMessages(jobObject));
            this.lbReadThread.Start();

            return true;
        }

        //gets the current job object
        private ConfigHandler.OneJob getJobObject()
        {
            foreach (ConfigHandler.OneJob job in ConfigHandler.JobConfigHandler.Jobs)
            {
                if (job.DbId == this.JobID)
                {
                    return job;
                }
            }

            return null;
        }

        //returns filestreams for destination files
        private void initFileStreams(Common.JobVM vm, ConfigHandler.OneJob jobObject)
        {
            //add new backup
            Guid g = Guid.NewGuid();
            string guidFolder = g.ToString();
           
            //create lb folder
            string destFolder = System.IO.Path.Combine(jobObject.TargetPath, jobObject.Name + "\\" + vm.vmID + "\\" + guidFolder + ".nxm");
            System.IO.Directory.CreateDirectory(destFolder);

            //create destination files and their corresponding streams
            for (int i = 0; i < vm.vmHDDs.Count; i++)
            {
                string fileName = System.IO.Path.GetFileName(vm.vmHDDs[i].path) + ".lb";
                fileName = System.IO.Path.Combine(destFolder, fileName);
                System.IO.FileStream stream = new System.IO.FileStream(fileName, System.IO.FileMode.Create);

                //write dummy to first 8 bytes (vhdxSize, gets filled later)
                byte[] buffer = new byte[8];
                stream.Write(buffer, 0, 8);

                //build new lbworker struct
                LBHDDWorker newWorker = new LBHDDWorker();
                newWorker.lbFileStream = stream;
                newWorker.hddPath = vm.vmHDDs[i].path;
                newWorker.lbObjectID = vm.vmHDDs[i].lbObjectID;
                RunningHDDWorker.Add(newWorker);
            }

            this.backupuuid = guidFolder;
        }

        //adds the vm-LB backup to destination config.xml file for the given vm
        public void addToBackupConfig()
        {
            //load job object
            ConfigHandler.OneJob jobObject = getJobObject();

            if (jobObject == null)
            {
                Common.DBQueries.addLog("job object not found", Environment.StackTrace, null);
                return;
            }

            //iterate through all vms
            foreach (Common.JobVM vm in jobObject.JobVMs)
            {
                //read existing backup chain
                List<ConfigHandler.BackupConfigHandler.BackupInfo> currentChain = ConfigHandler.BackupConfigHandler.readChain(System.IO.Path.Combine(jobObject.TargetPath, jobObject.Name + "\\" + vm.vmID), false);

                //get parent backup
                string parentInstanceID = currentChain[currentChain.Count - 1].instanceID;

                ConfigHandler.BackupConfigHandler.addBackup(System.IO.Path.Combine(jobObject.TargetPath, jobObject.Name + "\\" + vm.vmID), jobObject.UseEncryption, this.backupuuid, "lb", "nxm:" + this.backupuuid, parentInstanceID, false, this.eventHandler.ExecutionId.ToString());

            }

        }

        //sets the lb end time within backup xml file
        private void setLBEndTime()
        {
            //load job object
            ConfigHandler.OneJob jobObject = getJobObject();

            if (jobObject == null)
            {
                Common.DBQueries.addLog("job object not found for setting lb end time", Environment.StackTrace, null);
                return;
            }

            //iterate through all vms
            foreach (Common.JobVM vm in jobObject.JobVMs)
            {
                //read existing backup chain
                List<ConfigHandler.BackupConfigHandler.BackupInfo> currentChain = ConfigHandler.BackupConfigHandler.readChain(System.IO.Path.Combine(jobObject.TargetPath, jobObject.Name + "\\" + vm.vmID), false);

                //get parent backup
                string parentInstanceID = currentChain[currentChain.Count - 1].instanceID;
                ConfigHandler.BackupConfigHandler.setLBEndTime(System.IO.Path.Combine(jobObject.TargetPath, jobObject.Name + "\\" + vm.vmID), this.backupuuid);
            }
        }

        //reads km lb messages
        private void readLBMessages(ConfigHandler.OneJob jobObject)
        {
            try
            {
                bool sizeLimitReached = false;

                while (this.isRunning && !sizeLimitReached)
                {
                    MFUserMode.MFUserMode.LB_BLOCK lbBlock = this.um.handleLBMessage();

                    this.processedBytes += (UInt64)lbBlock.length;

                    //just show progress every 10 MB
                    if (this.processedBytes - this.lastShownBytes >= 10000000)
                    {
                        //check whether lb has reached size limit
                        if ((ulong)jobObject.LiveBackupSize *1000000000 <= this.processedBytes)
                        {
                            //size limit reached, cancel
                            sizeLimitReached = true;
                        }

                        this.lastShownBytes = this.processedBytes;
                        string prettyPrintedBytes = Common.PrettyPrinter.prettyPrintBytes((long)this.processedBytes);

                        //just add to DB when string changed
                        if (prettyPrintedBytes != this.lastPrettyPrintedBytes)
                        {
                            this.lastPrettyPrintedBytes = prettyPrintedBytes;
                            this.eventHandler.raiseNewEvent("LiveBackup läuft... " + prettyPrintedBytes + " verarbeitet", false, true, this.eventID, Common.EventStatus.info);
                        }
                    }

                    

                    if (lbBlock.isValid)
                    {
                        //look for corresponding vm and hdd
                        foreach(LBHDDWorker hddWorker in RunningHDDWorker)
                        {

                            if (lbBlock.objectID == hddWorker.lbObjectID)
                            {
                                //save the block to backup destination
                                storeLBBlock(lbBlock, hddWorker.lbFileStream);
                            }

                        }
                    }  
                }

                if (sizeLimitReached)
                {
                    stopLB();
                }

            }catch(Exception ex)
            {
            }
        }

        //writes LB block to corresponding backup path
        private void storeLBBlock(MFUserMode.MFUserMode.LB_BLOCK lbBlock, System.IO.FileStream lbDestinationStream)
        {
            //write data to stream if stream exists
            if (lbDestinationStream != null)
            {
                byte[] buffer;

                //write timestamp
                ulong timeStamp = ulong.Parse(DateTime.Now.ToString("yyyyMMddHHmmss"));
                buffer = BitConverter.GetBytes(timeStamp);
                lbDestinationStream.Write(buffer, 0, 8);

                //write offset
                buffer = BitConverter.GetBytes(lbBlock.offset);
                lbDestinationStream.Write(buffer, 0, 8);

                //write length
                buffer = BitConverter.GetBytes(lbBlock.length);
                lbDestinationStream.Write(buffer, 0, 8);

                //write payload data
                lbDestinationStream.Write(lbBlock.buffer, 0, (int)lbBlock.length);

                //flush stream
                lbDestinationStream.Flush();
            }
        }

        //stops LB
        public void stopLB()
        {
            //load job object dynamically
            ConfigHandler.OneJob jobObject = getJobObject();

            //remove myself from global list of workers
            LiveBackupWorker.ActiveWorkers.Remove(this);

            //look for corresponding job object
            foreach (ConfigHandler.OneJob job in ConfigHandler.JobConfigHandler.Jobs)
            {
                //set lb to "not active"
                if (job.DbId == this.JobID)
                {
                    job.LiveBackupActive = false;
                }
            }

            if (isRunning)
            {
                isRunning = false;

                //wait a while to not force the thread to exit
                Thread.Sleep(500);

                //set start timestamp to db
                this.eventHandler.setLBStop();
                this.um.closeConnection(true);

                //set end time to xml file
                setLBEndTime();

                //just cancel thread when lbReadThread is not current thread
                if (this.lbReadThread != Thread.CurrentThread)
                {
                    this.lbReadThread.Abort();
                }

                //close all open filestreams
                foreach(LBHDDWorker worker in RunningHDDWorker)
                {
                    //get vhdx size
                    System.IO.FileInfo fileInfo = new System.IO.FileInfo(worker.hddPath);
                    UInt64 vhdxSize;
                    try
                    {
                        vhdxSize = (UInt64)fileInfo.Length;
                    }
                    catch (Exception)
                    {
                        vhdxSize = 0;
                    }

                    //write vhdx size to file header
                    worker.lbFileStream.Seek(0, System.IO.SeekOrigin.Begin);
                    byte[] buffer = BitConverter.GetBytes(vhdxSize);
                    worker.lbFileStream.Write(buffer, 0, 8);

                    //close stream
                    worker.lbFileStream.Close();

                    

                }
                

                //write stop to DB
                this.eventHandler.raiseNewEvent("LiveBackup beendet. " + this.lastPrettyPrintedBytes + " verarbeitet", false, true, this.eventID, Common.EventStatus.info);
                this.eventHandler.raiseNewEvent("", true, false, this.eventID, Common.EventStatus.info);
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

    public struct LBHDDWorker
    {
        public System.IO.FileStream lbFileStream;
        public int lbObjectID;
        public string hddPath;
    }
}

//file structure
//8 bytes = vhdx size

//one block:
//8 bytes: timestamp (yyyyMMddHHmmss)
//8 bytes: payload offset
//8 bytes: payload length
//x bytes: payload
