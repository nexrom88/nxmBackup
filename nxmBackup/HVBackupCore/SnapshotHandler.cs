using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using Common;


namespace HyperVBackupRCT
{

    public class SnapshotHandler
    {
        private const UInt16 SnapshotTypeRecovery = 32768;
        private const UInt16 SnapshotTypeFull = 2;
        private JobVM vm;
        private int executionId;
        private const int NO_RELATED_EVENT = -1;
        private Common.EventHandler eventHandler;

        public SnapshotHandler(JobVM vm, int executionId)
        {
            this.vm = vm;
            this.executionId = executionId;
            this.eventHandler = new Common.EventHandler(vm, executionId);
        }

        //performs a full backup chain
        public bool performFullBackupProcess(ConsistencyLevel cLevel, Boolean allowSnapshotFallback, bool incremental, ConfigHandler.OneJob job)
        {
            string destination = job.BasePath;
            ManagementObject snapshot = createSnapshot(cLevel, allowSnapshotFallback);
            ManagementObject refP = null;

            //add job name and vm name to destination
            destination = System.IO.Path.Combine(destination, job.Name + "\\" + this.vm.vmID);

            //create folder if it does not exist
            System.IO.Directory.CreateDirectory(destination);

            List<ConfigHandler.BackupConfigHandler.BackupInfo> chain = ConfigHandler.BackupConfigHandler.readChain(destination);
            if (incremental) //incremental backup? get latest reference point
            {
                if (chain == null || chain.Count == 0) //first backup must be full backup
                {
                    this.eventHandler.raiseNewEvent("Inkrementielles Backup nicht möglich", false, false, NO_RELATED_EVENT, EventStatus.info);
                    refP = null; //incremental backup not possible
                }
                else if (getBlockSize(chain) >= job.BlockSize) //block size reached?
                {
                    refP = null; //incremental backup not possible
                }
                else
                {
                    //incremental backup possible, get required reference point
                    string instanceID = chain[chain.Count - 1].instanceID;
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
            export(destination, snapshot, refP, job);

            //read current backup chain for further processing
            chain = ConfigHandler.BackupConfigHandler.readChain(destination);

            //if full backup, delete unnecessary reference points
            if (refP == null)
            {
                int eventId = this.eventHandler.raiseNewEvent("Entferne alte Referenz Punkte...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);
                //remove current (last) backup
                chain.RemoveAt(chain.Count - 1);
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
                this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventId, EventStatus.successful);
            }

            //read current backup chain for further processing
            chain = ConfigHandler.BackupConfigHandler.readChain(destination);

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
                if (job.Rotation.maxElementCount > 0 && getBlockCount(chain) > job.Rotation.maxElementCount)
                {
                    blockRotate(destination, chain);
                }
            }

            this.eventHandler.raiseNewEvent("Backupvorgang erfolgreich", false, false, NO_RELATED_EVENT, EventStatus.successful);
            return true;
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
                else //rct backup found -> increment blockSize counter
                {
                    blockSize++;
                }
            }
            return blockSize;
        }


        //performs a block rotation
        private void blockRotate(string path, List<ConfigHandler.BackupConfigHandler.BackupInfo> chain)
        {
            int eventID = this.eventHandler.raiseNewEvent("Rotiere Backups (Block Rotation)...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

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
                    //remove rct backup
                    ConfigHandler.BackupConfigHandler.removeBackup(path, chain[i].uuid); //remove from config
                    System.IO.Directory.Delete(System.IO.Path.Combine(path, chain[i].uuid + ".nxm"), true); //remove backup file
                }
            }

