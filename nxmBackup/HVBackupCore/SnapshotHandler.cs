using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using Common;
using nxmBackup.Language;


namespace nxmBackup.HVBackupCore
{

    public class SnapshotHandler
    {
        private const UInt16 SnapshotTypeRecovery = 32768;
        private const UInt16 SnapshotTypeFull = 2;
        private JobVM vm;
        private int executionId;
        private const int NO_RELATED_EVENT = -1;
        private Common.EventHandler eventHandler;
        private bool useEncryption;
        private byte[] aesKey;
        private bool usingDedupe;
        public StopRequestWrapper StopRequestWrapper { get; set; }

        public const string USER_SNAPSHOT_TYPE = "Microsoft:Hyper-V:Snapshot:Realized";
        public const string RECOVERY_SNAPSHOT_TYPE = "Microsoft:Hyper-V:Snapshot:Recovery";

        public SnapshotHandler(JobVM vm, int executionId, bool useEncryption, byte[] aesKey, bool usingDedupe, StopRequestWrapper stopRequestWrapper)
        {
            this.useEncryption = useEncryption;
            this.aesKey = aesKey;
            this.vm = vm;
            this.executionId = executionId;
            this.eventHandler = new Common.EventHandler(vm, executionId);
            this.usingDedupe = usingDedupe;
            this.StopRequestWrapper = stopRequestWrapper;
            if (this.StopRequestWrapper == null)
            {
                this.StopRequestWrapper = new StopRequestWrapper();
            }
        }

        //performs a full backup chain
        public TransferDetails performFullBackupProcess(ConsistencyLevel cLevel, Boolean allowSnapshotFallback, bool incremental, ConfigHandler.OneJob job, ManagementObject snapshot = null)
        {
            TransferDetails retVal = new TransferDetails();

            string destination = job.TargetPath;
            
            //when no snapshot obj is given then take one
            if (snapshot == null)
            {
                snapshot = createSnapshot(cLevel, allowSnapshotFallback);
            }

            ManagementObject refP = null;
            //error occured while taking snapshot?
            if (snapshot == null)
            {
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("backup_failed") , false, false, NO_RELATED_EVENT, EventStatus.error);
                retVal.successful = false;
                return retVal;
            }

            //add job name and vm name to destination
            destination = System.IO.Path.Combine(destination, job.Name + "\\" + this.vm.vmID);

            try
            {
                //create folder if it does not exist
                System.IO.Directory.CreateDirectory(destination);
            }catch(Exception ex)
            {
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("destination_failed"), false, false, NO_RELATED_EVENT, EventStatus.error);
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("backup_failed"), false, false, NO_RELATED_EVENT, EventStatus.error);
                DBQueries.addLog("error on creating folder", Environment.StackTrace, ex);

                //delete previously created snapshot
                ManagementObject refPoint = convertToReferencePoint(snapshot, false);
                removeReferencePoint(refPoint);

