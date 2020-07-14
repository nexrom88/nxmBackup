using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;

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

        public LiveBackupWorker(ConfigHandler.OneJob job)
        {
            this.selectedJob = job;
        }

        //starts LB
        public void startLB()
        {
            isRunning = true;

            //connect to km and shared memory
            this.um = new MFUserMode.MFUserMode();
            this.um.connectToKM("\\nxmLBPort", "\\BaseNamedObjects\\nxmmflb");

            //start lb reading thred
            this.lbReadThread = new Thread(() => readLBMessages());
            this.lbReadThread.Start();

            //iterate through all vms
            foreach (Common.JobVM vm in this.selectedJob.JobVMs)
            {
                //iterate through all hdds
                foreach(Common.VMHDD hdd in vm.vmHDDs)
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
        }

        //reads km lb messages
        private void readLBMessages()
        {
            try
            {
                while (this.isRunning)
                {
                    MFUserMode.MFUserMode.LB_BLOCK lbBlock = this.um.handleLBMessage();

                    System.IO.File.AppendAllText("c:\\output.txt", lbBlock.offset + "-" + lbBlock.length + "-" + lbBlock.objectID);
                }
            }catch(Exception ex)
            {
                ex = ex;
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