            this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventID, EventStatus.successful);
        }

        //merge two backups to keep max snapshot count
        private void mergeOldest(string path, List<ConfigHandler.BackupConfigHandler.BackupInfo> chain)
        {
            //when the first two backups are "full" backups then the first one can just be deleted
            if (chain[0].type == "full" && chain[1].type == "full")
            {
                ConfigHandler.BackupConfigHandler.removeBackup(path, chain[0].uuid); //remove from config
                System.IO.File.Delete(System.IO.Path.Combine(path, chain[0].uuid + ".nxm")); //remove backup file
                return;
            }

            //Given at this point: first backup is "full", the second one is "rct"

            //create staging dir
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(path, "staging"));

            int eventId;
            eventId = this.eventHandler.raiseNewEvent("Rotiere Backups (Schritt 1 von 5)...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            RestoreHelper.FullRestoreHandler restHandler = new RestoreHelper.FullRestoreHandler(null);

            //perform restore to staging directory (including merge with second backup)
            restHandler.performFullRestoreProcess(path, System.IO.Path.Combine(path, "staging"), chain[1].instanceID);

            this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventId, EventStatus.successful);
            eventId = this.eventHandler.raiseNewEvent("Rotiere Backups (Schritt 2 von 5)...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            //remove first and second backup from backup chain
            ConfigHandler.BackupConfigHandler.removeBackup(path, chain[0].uuid); //remove from config
            System.IO.Directory.Delete(System.IO.Path.Combine(path, chain[0].uuid + ".nxm"), true); //remove backup file
            ConfigHandler.BackupConfigHandler.removeBackup(path, chain[1].uuid); //remove from config
            System.IO.Directory.Delete(System.IO.Path.Combine(path, chain[1].uuid + ".nxm"), true); //remove backup file

            //create new backup container from merged backups
            Guid g = Guid.NewGuid();
            string guidFolder = g.ToString();
            Common.LZ4Archive backupArchive = new Common.LZ4Archive(System.IO.Path.Combine(path, guidFolder + ".nxm"), null);
            backupArchive.create();
            backupArchive.open(System.IO.Compression.ZipArchiveMode.Create);

            this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventId, EventStatus.successful);
            eventId = this.eventHandler.raiseNewEvent("Rotiere Backups (Schritt 3 von 5)...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            //add whole staging directory to the container archive
            backupArchive.addDirectory(System.IO.Path.Combine(path, "staging"));
            backupArchive.close();

            this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventId, EventStatus.successful);
            eventId = this.eventHandler.raiseNewEvent("Rotiere Backups (Schritt 4 von 5)...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            //create entry to backup chain
            ConfigHandler.BackupConfigHandler.addBackup(path, guidFolder, "full", chain[1].instanceID, "", true);

            this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventId, EventStatus.successful);
            eventId = this.eventHandler.raiseNewEvent("Rotiere Backups (Schritt 5 von 5)...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

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

            this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventId, EventStatus.successful);


        }

        //creates the snapshot
        public ManagementObject createSnapshot(ConsistencyLevel cLevel, Boolean allowSnapshotFallback)
        {
            ManagementScope scope = new ManagementScope("\\\\localhost\\root\\virtualization\\v2", null);
            int eventId = this.eventHandler.raiseNewEvent("Initialisiere Umgebung...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            // Get the management service and the VM object.
            using (ManagementObject vm = WmiUtilities.GetVirtualMachine(this.vm.vmID, scope))
            using (ManagementObject service = WmiUtilities.GetVirtualMachineSnapshotService(scope))
            using (ManagementObject settings = WmiUtilities.GetVirtualMachineSnapshotSettings(scope))
            using (ManagementBaseObject inParams = service.GetMethodParameters("CreateSnapshot"))
            {
                //set settings
                settings["ConsistencyLevel"] = cLevel == ConsistencyLevel.ApplicationAware ? 1 : 2;
                settings["IgnoreNonSnapshottableDisks"] = true;
                inParams["AffectedSystem"] = vm.Path.Path;
                inParams["SnapshotSettings"] = settings.GetText(TextFormat.WmiDtd20);
                inParams["SnapshotType"] = SnapshotTypeRecovery;

                this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventId, EventStatus.successful);
                eventId = this.eventHandler.raiseNewEvent("Erzeuge Recovery Snapshot...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

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
                            this.eventHandler.raiseNewEvent("fehlgeschlagen", true, false, eventId, EventStatus.error);
                            this.eventHandler.raiseNewEvent("'Application Aware Processing' steht nicht zur Verfügung. Versuche Fallback.", false, false, NO_RELATED_EVENT, EventStatus.successful);
                            return createSnapshot(ConsistencyLevel.CrashConsistent, false);
                        }
                        else
                        {
                            //snapshot failed
                            return null;
                        }
                    }

                    this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventId, EventStatus.successful);

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
        }

        //converts a snapshot to a reference point
        /// <summary></summary>
        public ManagementObject convertToReferencePoint(ManagementObject snapshot)
        {
            ManagementScope scope = new ManagementScope("\\\\localhost\\root\\virtualization\\v2", null);
            int eventId = this.eventHandler.raiseNewEvent("Referenzpunkt wird erzeugt...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

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
                    this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventId, EventStatus.successful);
                    return refSnapshot;
                }
            }

        }

        //exports a snapshot
        public void export(string path, ManagementObject currentSnapshot, ManagementObject rctBase, ConfigHandler.OneJob job)
        {
            string basePath = path;
            string backupType = "";

            int eventId = this.eventHandler.raiseNewEvent("Erzeuge Einträge...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            //generate random guid path and append it to the path var
            Guid g = Guid.NewGuid();
            string guidFolder = g.ToString();

            //create and open archive
            Common.IArchive archive;
            
           
            archive = new Common.LZ4Archive(System.IO.Path.Combine(path, guidFolder + ".nxm"), this.eventHandler);
            
            
            archive.create();
            archive.open(System.IO.Compression.ZipArchiveMode.Create);


            this.eventHandler.raiseNewEvent("erfolgreich", true, false, eventId, EventStatus.successful);

            //iterate hdds
            var iterator = currentSnapshot.GetRelated("Msvm_StorageAllocationSettingData").GetEnumerator();
            int hddCounter = 0;

            //get all hdds first (getRelated would delete the iterator when backup takes too long)
            List<ManagementObject> hdds = new List<ManagementObject>();
            while (iterator.MoveNext())
            {
                hdds.Add((ManagementObject)iterator.Current);
            }
            
            //have hdds changed?
            ChangedHDDsResponse hddsChangedResponse = hddsChanged(hdds, job);

            //set to full backup when hdds have changed
            if (hddsChangedResponse.hddsChanged)
            {
                rctBase = null;
                this.eventHandler.raiseNewEvent("Veränderter Datenspeicher erkannt", false, false, NO_RELATED_EVENT, EventStatus.warning);
            }

            //now export the hdds
            foreach (ManagementObject hdd in hdds)
            {
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
                        this.eventHandler.raiseNewEvent("Beginne Vollbackup", false, false, NO_RELATED_EVENT, EventStatus.successful);
                    }
                    //write to the archive
                    archive.addFile(hddPath[0], "Virtual Hard Disks");

                    backupType = "full";
                }
                else
                {
                    //just raise event by first iteration
                    if (hddCounter == 0)
                    {
                        this.eventHandler.raiseNewEvent("Beginne inkrementielles Backup", false, false, NO_RELATED_EVENT, EventStatus.successful);
                    }
                    
                    //do a rct backup copy
                    performrctbackup(hddPath[0], ((string[])rctBase["ResilientChangeTrackingIdentifiers"])[hddCounter], archive);

                    backupType = "rct";
                }
                hddCounter++;

            }

            //no vhd? do nothing anymore

            //get config files
            string[] configFiles = System.IO.Directory.GetFiles(currentSnapshot["ConfigurationDataRoot"].ToString() + "\\Snapshots", currentSnapshot["ConfigurationID"].ToString() + "*");

            //copy config files
            foreach (string file in configFiles)
            {
                archive.addFile(file, "Virtual Machines");
                //System.IO.File.Copy(file, System.IO.Path.Combine(path, "Virtual Machines\\" + System.IO.Path.GetFileName(file)));
            }
            archive.close();

            //if LB activated for job, start it before converting to reference point
            if (job.LiveBackup)
            {
                job.LiveBackupWorker = new nxmBackup.HVBackupCore.LiveBackupWorker(job);
                job.LiveBackupWorker.startLB();
            }

            //convert the snapshot to a reference point
            ManagementObject refP = this.convertToReferencePoint(currentSnapshot);

            //write backup xml
            string parentiid = "";
            if (rctBase != null)
            {
                parentiid = (string)rctBase["InstanceId"];
            }

            ConfigHandler.BackupConfigHandler.addBackup(basePath, guidFolder, backupType, (string)refP["InstanceId"], parentiid, false);

            //now add lb backup to config.xml
            if (job.LiveBackup)
            {
                job.LiveBackupWorker.addToBackupConfig();
            }

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
                this.eventHandler.raiseNewEvent("Veränderter Datenspeicher inventarisiert", false, false, NO_RELATED_EVENT, EventStatus.info);
            }

        }

        //checks whether vm hdds are the same within the job definition
        private ChangedHDDsResponse hddsChanged(List<ManagementObject> mountedHDDs, ConfigHandler.OneJob job)
        {
            ChangedHDDsResponse retVal = new ChangedHDDsResponse();
            JobVM currentVM = new JobVM();
            //find the corresponding vm object
            foreach(JobVM vm in job.JobVMs)
            {
                if (vm.vmID == this.vm.vmID)
                {
                    //found vm object
                    currentVM = vm;
                    break;
                }
            }


            List<VMHDD> newHDDS = new List<VMHDD>(); //struct for new HDDs retVal

            bool hddsHaveChanged = false;
            //iterate through all currently mounted HDDs
            foreach (ManagementObject mountedHDD in mountedHDDs)
            {
                bool hddFound = false;
                //find corresponding HDD within job
                foreach (VMHDD vmHDD in currentVM.vmHDDs)
                {
                    VMHDD newHDD = new VMHDD();
                    //get vhdx id from vhdx file
                    string vhdxPath = ((string[])mountedHDD["HostResource"])[0];
                    string hddID = Convert.ToBase64String(vhdxParser.getVHDXIDFromFile(vhdxPath));
                    
                    newHDD.name = hddID;
                    newHDD.path = vhdxPath;
                    newHDDS.Add(newHDD);

                    if (hddID == vmHDD.name)
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
            }

            //hdd count changed or hdds themself changed?
            if (mountedHDDs.Count != currentVM.vmHDDs.Count || hddsHaveChanged)
            {
                retVal.hddsChanged = true;;
            }
            else
            {
                retVal.hddsChanged = false;
            }

            retVal.newHDDs = newHDDS;
            return retVal;
        }


        //performs a rct backup copy
        private void performrctbackup(string snapshothddPath, string rctID, Common.IArchive archive)
        {
            //read vhd size
            VirtualDiskHandler diskHandler = new VirtualDiskHandler(snapshothddPath);
            diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.AttachReadOnly | VirtualDiskHandler.VirtualDiskAccessMask.GetInfo);
            VirtualDiskHandler.GetVirtualDiskInfoSize sizeStruct = diskHandler.getSize();
            ulong hddSize = sizeStruct.VirtualSize;
            ulong bufferSize = sizeStruct.SectorSize * 10000; //buffersize has to be a multiple of virtual sector size
            diskHandler.close();


            ManagementScope scope = new ManagementScope("\\\\localhost\\root\\virtualization\\v2", null);
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
                    WmiUtilities.ValidateOutput(outParams, scope);

                    //get vhdx size
                    UInt64 vhdxSize;
                    System.IO.FileInfo fi = new System.IO.FileInfo(snapshothddPath);
                    vhdxSize = (UInt64)fi.Length;

                    //get vhdx headers
                    BATTable batTable;
                    UInt32 vhdxBlockSize = 0;
                    UInt32 vhdxLogicalSectorSize = 0;
                    RawBatTable rawBatTable;
                    RawHeader rawHeader;
                    using (Common.vhdxParser vhdxParser = new vhdxParser(snapshothddPath))
                    {
                        Common.RegionTable regionTable = vhdxParser.parseRegionTable();
                        Common.MetadataTable metadataTable = vhdxParser.parseMetadataTable(regionTable);
                        vhdxBlockSize = vhdxParser.getBlockSize(metadataTable);
                        vhdxLogicalSectorSize = vhdxParser.getLogicalSectorSize(metadataTable);

                        UInt32 vhdxChunkRatio = (UInt32)((Math.Pow(2, 23) * vhdxLogicalSectorSize) / vhdxBlockSize); //see vhdx file format specs

                        batTable = vhdxParser.parseBATTable(regionTable, vhdxChunkRatio, true);

                        //get raw bat table
                        rawBatTable = vhdxParser.getRawBatTable(regionTable);

                        //get raw header
                        rawHeader = vhdxParser.getRawHeader();
                    }

                    //reopen virtual disk
                    diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.AttachReadOnly | VirtualDiskHandler.VirtualDiskAccessMask.GetInfo);
                    diskHandler.attach(VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST | VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY);
                    //output ok, build block structure
                    int blockCount = ((ulong[])outParams["ChangedByteOffsets"]).Length;
                    ChangedBlock[] changedBlocks = new ChangedBlock[blockCount];

                    for (int i = 0; i < blockCount; i++)
                    {
                        ChangedBlock block = new ChangedBlock();
                        block.offset = ((ulong[])outParams["ChangedByteOffsets"])[i];
                        block.length = ((ulong[])outParams["ChangedByteLengths"])[i];
                        changedBlocks[i] = block;
                    }

                    //write backup output
                    DiffHandler diffWriter = new DiffHandler(this.eventHandler);

                    diffWriter.writeDiffFile(changedBlocks, diskHandler, vhdxBlockSize, archive, System.IO.Path.GetFileName(snapshothddPath), batTable, bufferSize, rawBatTable, rawHeader, vhdxSize);

                    
                    //close vhd file
                    diskHandler.detach();
                    diskHandler.close();

                }


            }
        }


        //gets a list of all snapshots
        public List<ManagementObject> getSnapshots()
        {
            List<ManagementObject> snapshots = new List<ManagementObject>();
            ManagementScope scope = new ManagementScope("\\\\localhost\\root\\virtualization\\v2", null);

            // Get the necessary wmi objects
            using (ManagementObject vm = WmiUtilities.GetVirtualMachine(this.vm.vmID, scope))
            {
                //get all snapshots
                var iterator = vm.GetRelationships("Msvm_SnapshotOfVirtualSystem").GetEnumerator();

                //iterate all snapshots
                while (iterator.MoveNext())
                {
                    var snapshot = (System.Management.ManagementObject)iterator.Current;

                    //get "dependent" vs settings class
                    ManagementObject vsSettings = new ManagementObject(snapshot["dependent"].ToString());
                    string type = vsSettings["VirtualSystemType"].ToString();

                    //is recovery snapshot
                    if (type == "Microsoft:Hyper-V:Snapshot:Recovery")
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
            ManagementScope scope = new ManagementScope("\\\\localhost\\root\\virtualization\\v2", null);
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
            ManagementScope scope = new ManagementScope("\\\\localhost\\root\\virtualization\\v2", null);

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
            ManagementScope scope = new ManagementScope("\\\\localhost\\root\\virtualization\\v2", null);

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
            ManagementScope scope = new ManagementScope("\\\\localhost\\root\\virtualization\\v2", null);

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
            List<ManagementObject> snapshots = this.getSnapshots();
            foreach (ManagementObject snapshot in snapshots)
            {
                this.convertToReferencePoint(snapshot);
            }

            List<ManagementObject> refPs = this.getReferencePoints();
            foreach (ManagementObject snapshot in refPs)
            {
                this.removeReferencePoint(snapshot);
            }
        }

        //struct for hddsChanged retVal
        private struct ChangedHDDsResponse
        {
            public bool hddsChanged;
            public List<VMHDD> newHDDs;
        }



    }
}
