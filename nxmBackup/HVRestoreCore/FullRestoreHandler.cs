using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using Common;
using nxmBackup.Language;

namespace HVRestoreCore
{
    public class FullRestoreHandler
    {
        private Common.EventHandler eventHandler;
        private const int NO_RELATED_EVENT = -1;
        private bool useEncryption;
        private byte[] aesKey;
        private bool usingDedupe;
        private DateTime startTime;
        public bool StopRequest {
            set
            {
                stopRequestWrapper.value = value;
            }
            get
            {
                return stopRequestWrapper.value;
            }
        }
        private Common.StopRequestWrapper stopRequestWrapper = new Common.StopRequestWrapper();

        public FullRestoreHandler(Common.EventHandler eventHandler, bool useEncryption, byte[] aesKey, bool usingDedupe)
        {
            this.eventHandler = eventHandler;
            this.useEncryption = useEncryption;
            this.aesKey = aesKey;
            this.usingDedupe = usingDedupe;
        }

        //performs a full restore process
        public void performFullRestoreProcess(string basePath, string destPath, string vmName, string instanceID, bool importToHyperV, UInt64 lbTimeLimit)
        {
            int relatedEventId = -1;
            if (this.eventHandler != null)
            {
                this.startTime = DateTime.Now;
                relatedEventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("analyzing_backups"), false, false, NO_RELATED_EVENT, Common.EventStatus.inProgress);
            }

            //get full backup chain
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backupChain = ConfigHandler.BackupConfigHandler.readChain(basePath);
            
            //look for the desired instanceid
            ConfigHandler.BackupConfigHandler.BackupInfo targetBackup = getBackup(backupChain, instanceID);

            //target backup found?
            if (targetBackup.instanceID != instanceID && this.eventHandler != null)
            {
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("failed"), true, false, relatedEventId, Common.EventStatus.error);
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("target_backup_not_found"), false, false, NO_RELATED_EVENT, Common.EventStatus.error);
                closeExecution(false);
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
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("failed"), true, false, relatedEventId, Common.EventStatus.error);
                    closeExecution(false);
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
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, relatedEventId, Common.EventStatus.successful);
            }

            //copy full backup to destination and get vhdx files
            List<string> hddFiles = transferSnapshot(System.IO.Path.Combine(basePath, restoreChain[restoreChain.Count - 1].uuid + ".nxm"), destPath, false);

            //restore not possible
            if (hddFiles == null)
            {
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("backup_not_writeable"), false, false, NO_RELATED_EVENT, Common.EventStatus.error);
                closeExecution(false);
                return;
            }

            //remove full backup from restore chain
            restoreChain.RemoveAt(restoreChain.Count - 1);

            //iterate through all incremental backups
            nxmBackup.HVBackupCore.DiffHandler diffRestore = new nxmBackup.HVBackupCore.DiffHandler(this.eventHandler, this.stopRequestWrapper);
            while (restoreChain.Count > 0 && !this.stopRequestWrapper.value)
            {
                ConfigHandler.BackupConfigHandler.BackupInfo currentBackup = restoreChain[restoreChain.Count - 1];

                if (currentBackup.type == "rct") //rct backup?
                {
                    //open diff file
                    Common.IArchive archive;


                    archive = new Common.LZ4Archive(System.IO.Path.Combine(basePath, currentBackup.uuid + ".nxm"), null, this.useEncryption, this.aesKey, this.usingDedupe, this.stopRequestWrapper);


                    archive.open(System.IO.Compression.ZipArchiveMode.Read);

                    //iterate through all vhds
                    foreach (string hddFile in hddFiles)
                    {
                        System.IO.Stream diffStream = archive.openAndGetFileStream(System.IO.Path.GetFileName(hddFile) + ".cb");

                        //merge the files
                        if (!diffRestore.merge_rct((BlockCompression.LZ4BlockStream)diffStream, hddFile))
                        {
                            this.eventHandler.raiseNewEvent(LanguageHandler.getString("merge_failed"), true, false, NO_RELATED_EVENT, Common.EventStatus.error);
                            diffStream.Close();
                            closeExecution(false);
                            return;
                        }
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
                        diffRestore.merge_lb(lbStream, hddFile, lbTimeLimit);
                    }

                }

                //remove current diff
                restoreChain.RemoveAt(restoreChain.Count - 1);
            }

            //has the restored VM to be imported into HyperV?
            if (importToHyperV && !stopRequestWrapper.value)
            {
                relatedEventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("registering"), false, false, NO_RELATED_EVENT, Common.EventStatus.inProgress);

                //look for vmcx file
                string[] configFiles = System.IO.Directory.GetFiles(destPath + "\\Virtual Machines", "*.vmcx", SearchOption.AllDirectories);
                
                //there may just be exactly one config file otherwise cancel import
                if (configFiles.Length != 1)
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("failed"), true, false, relatedEventId, Common.EventStatus.warning);
                    closeExecution(false);
                    return;
                }
                else
                {
                    //import vm
                    try
                    {
                        VMImporter.importVM(configFiles[0], destPath, true, vmName);
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, relatedEventId, Common.EventStatus.successful);
                    }catch(Exception ex)
                    {
                        Common.DBQueries.addLog("importVM failed", Environment.StackTrace, ex);
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("failed"), true, false, relatedEventId, Common.EventStatus.warning);
                        closeExecution(false);
                        return;
                    }
                }


            }

            if (this.eventHandler != null)
            {
                //finished "normally"
                if (!this.stopRequestWrapper.value)
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("restore_successful"), false, false, NO_RELATED_EVENT, Common.EventStatus.successful);
                    
                }
                else
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("restore_canceled"), false, false, NO_RELATED_EVENT, Common.EventStatus.error);
                    closeExecution(false);
                    return;
                }
            }

            closeExecution(true);
            return;

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

           
            archive = new Common.LZ4Archive(archivePath, this.eventHandler, this.useEncryption, this.aesKey, this.usingDedupe, this.stopRequestWrapper);

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
                if (!archive.getFile(entry, fileDestination))
                {
                    //error on writing full snapshot
                    archive.close();
                    return null;
                }

                //add to return list if vhdx
                if (fileDestination.EndsWith(".vhdx"))
                {
                    hddFiles.Add(fileDestination);
                }
            }

            archive.close();
            return hddFiles;
        }

        //closes the current jobexecution
        private void closeExecution(bool successful)
        {
            if (this.eventHandler == null)
            {
                return;
            }

            JobExecutionProperties props = new JobExecutionProperties();
            props.successful  = successful;
            props.endStamp = DateTime.Now;
            props.startStamp = this.startTime;

            Common.DBQueries.closeJobExecution(props, this.eventHandler.ExecutionId.ToString());
        }

    }

    

    
}
