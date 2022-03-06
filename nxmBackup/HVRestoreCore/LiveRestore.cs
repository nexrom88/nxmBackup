using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nxmBackup.MFUserMode;
using System.Threading;
using ConfigHandler;
using System.Windows;
using nxmBackup.SubGUIs;
using System.Management;
using Common;

namespace HVRestoreCore
{
    public class LiveRestore
    {
        private bool useEncryption;
        private byte[] aesKey;
        private bool usingDedupe;

        //set to true when stop is requested
        public bool StopRequest { get; set; }

        public lrState State { get; set; }

        public LiveRestore(bool useEncryption, byte[] aesKey, bool usingDedupe)
        {
            this.useEncryption = useEncryption;
            this.aesKey = aesKey;
            this.usingDedupe = usingDedupe;
            State = lrState.initializing;
        }


        public void performLiveRestore(string basePath, string vmName, string instanceID, bool wpfMode)
        {
            //get full backup chain
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backupChain = ConfigHandler.BackupConfigHandler.readChain(basePath, false);

            //look for the desired instanceid
            ConfigHandler.BackupConfigHandler.BackupInfo targetBackup = getBackup(backupChain, instanceID);

            //target backup found?
            if (targetBackup.instanceID != instanceID )
            {
                DBQueries.addLog("LR: target backup not found", Environment.StackTrace, null);
                return; //not found, no restore
            }

            //build restore chain, top down (full backup is last element)
            List<ConfigHandler.BackupConfigHandler.BackupInfo> restoreChain = new List<ConfigHandler.BackupConfigHandler.BackupInfo>();

            //add target chain element first
            restoreChain.Add(targetBackup);

            //look for backup element until full backup found
            while (restoreChain[restoreChain.Count - 1].type != "full")
            {
                ConfigHandler.BackupConfigHandler.BackupInfo restoreElement = getBackup(backupChain, restoreChain[restoreChain.Count - 1].parentInstanceID);

                //just first backup can be "lb", ignore it here
                if (restoreElement.type == "lb")
                {
                    continue;
                }

                //valid element found?
                if (restoreElement.instanceID != restoreChain[restoreChain.Count - 1].parentInstanceID)
                {
                    //element is not valid, chain is broken, cancel restore
                    return;
                }
                else
                {
                    //valid element found
                    restoreChain.Add(restoreElement);
                }
            }

            //remove all lb backups except when lb backup is first element
            for (int i = 1; i < restoreChain.Count; i++)
            {
                if (restoreChain[i].type == "lb")
                {
                    restoreChain.RemoveAt(i);
                    i--;
                }
            }

            //get hdd files from backup chain
            BackupConfigHandler.LRBackupChains hddFiles = BackupConfigHandler.getHDDFilesFromChainForLR(restoreChain, basePath);

            string backupBasePath = System.IO.Path.Combine(basePath, restoreChain[restoreChain.Count -1].uuid + ".nxm");

            MountHandler mountHandler = new MountHandler(MountHandler.RestoreMode.lr, this.useEncryption, this.aesKey, this.usingDedupe);

            Thread mountThread = new Thread(() => mountHandler.startMfHandlingForLR(hddFiles, backupBasePath, vmName));
            mountThread.Start();

            //wait for mounting process
            while (mountHandler.mountState == MountHandler.ProcessState.pending)
            {
                Thread.Sleep(200);
            }

            //error while mounting
            if (mountHandler.mountState == MountHandler.ProcessState.error)
            {
                State = lrState.error;
                //MessageBox.Show("Backup konnte nicht eingehängt werden", "Restore Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                mountThread.Abort();
                mountHandler.stopMfHandling();
                return;
            }

            State = lrState.running;

            if (wpfMode)
            {
                //start restore window
                LRWindow h = new LRWindow();
                h.ShowDialog();
            }
            else
            {
                //wait for stop request
                while (!StopRequest)
                {
                    Thread.Sleep(100);
                }
            }

            State = lrState.stopped;

            //turns off VM
            powerOffVM(mountHandler.LrVMID);

            mountThread.Abort();
            mountHandler.stopMfHandling();

            deleteVM(mountHandler.LrVMID);

        }


        //turns off vm
        private void powerOffVM(string vmId)
        {
            ManagementScope scope = new ManagementScope(@"root\virtualization\v2");

            //power off vm
            using (ManagementObject vm = Common.WmiUtilities.GetVirtualMachine(vmId, scope))
            using (ManagementBaseObject inParams = vm.GetMethodParameters("RequestStateChange"))
            {
                inParams["RequestedState"] = 3; //power off
                ManagementBaseObject outParams = vm.InvokeMethod("RequestStateChange", inParams, null);
                Common.WmiUtilities.ValidateOutput(outParams, scope, false, false);
            }

        }

        private void deleteVM(string vmId)
        {
            ManagementScope scope = new ManagementScope(@"root\virtualization\v2");

            //delete vm
            using (ManagementObject vm = Common.WmiUtilities.GetVirtualMachine(vmId, scope))
            using (ManagementObject service = Common.WmiUtilities.GetVirtualSystemManagementService(scope))
            using (ManagementBaseObject inParams = service.GetMethodParameters("DestroySystem"))
            {
                inParams["AffectedSystem"] = vm;
                ManagementBaseObject outParams = service.InvokeMethod("DestroySystem", inParams, null);
                Common.WmiUtilities.ValidateOutput(outParams, scope, false, false);
            }
        }

        private ConfigHandler.BackupConfigHandler.BackupInfo getBackup(List<ConfigHandler.BackupConfigHandler.BackupInfo> backupChain, string instanceID)
        {
            foreach (ConfigHandler.BackupConfigHandler.BackupInfo backup in backupChain)
            {
                if (backup.instanceID == instanceID)
                {
                    return backup;
                }
            }

            //backup not found, return empty backup element
            return new ConfigHandler.BackupConfigHandler.BackupInfo();
        }

        public enum lrState
        {
            initializing, running, error, stopped
        }
    }
}
