using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperVBackupRCT
{
    public class RestoreHandler
    {

        public event Common.Job.newEventDelegate newEvent;

        //performs a full restore process
        public void performFullRestoreProcess(string basePath, string destPath, string instanceID)
        {
            raiseNewEvent("Analysiere Backups...", false, false);

            //get full backup chain
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backupChain = ConfigHandler.BackupConfigHandler.readChain(basePath);
            
            //look for the desired instanceid
            ConfigHandler.BackupConfigHandler.BackupInfo targetBackup = getBackup(backupChain, instanceID);

            //target backup found?
            if (targetBackup.instanceID != instanceID)
            {
                raiseNewEvent("fehlgeschlagen", true, false);
                raiseNewEvent("Ziel-Backup kann nicht gefunden werden", false, false);
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

            raiseNewEvent("erfolgreich", true, false);

            //copy full backup to destination and get vhdx files
            List<string> hddFiles = transferSnapshot(System.IO.Path.Combine(basePath, restoreChain[restoreChain.Count - 1].uuid + ".nxm"), destPath, false);

            //remove full backup from restore chain
            restoreChain.RemoveAt(restoreChain.Count - 1);

            //iterate through all incremental backups
            DiffHandler diffRestore = new DiffHandler(this.newEvent);
            while (restoreChain.Count > 0)
            {
                ConfigHandler.BackupConfigHandler.BackupInfo currentBackup = restoreChain[restoreChain.Count - 1];

                //open diff file
                Common.ZipArchive archive = new Common.ZipArchive(System.IO.Path.Combine(basePath, currentBackup.uuid + ".nxm"), null);
                archive.open(System.IO.Compression.ZipArchiveMode.Read);

                //iterate through all vhds
                foreach (string hddFile in hddFiles)
                {
                    System.IO.Stream diffStream = archive.openAndGetFileStream(System.IO.Path.GetFileName(hddFile) + ".cb");

                    //merge the files
                    diffRestore.merge(diffStream, hddFile);
                    diffStream.Close();
                }
                archive.close();

                //remove current diff
                restoreChain.RemoveAt(restoreChain.Count - 1);
            }
            raiseNewEvent("Wiederherstellung erfolgreich", false, false);

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
            Common.ZipArchive archive = new Common.ZipArchive(archivePath, this.newEvent);
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

        //builds a EventProperties object and raises the "newEvent" event
        public void raiseNewEvent(string text, bool setDone, bool isUpdate)
        {
            Common.EventProperties props = new Common.EventProperties();
            props.text = text;
            props.setDone = setDone;
            props.isUpdate = isUpdate;
            if (this.newEvent != null)
            {
                this.newEvent(props);
            }
        }

    }
}
