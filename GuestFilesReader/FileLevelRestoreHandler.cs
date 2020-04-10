using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Common;

namespace GuestFilesReader
{
    public class FileLevelRestoreHandler
    {
        private const int NO_RELATED_EVENT = -1;
        private Common.EventHandler eventHandler;


        public FileLevelRestoreHandler(Common.EventHandler eventHandler)
        {
            this.eventHandler = eventHandler;
        }

        //performs a guest files restore
        public void performGuestFilesRestore(string basePath, string instanceID, ConfigHandler.Compression compressionType)
        {
            int relatedEventId;
            relatedEventId = this.eventHandler.raiseNewEvent("Analysiere Backups...", false, false, NO_RELATED_EVENT, Common.EventStatus.inProgress);

            //get full backup chain
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backupChain = ConfigHandler.BackupConfigHandler.readChain(basePath);

            //look for the desired instanceid
            ConfigHandler.BackupConfigHandler.BackupInfo targetBackup = getBackup(backupChain, instanceID);

            //target backup found?
            if (targetBackup.instanceID != instanceID)
            {
                this.eventHandler.raiseNewEvent("fehlgeschlagen", true, false, relatedEventId, Common.EventStatus.error);
                this.eventHandler.raiseNewEvent("Ziel-Backup kann nicht gefunden werden", false, false, NO_RELATED_EVENT, EventStatus.error);
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

            this.eventHandler.raiseNewEvent("erfolgreich", true, false, relatedEventId, EventStatus.successful);

            string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[restoreChain.Count - 1].uuid + ".nxm\\" + "Virtual Hard Disks");


            //get vhd files            
            string[] entries = System.IO.Directory.GetFiles(vmBasePath, "*.vhdx");

            //todo: just use first hdd to mount
            MFUserMode.MountHandler mountHandler = new MFUserMode.MountHandler();

            Thread mountThread = new Thread(() => mountHandler.startMountProcess(System.IO.Path.Combine(vmBasePath, entries[0]), "e:\\target\\mount.vhdx"));
            mountThread.Start();

            Thread.Sleep(1000);

            //start restore window
            FLRWindow h = new FLRWindow();
            h.VhdPath = "e:\\target\\mount.vhdx";
            h.ShowDialog();

            mountThread.Abort();
            mountHandler.stopMountProcess();


        }

        //gets a backup by the given instanceID
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
