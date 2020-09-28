﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace RestoreHelper
{
    public class FullRestoreHandler
    {
        private Common.EventHandler eventHandler;
        private const int NO_RELATED_EVENT = -1;

        public FullRestoreHandler(Common.EventHandler eventHandler)
        {
            this.eventHandler = eventHandler;
        }

        //performs a full restore process
        public void performFullRestoreProcess(string basePath, string destPath, string instanceID)
        {
            int relatedEventId = -1;
            if (this.eventHandler != null)
            {
                relatedEventId = this.eventHandler.raiseNewEvent("Analysiere Backups...", false, false, NO_RELATED_EVENT, Common.EventStatus.inProgress);
            }

            //get full backup chain
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backupChain = ConfigHandler.BackupConfigHandler.readChain(basePath);
            
            //look for the desired instanceid
            ConfigHandler.BackupConfigHandler.BackupInfo targetBackup = getBackup(backupChain, instanceID);

            //target backup found?
            if (targetBackup.instanceID != instanceID && this.eventHandler != null)
            {
                this.eventHandler.raiseNewEvent("fehlgeschlagen", true, false, relatedEventId, Common.EventStatus.error);
                this.eventHandler.raiseNewEvent("Ziel-Backup kann nicht gefunden werden", false, false, NO_RELATED_EVENT, Common.EventStatus.error);
                return; //not found, no restore
            }

            //build restore chain, top down (full backup is last element)
            List<ConfigHandler.BackupConfigHandler.BackupInfo> restoreChain = new List<ConfigHandler.BackupConfigHandler.BackupInfo>();

            //add target chain element first
            restoreChain.Add(targetBackup);

            //look for backup element until full backup found
            while (restoreChain[restoreChain.Count -1].type != "full")
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

            if (this.eventHandler != null)
            {
                this.eventHandler.raiseNewEvent("erfolgreich", true, false, relatedEventId, Common.EventStatus.successful);
            }

            //copy full backup to destination and get vhdx files
            List<string> hddFiles = transferSnapshot(System.IO.Path.Combine(basePath, restoreChain[restoreChain.Count - 1].uuid + ".nxm"), destPath, false);

            //remove full backup from restore chain
            restoreChain.RemoveAt(restoreChain.Count - 1);

            //iterate through all incremental backups
            Common.DiffHandler diffRestore = new Common.DiffHandler(this.eventHandler);
            while (restoreChain.Count > 0)
            {
                ConfigHandler.BackupConfigHandler.BackupInfo currentBackup = restoreChain[restoreChain.Count - 1];

                if (currentBackup.type == "rct") //rct backup?
                {
                    //open diff file
                    Common.IArchive archive;


                    archive = new Common.LZ4Archive(System.IO.Path.Combine(basePath, currentBackup.uuid + ".nxm"), null);


                    archive.open(System.IO.Compression.ZipArchiveMode.Read);

                    //iterate through all vhds
                    foreach (string hddFile in hddFiles)
                    {
                        System.IO.Stream diffStream = archive.openAndGetFileStream(System.IO.Path.GetFileName(hddFile) + ".cb");

                        //merge the files
                        diffRestore.merge_rct((BlockCompression.LZ4BlockStream)diffStream, hddFile);
                        diffStream.Close();
                    }
                    archive.close();

                }else if (currentBackup.type == "lb") //lb backup
                {
                    //iterate through all vhds
                    foreach (string hddFile in hddFiles)
                    {
                        FileStream lbStream = new FileStream(System.IO.Path.Combine(basePath, currentBackup.uuid + ".nxm\\" + System.IO.Path.GetFileName(hddFile) + ".lb"), FileMode.Open, FileAccess.Read);

                        //merge the files
                        diffRestore.merge_lb(lbStream, hddFile);
                    }

                }

                //remove current diff
                restoreChain.RemoveAt(restoreChain.Count - 1);
            }

            if (this.eventHandler != null)
            {
                this.eventHandler.raiseNewEvent("Wiederherstellung erfolgreich", false, false, NO_RELATED_EVENT, Common.EventStatus.successful);
            }

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

        //copys a file (full backup vhd) from an archive to destination and returns all vhdx files
        public List<string> transferSnapshot(string archivePath, string destination, bool justHardDrives)
        {
            List<string> hddFiles = new List<string>();

            Common.IArchive archive;

           
            archive = new Common.LZ4Archive(archivePath, this.eventHandler);
             

            archive.open(System.IO.Compression.ZipArchiveMode.Read);

            //get all archive entries
            List<string> entries = archive.listEntries();

            //iterate through all entries
            foreach (string entry in entries)
            {

                //just vhds?
                if (justHardDrives && !entry.EndsWith(".vhdx"))
                {
                    continue;
                }

                //extract folder from archive folder
                string archiveFolder = entry.Substring(0, entry.LastIndexOf("/"));

                //build complete dest folder
                string fileDestination = System.IO.Path.Combine(destination, archiveFolder);
                string[] splitter = entry.Split("/".ToCharArray());
                fileDestination = System.IO.Path.Combine(fileDestination, splitter[splitter.Length - 1]);

                //start the transfer
                archive.getFile(entry, fileDestination);

                //add to return list if vhdx
                if (fileDestination.EndsWith(".vhdx"))
                {
                    hddFiles.Add(fileDestination);
                }
            }

            archive.close();
            return hddFiles;
        }


    }
}
