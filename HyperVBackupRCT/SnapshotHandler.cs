using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.Runtime.InteropServices;
using Common;


namespace HyperVBackupRCT
{

    public class SnapshotHandler
    {
        private const UInt16 SnapshotTypeRecovery = 32768;
        private const UInt16 SnapshotTypeFull = 2;
        private string vmName;
        public event Common.Job.newEventDelegate newEvent;

        public SnapshotHandler(string vmName)
        {
            this.vmName = vmName;
        }

        //performs a full backup chain
        public void performFullBackupProcess(ConsistencyLevel cLevel, Boolean allowSnapshotFallback, string destination, bool incremental, ConfigHandler.OneJob job)
        {
            ManagementObject snapshot = createSnapshot(cLevel, allowSnapshotFallback);
            ManagementObject refP = null;

            //create folder if it does not exist
            System.IO.Directory.CreateDirectory(destination);

            List<ConfigHandler.BackupConfigHandler.BackupInfo> chain = ConfigHandler.BackupConfigHandler.readChain(destination);
            if (incremental) //incremental backup? get latest reference point
            {
                if (chain == null || chain.Count == 0) //first backup must be full backup
                {
                    raiseNewEvent("Inkrementielles Backup nicht möglich", false, false);
                    refP = null; //incremental backup not possible
                }
                else if (getBlockSize(chain) >= job.blockSize) //block size reached?
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
            export(destination, snapshot, refP, job.compression);

            //read current backup chain for further processing
            chain = ConfigHandler.BackupConfigHandler.readChain(destination);

            //if full backup, delete unnecessary reference points
            if (refP == null)
            {
                raiseNewEvent("Entferne alte Referenz Punkte...", false, false);
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
                raiseNewEvent("erfolgreich", true, false);
            }

            //read current backup chain for further processing
            chain = ConfigHandler.BackupConfigHandler.readChain(destination);

            //check whether max snapshot count is reached, then merge
            if (job.rotation.type == ConfigHandler.RotationType.merge) //RotationType = "merge"
            {
                if (job.rotation.maxElementCount > 0 && chain.Count > job.rotation.maxElementCount)
                {
                    mergeOldest(destination, chain, job.compression);
                }
            }
            else if (job.rotation.type == ConfigHandler.RotationType.blockRotation) //RotationType = "blockRotation"
            {
                if (job.rotation.maxElementCount > 0 && getBlockCount(chain) > job.rotation.maxElementCount)
                {
                    blockRotate(destination, chain);
                }
            }

            raiseNewEvent("Backupvorgang erfolgreich", false, false);

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
            raiseNewEvent("Rotiere Backups (Block Rotation)...", false, false);

            //remove first full backup
            ConfigHandler.BackupConfigHandler.removeBackup(path, chain[0].uuid); //remove from config
            System.IO.File.Delete(System.IO.Path.Combine(path, chain[0].uuid + ".nxm")); //remove backup file

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
                    System.IO.File.Delete(System.IO.Path.Combine(path, chain[i].uuid + ".nxm")); //remove backup file
                }
            }

            raiseNewEvent("erfolgreich", true, false);
        }

        //merge two backups to keep max snapshot count
        private void mergeOldest(string path, List<ConfigHandler.BackupConfigHandler.BackupInfo> chain, ConfigHandler.Compression compressionType)
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

            raiseNewEvent("Rotiere Backups (Schritt 1 von 5)...", false, false);

            RestoreHandler restHandler = new RestoreHandler();

            //perform restore to staging directory (including merge with second backup)
            restHandler.performFullRestoreProcess(path, System.IO.Path.Combine(path, "staging"), chain[1].instanceID, compressionType);

            raiseNewEvent("erfolgreich", true, false);
            raiseNewEvent("Rotiere Backups (Schritt 2 von 5)...", false, false);

            //remove first and second backup from backup chain
            ConfigHandler.BackupConfigHandler.removeBackup(path, chain[0].uuid); //remove from config
            System.IO.File.Delete(System.IO.Path.Combine(path, chain[0].uuid + ".nxm")); //remove backup file
            ConfigHandler.BackupConfigHandler.removeBackup(path, chain[1].uuid); //remove from config
            System.IO.File.Delete(System.IO.Path.Combine(path, chain[1].uuid + ".nxm")); //remove backup file

            //create new backup container from merged backups
            Guid g = Guid.NewGuid();
            string guidFolder = g.ToString();
            Common.ZipArchive backupArchive = new Common.ZipArchive(System.IO.Path.Combine(path, guidFolder + ".nxm"), null);
            backupArchive.create();
            backupArchive.open(System.IO.Compression.ZipArchiveMode.Create);

            raiseNewEvent("erfolgreich", true, false);
            raiseNewEvent("Rotiere Backups (Schritt 3 von 5)...", false, false);

            //add whole staging directory to the container archive
            backupArchive.addDirectory(System.IO.Path.Combine(path, "staging"));
            backupArchive.close();

            raiseNewEvent("erfolgreich", true, false);
            raiseNewEvent("Rotiere Backups (Schritt 4 von 5)...", false, false);

