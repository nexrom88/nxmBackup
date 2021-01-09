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

namespace RestoreHelper
{
    public class LiveRestore
    {
        public void performLiveRestore(string basePath, string vmName, string instanceID)
        {
         

            //get full backup chain
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backupChain = ConfigHandler.BackupConfigHandler.readChain(basePath);

            //look for the desired instanceid
            ConfigHandler.BackupConfigHandler.BackupInfo targetBackup = getBackup(backupChain, instanceID);

            //target backup found?
            if (targetBackup.instanceID != instanceID )
            {

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
            string[] hddFiles = BackupConfigHandler.getHDDFilesFromChain(restoreChain, basePath, null);

            string backupBasePath = System.IO.Path.Combine(basePath, restoreChain[restoreChain.Count -1].uuid + ".nxm");

            MountHandler mountHandler = new MountHandler(MountHandler.RestoreMode.lr);

            MountHandler.mountState mountState = MountHandler.mountState.pending;
            Thread mountThread = new Thread(() => mountHandler.startMfHandlingForLR(hddFiles, backupBasePath, ref mountState));
            mountThread.Start();

            //wait for mounting process
            while (mountState == MountHandler.mountState.pending)
            {
                Thread.Sleep(200);
            }

            //error while mounting
            if (mountState == MountHandler.mountState.error)
            {
                MessageBox.Show("Backup konnte nicht eingehängt werden", "Restore Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            //start restore window
            LRWindow h = new LRWindow();
            h.ShowDialog();

            //turns off and deletes vm
            deleteVM(mountHandler.LrVMID);

            mountThread.Abort();
            mountHandler.stopMfHandling();

        }


        //turns off and deletes vm
        private void deleteVM(string vmId)
        {
            ManagementScope scope = new ManagementScope(@"root\virtualization\v2");

            //power off vm
            bool succeeded = false;
            using (ManagementObject vm = Common.WmiUtilities.GetVirtualMachine(vmId, scope))
            using (ManagementBaseObject inParams = vm.GetMethodParameters("RequestStateChange"))
            {
                inParams["RequestedState"] = 3; //power off
                ManagementBaseObject outParams = vm.InvokeMethod("RequestStateChange", inParams, null);
                succeeded = Common.WmiUtilities.ValidateOutput(outParams, scope, false, false);
            }

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
    }
}