                retVal.successful = false;
                return retVal;
            }

            List<ConfigHandler.BackupConfigHandler.BackupInfo> chain = ConfigHandler.BackupConfigHandler.readChain(destination);
            if (incremental) //incremental backup? get latest reference point
            {
                if (chain == null || chain.Count == 0) //first backup must be full backup
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("incremental_failed"), false, false, NO_RELATED_EVENT, EventStatus.info);
                    refP = null; //incremental backup not possible
                }
                else if (getBlockSize(chain) >= job.BlockSize) //block size reached?
                {
                    refP = null; //incremental backup not possible
                }
                else
                {
                    //incremental backup possible, get required reference point
                    string instanceID;
                    if (chain[chain.Count - 1].type != "lb") {
                        instanceID = chain[chain.Count - 1].instanceID;
                    }
                    else //last backup lb backup? take very last backup
                    {
                        instanceID = chain[chain.Count - 2].instanceID;
                    }
                    List<ManagementObject> refPs = getReferencePoints();
                    foreach (ManagementObject mo in refPs)
                    {
                        if (mo["InstanceId"].ToString() == instanceID)
                        {
                            refP = mo;
                        }
                    }
                }
            }

            //export the snapshot
            TransferDetails transferDetails = export(destination, snapshot, refP, job);

            if (!transferDetails.successful && transferDetails.retryFullBackupOnFail)
            {
                //backup failed, but retry by using full backup
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("retry_full"), false, false, NO_RELATED_EVENT, EventStatus.info);
                return performFullBackupProcess(cLevel, allowSnapshotFallback, false, job, snapshot);
            }

            //read current backup chain for further processing
            chain = ConfigHandler.BackupConfigHandler.readChain(destination);

            //if full backup, delete unnecessary reference points
            if (refP == null && chain != null && chain.Count > 0)
            {
                int eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("remove_old_rps"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);
                
                //remove current (last) full backup

                if (chain[chain.Count - 1].type == "lb") //last backup lb backup? remove very last element
                {
                    chain.RemoveAt(chain.Count - 2);
                }
                else //last backup full backup? remove last element
                {
                    chain.RemoveAt(chain.Count - 1);
                }
                
                
                
                List<ManagementObject> refPs = getReferencePoints();

                //iterate chain
                foreach (ConfigHandler.BackupConfigHandler.BackupInfo backup in chain)
                {
                    //iterate reference points
                    List<ManagementObject> referencePoints = getReferencePoints();
                    foreach (ManagementObject mo in referencePoints)
                    {
                        if (mo["InstanceId"].ToString() == backup.instanceID)
                        {
                            removeReferencePoint(mo);
                        }
                    }
                }
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);
            }

            //read current backup chain for further processing
            chain = ConfigHandler.BackupConfigHandler.readChain(destination);

            //just rotate when stop request is not set
            if (!StopRequestWrapper.value)
            {

                if (chain != null && chain.Count > 0)
                {
                    //check whether max snapshot count is reached, then merge
                    if (job.Rotation.type == RotationType.merge) //RotationType = "merge"
                    {
                        if (job.Rotation.maxElementCount > 0 && chain.Count > job.Rotation.maxElementCount)
                        {
                            mergeOldest(destination, chain);
                        }
                    }
                    else if (job.Rotation.type == RotationType.blockRotation) //RotationType = "blockRotation"
                    {
                        if (job.Rotation.maxElementCount > 0 && getBlockCount(chain) > job.Rotation.maxElementCount + 1)
                        {
                            blockRotate(destination, chain);
                        }
                    }
                }
                else
                {
                    //chain is broken, not readable
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("rotation_failed"), false, false, NO_RELATED_EVENT, EventStatus.error);
                }
            }

            if (transferDetails.successful)
            {
                //backup successful
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("backup_successful"), false, false, NO_RELATED_EVENT, EventStatus.successful);
            }
            else
            {
                //backup failed
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("backup_failed"), false, false, NO_RELATED_EVENT, EventStatus.error);
            }


            return transferDetails;
        }

        //gets the block count for the given chain
        private uint getBlockCount(List<ConfigHandler.BackupConfigHandler.BackupInfo> chain)
        {
            if (chain == null)
            {
                return 0;
            }

            uint blockCount = 0;
            foreach (ConfigHandler.BackupConfigHandler.BackupInfo backup in chain)
            {
                if (backup.type == "full")
                {
                    blockCount++;
                }
            }

            return blockCount;

        }

        //reads the current block size from a given backup chain
        private uint getBlockSize (List<ConfigHandler.BackupConfigHandler.BackupInfo> chain)
        {
            if (chain == null)
            {
                return 0;
            }

            uint blockSize = 0;
            foreach (ConfigHandler.BackupConfigHandler.BackupInfo backup in chain)
            {
                if (backup.type == "full") //full backup found -> reset blockSize Counter
                {
                    blockSize = 1;
                }
                else if (backup.type != "lb") //rct backup found -> increment blockSize counter
                {
                    blockSize++;
                }
            }
            return blockSize;
        }


        //performs a block rotation
        private void blockRotate(string path, List<ConfigHandler.BackupConfigHandler.BackupInfo> chain)
        {
            int eventID = this.eventHandler.raiseNewEvent(LanguageHandler.getString("rotating_block"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            //remove first full backup
            ConfigHandler.BackupConfigHandler.removeBackup(path, chain[0].uuid); //remove from config
            System.IO.Directory.Delete(System.IO.Path.Combine(path, chain[0].uuid + ".nxm"), true); //remove backup file

            //remove rct backups
            for (int i = 1; i < chain.Count; i++)
            {
                //when backuptype type == full => blockrotation completed
                if (chain[i].type == "full")
                {
                    break;
                }
                else
                {
                    //remove rct or lb backup
                    ConfigHandler.BackupConfigHandler.removeBackup(path, chain[i].uuid); //remove from config
                    System.IO.Directory.Delete(System.IO.Path.Combine(path, chain[i].uuid + ".nxm"), true); //remove backup file
                }
            }

            this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventID, EventStatus.successful);
        }

        //merge two backups to keep max snapshot count
        private void mergeOldest(string path, List<ConfigHandler.BackupConfigHandler.BackupInfo> chain)
        {

            try
            {

                //when the first two backups are "full" backups then the first one can just be deleted
                if (chain[0].type == "full" && chain[1].type == "full")
                {
                    ConfigHandler.BackupConfigHandler.removeBackup(path, chain[0].uuid); //remove from config
                    System.IO.Directory.Delete(System.IO.Path.Combine(path, chain[0].uuid + ".nxm"), true); //remove backup file

                    return;
                }

                //Given at this point: first backup is "full", the second one is "rct" or "lb"

                //is second backup within chain "lb" backup?
                if (chain[1].type == "lb")
                {
                    //remove lb backup from config, hdd and chainlist
                    ConfigHandler.BackupConfigHandler.removeBackup(path, chain[1].uuid); //remove from config
                    System.IO.Directory.Delete(System.IO.Path.Combine(path, chain[1].uuid + ".nxm"), true); //remove backup file
                    chain.RemoveAt(1);
                }




                //create staging dir
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(path, "staging"));

                int eventId;
                eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("rotating_1"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);

                HVRestoreCore.FullRestoreHandler restHandler = new HVRestoreCore.FullRestoreHandler(null, this.useEncryption, this.aesKey, this.usingDedupe);

                //perform restore to staging directory (including merge with second backup)
                restHandler.performFullRestoreProcess(path, System.IO.Path.Combine(path, "staging"), "", chain[1].instanceID, false, 0);

                this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);
                eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("rotating_2"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);

                //remove first and second backup from backup chain
                ConfigHandler.BackupConfigHandler.removeBackup(path, chain[0].uuid); //remove from config
                System.IO.Directory.Delete(System.IO.Path.Combine(path, chain[0].uuid + ".nxm"), true); //remove backup file
                ConfigHandler.BackupConfigHandler.removeBackup(path, chain[1].uuid); //remove from config
                System.IO.Directory.Delete(System.IO.Path.Combine(path, chain[1].uuid + ".nxm"), true); //remove backup file

                //create new backup container from merged backups
                Guid g = Guid.NewGuid();
                string guidFolder = g.ToString();
                Common.LZ4Archive backupArchive = new Common.LZ4Archive(System.IO.Path.Combine(path, guidFolder + ".nxm"), null, this.useEncryption, this.aesKey,this.usingDedupe, null);
                backupArchive.create();
                backupArchive.open(System.IO.Compression.ZipArchiveMode.Create);

                this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);
                eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("rotating_3"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);

                //add whole staging directory to the container archive
                backupArchive.addDirectory(System.IO.Path.Combine(path, "staging"));
                backupArchive.close();

                this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);
                eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("rotating_4"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);

                //create entry to backup chain
                bool configWritten = ConfigHandler.BackupConfigHandler.addBackup(path,this.useEncryption, guidFolder, "full", chain[1].instanceID, "", true, this.executionId.ToString());

                if (configWritten)
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);
                }
                else
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("failed"), true, false, eventId, EventStatus.error);
                }
                eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("rotating_5"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);

                //remove reference point
                List<ManagementObject> refPs = getReferencePoints();
                //iterate reference points
                List<ManagementObject> referencePoints = getReferencePoints();
                foreach (ManagementObject mo in referencePoints)
                {
                    if (mo["InstanceId"].ToString() == chain[0].instanceID)
                    {
                        removeReferencePoint(mo);
                    }
                }

                //remove staging folder
                System.IO.Directory.Delete(System.IO.Path.Combine(path, "staging"), true);

                this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);

            }
            catch(Exception ex)
            {
                //old backups could not be deleted, write error message
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("rotation_failed_old"), false, false, NO_RELATED_EVENT, EventStatus.warning);
                Common.DBQueries.addLog("merge failed", Environment.StackTrace, ex);
            }


        }

        //creates the snapshot
        public ManagementObject createSnapshot(ConsistencyLevel cLevel, Boolean allowSnapshotFallback)
        {
            ManagementScope scope = getHyperVManagementScope();
            int eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("initializing_env"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            //check for already existing snapshots (user created ones)
            List<ManagementObject> snapshots = getSnapshots(USER_SNAPSHOT_TYPE);
            
            if (snapshots == null)
            {
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("failed"), true, false, eventId, EventStatus.error);
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("vm_unreachable"), false, false, NO_RELATED_EVENT, EventStatus.error);
                return null;
            }
            
            if (snapshots.Count > 0)
            {
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("failed"), true, false, eventId, EventStatus.error);
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("checkpoints_exist"), false, false, NO_RELATED_EVENT, EventStatus.error);
                return null;
            }

            //check for already existing snapshots (recovery ones)
            snapshots = getSnapshots(RECOVERY_SNAPSHOT_TYPE);
            
            //delete these snapshots
            foreach(ManagementObject snapshot in snapshots)
            {
                ManagementObject refPoint = convertToReferencePoint(snapshot, false);
                removeReferencePoint(refPoint);
            }


            try
            {
                // Get the management service and the VM object.
                using (ManagementObject vm = WmiUtilities.GetVirtualMachine(this.vm.vmID, scope))
                using (ManagementObject service = WmiUtilities.GetVirtualMachineSnapshotService(scope))
                using (ManagementObject settings = WmiUtilities.GetVirtualMachineSnapshotSettings(scope))
                using (ManagementBaseObject inParams = service.GetMethodParameters("CreateSnapshot"))
                {
                    //check vm state
                    UInt16[] vmStateArray = (UInt16[]) vm["OperationalStatus"];
                    if (vmStateArray.Length == 2 && vmStateArray[1] != 0) //32772
                    {
                        //vm in wrong state, do not create snapshot
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("failed"), true, false, eventId, EventStatus.error);
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("vm_not_ready"), false, false, NO_RELATED_EVENT, EventStatus.error);
                        return null;
                    }


                    //set settings
                    settings["ConsistencyLevel"] = cLevel == ConsistencyLevel.ApplicationAware ? 1 : 2;
                    settings["IgnoreNonSnapshottableDisks"] = true;
                    settings["GuestBackupType"] = 1; //full backup
                    inParams["AffectedSystem"] = vm.Path.Path;
                    inParams["SnapshotSettings"] = settings.GetText(TextFormat.WmiDtd20);
                    inParams["SnapshotType"] = SnapshotTypeRecovery;

                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);

                    if (this.usingDedupe)
                    {
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("dedupe_used"), false, false, NO_RELATED_EVENT, EventStatus.info);
                    }
                    if (this.useEncryption)
                    {
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("encryption_used"), false, false, NO_RELATED_EVENT, EventStatus.info);
                    }

                    eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("creating_snapshot"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);

                    using (ManagementBaseObject outParams = service.InvokeMethod(
                        "CreateSnapshot",
                        inParams,
                        null))
                    {
                        //wait for the snapshot to be created
                        try
                        {

                            WmiUtilities.ValidateOutput(outParams, scope);

                        }
                        catch (Exception ex)
                        {
                            //snapshot fallback possible and allowed?
                            if (cLevel == ConsistencyLevel.ApplicationAware && allowSnapshotFallback)
                            {
                                this.eventHandler.raiseNewEvent(LanguageHandler.getString("failed"), true, false, eventId, EventStatus.error);
                                this.eventHandler.raiseNewEvent(LanguageHandler.getString("aap_failed"), false, false, NO_RELATED_EVENT, EventStatus.successful);
                                return createSnapshot(ConsistencyLevel.CrashConsistent, false);
                            }
                            else
                            {
                                //snapshot failed
                                return null;
                            }
                        }

                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);

                        //get the job and the snapshot object
                        ManagementObject job = new ManagementObject((string)outParams["job"]);

                        //get the snapshot
                        ManagementObject snapshot = null;
                        var iterator = job.GetRelated("Msvm_VirtualSystemSettingData").GetEnumerator();
                        while (iterator.MoveNext())
                        {
                            snapshot = (System.Management.ManagementObject)iterator.Current;
                        }
                        return snapshot;

                    }
                }
            }catch (Exception ex)
            {
                //error while taking snapshot
                Common.DBQueries.addLog(ex.Message, Environment.StackTrace, ex);
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("failed"), true, false, eventId, EventStatus.error);
                return null;
            }
        }

        //converts a snapshot to a reference point
        /// <summary></summary>
        public ManagementObject convertToReferencePoint(ManagementObject snapshot, bool raiseEvents)
        {
            ManagementScope scope = getHyperVManagementScope();
            int eventId = 0;
            if (raiseEvents) {
               eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("creating_refp"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);
            }

            using (ManagementObject settings = WmiUtilities.GetVirtualMachineSnapshotService(scope))
            using (ManagementObject service = WmiUtilities.GetVirtualMachineSnapshotService(scope))
            using (ManagementBaseObject inParams = service.GetMethodParameters("ConvertToReferencePoint"))
            {
                inParams["AffectedSnapshot"] = snapshot;

                //start the conversion
                using (ManagementBaseObject outParams = service.InvokeMethod(
                    "ConvertToReferencePoint",
                    inParams,
                    null))
                {
                    //wait for the reference point to be converted
                    WmiUtilities.ValidateOutput(outParams, scope);

                    //get the job and the reference point object
                    ManagementObject job = new ManagementObject((string)outParams["job"]);

                    //get the reference point
                    ManagementObject refSnapshot = null;
                    var iterator = job.GetRelated("Msvm_VirtualSystemReferencePoint").GetEnumerator();
                    while (iterator.MoveNext())
                    {
                        refSnapshot = (System.Management.ManagementObject)iterator.Current;
                    }
                    if (raiseEvents)
                    {
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);
                    }
                    return refSnapshot;
                }
            }

        }

        //exports a snapshot
        public TransferDetails export(string path, ManagementObject currentSnapshot, ManagementObject rctBase, ConfigHandler.OneJob job)
        {
            string basePath = path;
            string backupType = "";

            int eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("creating_entries"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            //generate random guid path and append it to the path var
            Guid g = Guid.NewGuid();
            string guidFolder = g.ToString();

            //create and open archive
            Common.IArchive archive;


            archive = new Common.LZ4Archive(System.IO.Path.Combine(path, guidFolder + ".nxm"), this.eventHandler, this.useEncryption, this.aesKey, this.usingDedupe, StopRequestWrapper);
            
            
            archive.create();
            archive.open(System.IO.Compression.ZipArchiveMode.Create);


            this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);

            //iterate hdds
            var iterator = currentSnapshot.GetRelated("Msvm_StorageAllocationSettingData").GetEnumerator();
            int hddCounter = 0;

            //get all hdds first (getRelated would delete the iterator when backup takes too long)
            List<ManagementObject> hdds = new List<ManagementObject>();
            while (iterator.MoveNext())
            {
                //just add vhdx files to hdd list
                if (((string[])iterator.Current["HostResource"])[0].EndsWith(".vhdx")){
                    hdds.Add((ManagementObject)iterator.Current);
                }
            }
            
            //have hdds changed?
            ChangedHDDsResponse hddsChangedResponse = hddsChanged(hdds, job);

            //set to full backup when hdds have changed
            if (hddsChangedResponse.hddsChanged)
            {
                rctBase = null;
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("storage_changed"), false, false, NO_RELATED_EVENT, EventStatus.warning);
            }

            //transfer statistics
            TransferDetails transferDetailsSummary = new TransferDetails();
            transferDetailsSummary.successful = true;

            //now export the hdds
            foreach (ManagementObject hdd in hdds)
            {
                //stop processing when last hdd was not successfully transfered
                if (!transferDetailsSummary.successful)
                {
                    break;
                }

                string[] hddPath = (string[])hdd["HostResource"];
                if (!hddPath[0].EndsWith(".vhdx"))
                {
                    continue;
                }

                //copy a full snapshot?
                if (rctBase == null)
                {
                    //just raise event by first iteration
                    if(hddCounter == 0)
                    {
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("beginning_full"), false, false, NO_RELATED_EVENT, EventStatus.successful);
                    }

                    //write to the archive
                    TransferDetails transferDetails = archive.addFile(hddPath[0], "Virtual Hard Disks");
                    transferDetailsSummary.bytesProcessed += transferDetails.bytesProcessed;
                    transferDetailsSummary.bytesTransfered += transferDetails.bytesTransfered;
                    transferDetailsSummary.successful = transferDetails.successful;

                    backupType = "full";
                }
                else
                {
                    //just raise event by first iteration
                    if (hddCounter == 0)
                    {
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("beginning_incremental"), false, false, NO_RELATED_EVENT, EventStatus.successful);
                    }
                    
                    //do a rct backup copy
                    TransferDetails transferDetails = performrctbackup(hddPath[0], ((string[])rctBase["ResilientChangeTrackingIdentifiers"])[hddCounter], archive);
                    transferDetailsSummary.bytesProcessed += transferDetails.bytesProcessed;
                    transferDetailsSummary.bytesTransfered += transferDetails.bytesTransfered;
                    transferDetailsSummary.successful = transferDetails.successful;
                    transferDetailsSummary.retryFullBackupOnFail = transferDetails.retryFullBackupOnFail;

                    backupType = "rct";
                }
                hddCounter++;

            }

            //no vhd? do nothing anymore

            //get config files
            string[] configFiles = System.IO.Directory.GetFiles(currentSnapshot["ConfigurationDataRoot"].ToString() + "\\Snapshots", currentSnapshot["ConfigurationID"].ToString() + "*");

            //copy config files when no error occured
            if (transferDetailsSummary.successful)
            {
                foreach (string file in configFiles)
                {
                    TransferDetails transferDetails = archive.addFile(file, "Virtual Machines");

                    transferDetailsSummary.bytesProcessed += transferDetails.bytesProcessed;
                    transferDetailsSummary.bytesTransfered += transferDetails.bytesTransfered;

                }
            }

            archive.close();

            //hdds changed? write the new hdd config to job
            if (hddsChangedResponse.hddsChanged)
            {
                //build hdd string list
                List<string> hddStrings = new List<string>();
                foreach (ManagementObject hdd in hdds)
                {
                    hddStrings.Add((string)hdd["InstanceID"]);
                }
                DBQueries.refreshHDDs(hddsChangedResponse.newHDDs, this.vm.vmID);
                this.eventHandler.raiseNewEvent(LanguageHandler.getString("storage_inventoried"), false, false, NO_RELATED_EVENT, EventStatus.info);

                //update job object
                JobVM currentVM = null;

                //find the corresponding vm object within current joblist
                ConfigHandler.OneJob currentJob = null;
                foreach (ConfigHandler.OneJob newJob in ConfigHandler.JobConfigHandler.Jobs)
                {
                    if (newJob.DbId == job.DbId)
                    {
                        currentJob = newJob;
                    }
                }


                foreach (JobVM vm in currentJob.JobVMs)
                {
                    if (vm.vmID == this.vm.vmID)
                    {
                        //found vm object
                        currentVM = vm;
                        break;
                    }
                }

                if (currentVM != null)
                {
                    currentVM.vmHDDs = hddsChangedResponse.newHDDs;
                }
            }


            //if LB activated for job and no error occured, start it before converting to reference point
            LiveBackupWorker lbWorker = null;
            if (job.LiveBackup && transferDetailsSummary.successful)
            {
                //another lb job already running? cancel!
                if (LiveBackupWorker.ActiveWorkers.Count > 0)
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("lb_already_running"), false, false, NO_RELATED_EVENT, EventStatus.warning);
                }
                else
                {
                    //it is possible that the job structure changed while performing backup (e.g. new job created)
                    // => so we do not update the job structure here but we look for the current job within joblist and update that structure                
                    foreach (ConfigHandler.OneJob dbJob in ConfigHandler.JobConfigHandler.Jobs)
                    {
                        if (dbJob.DbId == job.DbId)
                        {
                            lbWorker = new nxmBackup.HVBackupCore.LiveBackupWorker(job.DbId, this.eventHandler);

                            //add worker to global list
                            LiveBackupWorker.ActiveWorkers.Add(lbWorker);
                            lbWorker.startLB();

                            dbJob.LiveBackupActive = true;
                        }
                    }
                }                    
            }

            //convert the snapshot to a reference point when no error occured
            if (transferDetailsSummary.successful)
            {
                ManagementObject refP = this.convertToReferencePoint(currentSnapshot, true);


                //write backup xml
                string parentiid = "";
                if (rctBase != null)
                {
                    parentiid = (string)rctBase["InstanceId"];
                }

                bool configWritten = ConfigHandler.BackupConfigHandler.addBackup(basePath, this.useEncryption, guidFolder, backupType, (string)refP["InstanceId"], parentiid, false, this.executionId.ToString());

                if (!configWritten)
                {
                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("config_writing_error"), false, false, NO_RELATED_EVENT, EventStatus.error);
                    transferDetailsSummary.successful = false;
                }

                //now add lb backup to config.xml
                if (job.LiveBackup && lbWorker != null)
                {
                    if (!lbWorker.addToBackupConfig())
                    {
                        //stop lb immediately when config cannot be written
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("lb_config_writing_error"), false, false, NO_RELATED_EVENT, EventStatus.error);
                        System.Threading.Thread.Sleep(2000);
                        lbWorker.stopLB();
                    }
  
                }
            }
            

            return transferDetailsSummary;

        }

        //checks whether vm hdds are the same within the job definition
        private ChangedHDDsResponse hddsChanged(List<ManagementObject> mountedHDDs, ConfigHandler.OneJob job)
        {
            ChangedHDDsResponse retVal = new ChangedHDDsResponse();
            JobVM dbVM = new JobVM();
            //find the corresponding vm object
            foreach(JobVM vm in job.JobVMs)
            {
                if (vm.vmID == this.vm.vmID)
                {
                    //found vm object
                    dbVM = vm;
                    break;
                }
            }


            List<VMHDD> newHDDS = new List<VMHDD>(); //struct for new HDDs retVal

            bool hddsHaveChanged = false;
            int lbObjectID = 1;
            //iterate through all currently mounted HDDs
            foreach (ManagementObject mountedHDD in mountedHDDs) 
            {
                VMHDD hdd;
                hdd = buildHDDStructure(mountedHDD, lbObjectID);
                newHDDS.Add(hdd);

                bool hddFound = false;
                //find corresponding HDD within job
                foreach (VMHDD dbHDD in dbVM.vmHDDs)
                {              

                    if (hdd.name == dbHDD.name)
                    {
                        hddFound = true;
                        break;
                    }
                }
                //hdd not found?
                if (!hddFound)
                {
                    hddsHaveChanged = true;
                }

                lbObjectID++;
            }

            //hdd count changed or hdds themself changed?
            if (mountedHDDs.Count != dbVM.vmHDDs.Count || hddsHaveChanged)
            {
                retVal.hddsChanged = true;
            }
            else
            {
                retVal.hddsChanged = false;
            }

            retVal.newHDDs = newHDDS;
            return retVal;
        }

        //builds a hdd structure from a given ManagementObject
        private VMHDD buildHDDStructure(ManagementObject mo, int currentLBObjectID)
        {
            VMHDD newHDD = new VMHDD();
            //get vhdx id from vhdx file
            string vhdxPath = ((string[])mo["HostResource"])[0];


            string hddID = Convert.ToBase64String(vhdxParser.getVHDXIDFromFile(vhdxPath));
            
            newHDD.lbObjectID = currentLBObjectID;
            newHDD.name = hddID;
            newHDD.path = vhdxPath;

            return newHDD;
        }


        //performs a rct backup copy
        private TransferDetails performrctbackup(string snapshothddPath, string rctID, Common.IArchive archive)
        {
            TransferDetails transferDetails = new TransferDetails();

            //read vhd size
            VirtualDiskHandler diskHandler = new VirtualDiskHandler(snapshothddPath);
            diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.AttachReadOnly | VirtualDiskHandler.VirtualDiskAccessMask.GetInfo);
            VirtualDiskHandler.GetVirtualDiskInfoSize sizeStruct = diskHandler.getSize();
            ulong hddSize = sizeStruct.VirtualSize;
            ulong bufferSize = sizeStruct.SectorSize * 50000; //buffersize has to be a multiple of virtual sector size
            diskHandler.close();


            ManagementScope scope = getHyperVManagementScope();
            //get the necessary wmi objects
            using (ManagementObject imageManagementService = WmiUtilities.GetImageManagementService(scope))
            using (ManagementBaseObject inParams = imageManagementService.GetMethodParameters("GetVirtualDiskChanges"))
            {
                inParams["Path"] = snapshothddPath;
                inParams["LimitId"] = rctID;
                inParams["ByteOffset"] = 0;

                //set snapshot hdd size
                inParams["ByteLength"] = hddSize;

                using (ManagementBaseObject outParams = imageManagementService.InvokeMethod(
                    "GetVirtualDiskChanges",
                    inParams,
                    null))
                {
                    //wait for the snapshot to be exported
                    try
                    {
                        WmiUtilities.ValidateOutput(outParams, scope);
                    }catch (Exception ex)
                    {
                        DBQueries.addLog("GetVirtualDiskChanges call failed", Environment.StackTrace, ex);
                        this.eventHandler.raiseNewEvent(LanguageHandler.getString("previous_backup_not_found"), false, false, NO_RELATED_EVENT, EventStatus.error);
                        transferDetails.successful = false;
                        transferDetails.retryFullBackupOnFail = true; //force retry by using full backup
                        return transferDetails;
                    }

                    //get vhdx size
                    UInt64 vhdxSize;
                    System.IO.FileInfo fi = new System.IO.FileInfo(snapshothddPath);
                    vhdxSize = (UInt64)fi.Length;

                    //get vhdx headers
                    BATTable batTable;
                    UInt32 vhdxBlockSize = 0;
                    UInt32 vhdxLogicalSectorSize = 0;
                    UInt64 virtualDiskSize = 0;
                    RawBatTable rawBatTable;
                    RawHeader rawHeader;
                    RawLog rawLog;
                    RawMetadataTable rawMeta;
                    using (Common.vhdxParser vhdxParser = new vhdxParser(snapshothddPath))
                    {
                        Common.RegionTable regionTable = vhdxParser.parseRegionTable();
                        Common.MetadataTable metadataTable = vhdxParser.parseMetadataTable(regionTable);
                        vhdxBlockSize = vhdxParser.getBlockSize(metadataTable);
                        vhdxLogicalSectorSize = vhdxParser.getLogicalSectorSize(metadataTable);
                        virtualDiskSize = vhdxParser.getVirtualDiskSize(metadataTable);

                        UInt32 vhdxChunkRatio = (UInt32)((Math.Pow(2, 23) * vhdxLogicalSectorSize) / vhdxBlockSize); //see vhdx file format specs

                        UInt64 dataBlocksCount = (UInt64)Math.Ceiling((double)virtualDiskSize / (double)vhdxBlockSize);
                        UInt32 sectorBitmapBlocksCount = (UInt32)Math.Ceiling((double)dataBlocksCount / (double)vhdxChunkRatio);

                        batTable = vhdxParser.parseBATTable(regionTable, vhdxChunkRatio, sectorBitmapBlocksCount, true);

                        //get raw bat table
                        rawBatTable = vhdxParser.getRawBatTable(regionTable);

                        //get raw header
                        rawHeader = vhdxParser.getRawHeader();

                        //get raw log section
                        rawLog = vhdxParser.getRawLog();

                        //get raw metatable
                        rawMeta = vhdxParser.getRawMetadataTable(regionTable);
                    }

                    //reopen virtual disk
                    diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.AttachReadOnly | VirtualDiskHandler.VirtualDiskAccessMask.GetInfo);
                    diskHandler.attach(VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST | VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY);



                    //output ok, build block structure
                    int eventId = this.eventHandler.raiseNewEvent(LanguageHandler.getString("processing_blocks"), false, false, NO_RELATED_EVENT, EventStatus.inProgress);
                    int blockCount = ((ulong[])outParams["ChangedByteOffsets"]).Length;
                    ChangedBlock[] changedBlocks = new ChangedBlock[blockCount];
 

                    ulong[] offsets = (ulong[])outParams["ChangedByteOffsets"];
                    ulong[] lengths = (ulong[])outParams["ChangedByteLengths"];

                    for (int i = 0; i < changedBlocks.Length; i++) {
                        changedBlocks[i].offset = offsets[i];
                        changedBlocks[i].length = lengths[i];
                        transferDetails.bytesProcessed += lengths[i];
                    }

                    this.eventHandler.raiseNewEvent(LanguageHandler.getString("successful"), true, false, eventId, EventStatus.successful);

                    //write backup output
                    DiffHandler diffWriter = new DiffHandler(this.eventHandler, null);

                    transferDetails.bytesTransfered = diffWriter.writeDiffFile(changedBlocks, diskHandler, vhdxBlockSize, archive, System.IO.Path.GetFileName(snapshothddPath), batTable, bufferSize, rawBatTable, rawHeader, rawLog, rawMeta, vhdxSize);

                    
                    //close vhd file
                    diskHandler.detach();
                    diskHandler.close();

                }


            }

            transferDetails.successful = true;
            return transferDetails;
        }


        //gets a list of all snapshots
        public List<ManagementObject> getSnapshots(string snapshotType)
        {
            List<ManagementObject> snapshots = new List<ManagementObject>();
            ManagementScope scope = getHyperVManagementScope();

            // Get the necessary wmi objects
            using (ManagementObject vm = WmiUtilities.GetVirtualMachine(this.vm.vmID, scope))
            {
                //cancel when vm not found
                if (vm == null)
                {
                    return null;
                }

                //get all snapshots
                var iterator = vm.GetRelationships("Msvm_SnapshotOfVirtualSystem").GetEnumerator();

                //iterate all snapshots
                while (iterator.MoveNext())
                {
                    var snapshot = (System.Management.ManagementObject)iterator.Current;

                    //get "dependent" vs settings class
 
                    ManagementObject vsSettings = new ManagementObject(getHyperVManagementScope(), new ManagementPath(snapshot["dependent"].ToString()), null);
                    string type = vsSettings["VirtualSystemType"].ToString();

                    //is recovery snapshot?
                    if (type == snapshotType)
                    {
                        snapshots.Add(vsSettings);
                    }
                }
            }


            return snapshots;
        }

        //reads the virtual disk id from the given vhd
        private string getVHDID(string vhdPath)
        {
            ManagementScope scope = getHyperVManagementScope();
            using (ManagementObject service = WmiUtilities.GetImageManagementService(scope))
            using (ManagementBaseObject inParams = service.GetMethodParameters("GetVirtualHardDiskSettingData"))
            {
                inParams["Path"] = vhdPath;
                //call method
                using (ManagementBaseObject outParams = service.InvokeMethod(
                    "GetVirtualHardDiskSettingData",
                    inParams,
                    null))
                {
                    //wait for the method to be executed
                    WmiUtilities.ValidateOutput(outParams, scope);
                }
            }
            return "";
        }

        //gets a list of all reference points
        public List<ManagementObject> getReferencePoints()
        {
            List<ManagementObject> rPoints = new List<ManagementObject>();
            ManagementScope scope = getHyperVManagementScope();

            // Get the necessary wmi objects
            using (ManagementObject vm = WmiUtilities.GetVirtualMachine(this.vm.vmID, scope))
            {
                //get all reference points
                var iterator = vm.GetRelationships("Msvm_ReferencePointOfVirtualSystem").GetEnumerator();

                //iterate all reference points
                while (iterator.MoveNext())
                {
                    var rPoint = (System.Management.ManagementObject)iterator.Current;

                    //get "dependent" vs settings class
                    ManagementObject vsSettings = new ManagementObject(rPoint["dependent"].ToString());
                    rPoints.Add(vsSettings);
                }
            }

            return rPoints;
        }

        //gets a list of reference points filtered by InstanceId
        public ManagementObject getReferencePoint(string iid)
        {
            List<ManagementObject> refPs = getReferencePoints();

            foreach (ManagementObject refP in refPs)
            {
                if ((string)refP["InstanceId"] == iid)
                {
                    return refP;
                }
            }
            return null;
        }

        //removes a reference point
        public void removeReferencePoint(ManagementObject rPoint)
        {
            ManagementScope scope = getHyperVManagementScope();

            // Get the necessary wmi objects
            using (ManagementObject rpService = WmiUtilities.GetVirtualSystemReferencePointService(scope))
            using (ManagementBaseObject inParams = rpService.GetMethodParameters("DestroyReferencePoint"))
            {
                inParams["AffectedReferencePoint"] = rPoint;
                using (ManagementBaseObject outParams = rpService.InvokeMethod(
                    "DestroyReferencePoint",
                    inParams,
                    null))
                {
                    //wait for the reference point to be converted
                    WmiUtilities.ValidateOutput(outParams, scope);

                }
            }
        }

        //removes a snapshot (including all child snapshots) --> not working
        public void removeSnapshot(ManagementObject snapshot)
        {
            ManagementScope scope = getHyperVManagementScope();

            // Get the management service and the VM object.
            using (ManagementObject vm = WmiUtilities.GetVirtualMachine(this.vm.vmID, scope))
            using (ManagementObject service = WmiUtilities.GetVirtualMachineSnapshotService(scope))
            using (ManagementObject settings = WmiUtilities.GetVirtualMachineSnapshotSettings(scope))
            using (ManagementBaseObject inParams = service.GetMethodParameters("DestroySnapshotTree"))
            {
                inParams["SnapshotSettingData"] = snapshot;
                using (ManagementBaseObject outParams = service.InvokeMethod(
                    "DestroySnapshotTree",
                    inParams,
                    null))
                {
                    //wait for the reference point to be converted
                    WmiUtilities.ValidateOutput(outParams, scope);

                }
            }
        }

        //removes all snapshots and reference points
        public void cleanUp()
        {
            List<ManagementObject> snapshots = this.getSnapshots(RECOVERY_SNAPSHOT_TYPE);
            foreach (ManagementObject snapshot in snapshots)
            {
                this.convertToReferencePoint(snapshot, false);
            }

            List<ManagementObject> refPs = this.getReferencePoints();
            foreach (ManagementObject snapshot in refPs)
            {
                this.removeReferencePoint(snapshot);
            }
        }

        //creates a HyperV ManagementScope
        private ManagementScope getHyperVManagementScope()
        {
            ManagementScope scope = new ManagementScope(WMIHelper.GetHyperVWMIScope(this.vm.host), this.vm.getHostAuthData());
            return scope;
        }

        //struct for hddsChanged retVal
        private struct ChangedHDDsResponse
        {
            public bool hddsChanged;
            public List<VMHDD> newHDDs;
        }

    }
}
