﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Common;
using System.Windows;

namespace RestoreHelper
{
    public class FileLevelRestoreHandler
    {
        private const int NO_RELATED_EVENT = -1;


        //performs a guest files restore
        public void performGuestFilesRestore(string basePath, string instanceID)
        {
            //get full backup chain
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backupChain = ConfigHandler.BackupConfigHandler.readChain(basePath);

            //look for the desired instanceid
            ConfigHandler.BackupConfigHandler.BackupInfo targetBackup = getBackup(backupChain, instanceID);

            //target backup found?
            if (targetBackup.instanceID != instanceID)
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


            //get hdd files from backup chain
            string[] hddFiles = getHDDFilesFromChain(restoreChain, basePath);

            //todo: just use first hdd to mount
            MFUserMode.MountHandler mountHandler = new MFUserMode.MountHandler();

            string mountPath = "f:\\mount.vhdx";
            MFUserMode.MountHandler.mountState mountState = MFUserMode.MountHandler.mountState.pending;
            Thread mountThread = new Thread(() => mountHandler.startMfHandling(hddFiles, mountPath, ref mountState));
            mountThread.Start();

            //wait for mounting process
            while (mountState == MFUserMode.MountHandler.mountState.pending)
            {
                Thread.Sleep(200);
            }

            //error while mounting
            if (mountState == MFUserMode.MountHandler.mountState.error)
            {
                MessageBox.Show("Backup konnte nicht eingehängt werden", "Restore Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //start restore window
            FLRWindow h = new FLRWindow();
            h.VhdPath = mountPath;
            h.ShowDialog();

            mountThread.Abort();
            mountHandler.stopMfHandling();


        }

        //builds an array of hdd files from a given backup chain
        private string[] getHDDFilesFromChain(List<ConfigHandler.BackupConfigHandler.BackupInfo> restoreChain, string basePath)
        {
            string[] retVal = new string[restoreChain.Count];
            string targetHDD = ""; //todo: make target hdd selectable by user

            //iterate through all backups within chain in reverse to read full backup first
            for (int i = restoreChain.Count - 1; i >= 0; i--)
            {
                if (restoreChain[i].type == "full")
                {
                    //get all vhdx files
                    string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\" + "Virtual Hard Disks");
                    string[] entries = System.IO.Directory.GetFiles(vmBasePath, "*.vhdx");
                    retVal[i] = entries[0];
                    targetHDD = System.IO.Path.GetFileName(entries[0]); //todo: make target hdd selectable by user
                } else if (restoreChain[i].type == "rct")
                {
                    string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\");
                    retVal[i] = System.IO.Path.Combine(vmBasePath, targetHDD + ".cb");
                }
            }

            return retVal;

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