            //create entry to backup chain
            ConfigHandler.BackupConfigHandler.addBackup(path, guidFolder, "full", chain[1].instanceID, "", true);

            raiseNewEvent("erfolgreich", true, false);
            raiseNewEvent("Rotiere Backups (Schritt 5 von 5)...", false, false);

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

            raiseNewEvent("erfolgreich", true, false);


        }

        //creates the snapshot
        public ManagementObject createSnapshot(ConsistencyLevel cLevel, Boolean allowSnapshotFallback)
        {
            ManagementScope scope = new ManagementScope("\\\\localhost\\root\\virtualization\\v2", null);
            raiseNewEvent("Initialisiere Umgebung...", false, false);

            // Get the management service and the VM object.
            using (ManagementObject vm = WmiUtilities.GetVirtualMachine(vmName, scope))
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

                raiseNewEvent("erfolgreich", true, false);
                raiseNewEvent("Erzeuge Recovery Snapshot...", false, false);

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
                            raiseNewEvent("fehlgeschlagen", true, false);
                            raiseNewEvent("'Application Aware Processing' steht nicht zur Verfügung. Versuche Fallback.", false, false);
                            return createSnapshot(ConsistencyLevel.CrashConsistent, false);
                        }
                        else
                        {
                            //snapshot failed
                            return null;
                        }
                    }

                    raiseNewEvent("erfolgreich", true, false);

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
            raiseNewEvent("Referenzpunkt wird erzeugt...", false, false);

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
                    raiseNewEvent("erfolgreich", true, false);
                    return refSnapshot;
                }
            }

        }

        //exports a snapshot
        public void export(string path, ManagementObject currentSnapshot, ManagementObject rctBase, ConfigHandler.Compression compressionType)
        {
            string basePath = path;
            string backupType = "";

            raiseNewEvent("Erzeuge Einträge...", false, false);

            //generate random guid path and append it to the path var
            Guid g = Guid.NewGuid();
            string guidFolder = g.ToString();

            //create and open archive
            Common.IArchive archive;
            
            switch (compressionType)
            {
                case ConfigHandler.Compression.zip:
                    archive = new Common.ZipArchive(System.IO.Path.Combine(path, guidFolder + ".nxm"), this.newEvent);
                    break;
                case ConfigHandler.Compression.lz4:
                    archive = new Common.LZ4Archive(System.IO.Path.Combine(path, guidFolder + ".nxm"), this.newEvent);
                    break;
                default: //default fallback to zip algorithm
                    archive = new Common.ZipArchive(System.IO.Path.Combine(path, guidFolder + ".nxm"), this.newEvent);
                    break;
            }
            
            archive.create();
            archive.open(System.IO.Compression.ZipArchiveMode.Create);

            
            bool hasHDD = false;

            raiseNewEvent("erfolgreich", true, false);

            //iterate hdds
            var iterator = currentSnapshot.GetRelated("Msvm_StorageAllocationSettingData").GetEnumerator();
            int hddCounter = 0;

            //get all hdds first (getRelated would delete the iterator when backup takes too long)
            List<ManagementObject> hdds = new List<ManagementObject>();
            while (iterator.MoveNext())
            {
                hdds.Add((ManagementObject)iterator.Current);
            }

            //now export the hdds
            foreach (ManagementObject hdd in hdds)
            {
                string[] hddPath = (string[])hdd["HostResource"];
                if (hddPath[0].EndsWith(".iso"))
                {
                    continue;
                }

                hasHDD = true; //indicates that the vm has at least one snapshotable vhd

                //copy a full snapshot?
                if (rctBase == null)
                {
                    //just raise event by first iteration
                    if(hddCounter == 0)
                    {
                        raiseNewEvent("Beginne Vollbackup", false, false);
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
                        raiseNewEvent("Beginne inkrementielles Backup", false, false);
                    }
                    
                    //do a rct backup copy
                    performrctbackup(hddPath[0], ((string[])rctBase["ResilientChangeTrackingIdentifiers"])[hddCounter], archive, compressionType);

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

            //convert the snapshot to a reference point
            ManagementObject refP = this.convertToReferencePoint(currentSnapshot);

            //write backup xml
            string parentiid = "";
            if (rctBase != null)
            {
                parentiid = (string)rctBase["InstanceId"];
            }

            ConfigHandler.BackupConfigHandler.addBackup(basePath, guidFolder, backupType, (string)refP["InstanceId"], parentiid, false);

        }


        //performs a rct backup copy
        private void performrctbackup(string snapshothddPath, string rctID, Common.IArchive archive, ConfigHandler.Compression compressionType)
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
                    DiffHandler diffWriter = new DiffHandler(this.newEvent);

                    diffWriter.writeDiffFile(changedBlocks, diskHandler, archive, compressionType, bufferSize, System.IO.Path.GetFileName(snapshothddPath));

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
            using (ManagementObject vm = WmiUtilities.GetVirtualMachine(vmName, scope))
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
            using (ManagementObject vm = WmiUtilities.GetVirtualMachine(vmName, scope))
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
            using (ManagementObject vm = WmiUtilities.GetVirtualMachine(vmName, scope))
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

            Console.WriteLine("done cleanup");
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
