﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Common;
using System.Windows;
using nxmBackup.SubGUIs;
using nxmBackup.MFUserMode;
using ConfigHandler;

namespace HVRestoreCore
{
    public class FileLevelRestoreHandler
    {
        private const int NO_RELATED_EVENT = -1;
        private bool useEncryption;
        private byte[] aesKey;
        public bool StopRequest { set; get; }
        public flrState State { get; set; }


        public FileLevelRestoreHandler(bool useEncryption, byte[] aesKey)
        {
            this.useEncryption = useEncryption;
            this.aesKey = aesKey;

            //init state property
            flrState newState = new flrState();
            newState.hddsToSelect = null;
            newState.type = flrStateType.initializing;

            State = newState;
        }


        //performs a guest files restore
        public void performGuestFilesRestore(string basePath, string instanceID, bool windowMode, string guiSelectedHDD)
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

            //remove all lb backups except when lb backup is first element
            for(int i = 1; i < restoreChain.Count; i++)
            {
                if (restoreChain[i].type == "lb")
                {
                    restoreChain.RemoveAt(i);
                    i--;
                }
            }


            //get all available vhdx files for hdd picker
            string[] baseHDDFiles = getBaseHDDFilesFromChain(restoreChain, basePath);

            //show hdd picker window when more than one hdd
            string selectedHDD = null;
            if (windowMode)
            {
                if (baseHDDFiles.Length > 1)
                {
                    HDDPickerWindow pickerWindow = new HDDPickerWindow();
                    pickerWindow.BaseHDDs = baseHDDFiles;
                    pickerWindow.ShowDialog();
                    selectedHDD = pickerWindow.UserPickedHDD;

                    //no hdd selected -> cancel restore
                    if (selectedHDD == null)
                    {
                        return;
                    }
                }
            }
            else
            {
                //web GUI mode, build return struct if more than one hdd
                if (baseHDDFiles.Length > 1)
                {
                    if (guiSelectedHDD == "")
                    {
                        flrState newState = new flrState();
                        newState.type = flrStateType.waitingForHDDSelect;
                        newState.hddsToSelect = baseHDDFiles;
                        State = newState;
                        return;
                    }
                    else
                    {
                        selectedHDD = guiSelectedHDD;
                    }
                }
            }

            //if lb is first element, show date picker
            //if (restoreChain[0].type == "lb") {
            //    string selectedHDDFileName = System.IO.Path.GetFileName(selectedHDD);
            //    LBDatePickerWindow datePickerWindow = new LBDatePickerWindow();
            //    datePickerWindow.TargetBackup = restoreChain[0];
            //    datePickerWindow.TargetHDD = selectedHDDFileName;
            //    datePickerWindow.BasePath = basePath;
            //    datePickerWindow.ShowDialog();
            //}


            //get hdd files from backup chain
            string[] hddFiles = BackupConfigHandler.getHDDFilesFromChain(restoreChain, basePath, selectedHDD);

            MountHandler mountHandler = new MountHandler(MountHandler.RestoreMode.flr, this.useEncryption, this.aesKey);

            MountHandler.ProcessState mountState = MountHandler.ProcessState.pending;
            Thread mountThread = new Thread(() => mountHandler.startMfHandlingForFLR(hddFiles, ref mountState));
            mountThread.Start();

            //wait for mounting process
            while (mountState == MountHandler.ProcessState.pending)
            {
                Thread.Sleep(200);
            }

            //error while mounting
            if (mountState == MountHandler.ProcessState.error)
            {
                flrState newState = new flrState();
                newState.type = flrStateType.error;
                State = newState;
                return;
            }

            //wait for exit
            if (windowMode)
            {
                //start restore window
                FLRWindow h = new FLRWindow();
                h.VhdPath = mountHandler.MountFile;
                h.ShowDialog();
            }
            else
            {
                while (!StopRequest)
                {
                    Thread.Sleep(200);
                }
            }

            mountThread.Abort();
            mountHandler.stopMfHandling();


        }


        

        //builds an array of available vhdx files from a given backup chain
        private string[] getBaseHDDFilesFromChain(List<ConfigHandler.BackupConfigHandler.BackupInfo> restoreChain, string basePath)
        {
            //iterate through all backups within chain in reverse to read full backup first
            for (int i = restoreChain.Count - 1; i >= 0; i--)
            {
                if (restoreChain[i].type == "full")
                {
                    //get all vhdx files
                    string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\" + "Virtual Hard Disks");
                    string[] entries = System.IO.Directory.GetFiles(vmBasePath, "*.vhdx");
                    return entries;
                }
            }

            return null;

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

        public struct flrState
        {
            public flrStateType type;
            public string[] hddsToSelect;
        }

        public enum flrStateType
        {
            initializing, running, stopped, waitingForHDDSelect, error
        }

    }
}
