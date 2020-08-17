using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Common;
using System.Windows;
using nxmBackup.SubGUIs;
using nxmBackup.MFUserMode;

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



            //get hdd files from backup chain
            string[] hddFiles = getHDDFilesFromChain(restoreChain, basePath, selectedHDD);

            MountHandler mountHandler = new MountHandler();

            MountHandler.mountState mountState = MountHandler.mountState.pending;
            Thread mountThread = new Thread(() => mountHandler.startMfHandling(hddFiles, ref mountState));
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
            FLRWindow h = new FLRWindow();
            h.VhdPath = mountHandler.MountFile;
            h.ShowDialog();

            mountThread.Abort();
            mountHandler.stopMfHandling();


        }


        //builds an array of hdd files from a given backup chain
        private string[] getHDDFilesFromChain(List<ConfigHandler.BackupConfigHandler.BackupInfo> restoreChain, string basePath, string userSelectedHDD)
        {
            string[] retVal = new string[restoreChain.Count];
            string targetHDD = "";

            //iterate through all backups within chain in reverse to read full backup first
            for (int i = restoreChain.Count - 1; i >= 0; i--)
            {
                if (restoreChain[i].type == "full")
                {
                    //did user select an HDD?
                    if (userSelectedHDD != null)
                    {
                        retVal[i] = userSelectedHDD;
                        targetHDD = System.IO.Path.GetFileName(userSelectedHDD);
                    }
                    else //no user-selected HDD
                    {
                        //get all vhdx files
                        string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\" + "Virtual Hard Disks");
                        string[] entries = System.IO.Directory.GetFiles(vmBasePath, "*.vhdx");
                        retVal[i] = entries[0]; //take first found file. OK here because otherwise user would have chosen one
                        targetHDD = System.IO.Path.GetFileName(entries[0]);
                    }
                } else if (restoreChain[i].type == "rct")
                {
                    string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\");
                    retVal[i] = System.IO.Path.Combine(vmBasePath, targetHDD + ".cb");
                } else if (restoreChain[i].type == "lb")
                {
                    string vmBasePath = System.IO.Path.Combine(basePath, restoreChain[i].uuid + ".nxm\\");
                    retVal[i] = System.IO.Path.Combine(vmBasePath, targetHDD + ".lb");
                }
            }

            return retVal;

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


    }
}
