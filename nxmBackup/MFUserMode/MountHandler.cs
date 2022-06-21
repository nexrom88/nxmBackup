using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HyperVBackupRCT;
using HVRestoreCore;
using nxmBackup.HVBackupCore;
using System.Runtime.InteropServices;
using System.Management;
using Common;
using ConfigHandler;

namespace nxmBackup.MFUserMode
{
    public class MountHandler
    {
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice([In] string lpDeviceName, [Out] StringBuilder lpTargetPath, [In] int ucchMax);

        private MFUserMode kmConnection;
        private bool processStopped = false;
        private System.IO.FileStream destStream;
        private BackupChainReader[] readableChains;
        private string[] mountFiles;
        private RestoreMode restoreMode;
        private string lrVMID; //vm id, just for lr
        public ProcessState mountState;
        private bool useEncryption;
        private byte[] aesKey;
        private bool usingDedupe;

        public MountHandler(RestoreMode mode, bool useEncryption, byte[] aesKey, bool usingDedupe)
        {
            this.useEncryption = useEncryption;
            this.aesKey = aesKey;
            this.restoreMode = mode;
            this.usingDedupe = usingDedupe;
        }

        public string[] MountFiles { get => mountFiles; }

        public string LrVMID { get => lrVMID; }

        //starts the mount process for LR
        public void startMfHandlingForLR(BackupConfigHandler.LRBackupChains sourceFiles, string basePath, string vmName, UInt64 lbTimeLimit)
        {
            string mountDirectory = String.Empty;
            this.readableChains = new BackupChainReader[sourceFiles.chains.Length];
            this.mountFiles = new string[sourceFiles.chains.Length];

            //build readable backup chains structure first
            for (int i = 0; i < sourceFiles.chains.Length; i++)
            {
                this.readableChains[i] = buildReadableBackupChain(sourceFiles.chains[i].files);

                //chain could not be read?
                if (this.readableChains[i] == null)
                {
                    DBQueries.addLog("LR: readable chain could not be built", Environment.StackTrace, null);
                    mountState = ProcessState.error;
                    return;
                }

                ulong decompressedFileSize = 0;
                //open source file and read "decompressed file size" (first 8 bytes) when LR on full backup
                if (sourceFiles.chains[i].files.Length == 1)
                {
                    FileStream sourceStream = new System.IO.FileStream(sourceFiles.chains[i].files[sourceFiles.chains[i].files.Length - 1], System.IO.FileMode.Open, System.IO.FileAccess.Read);
                    byte[] buffer = new byte[8];

                    //read iv length to jump over iv
                    sourceStream.Read(buffer, 0, 4);
                    int ivLength = BitConverter.ToInt32(buffer, 0);
                    sourceStream.Seek(ivLength + 16, SeekOrigin.Current); //+16 to jump over signature too

                    sourceStream.Read(buffer, 0, 8);
                    decompressedFileSize = BitConverter.ToUInt64(buffer, 0);
                    sourceStream.Close();
                    sourceStream.Dispose();
                }
                else if (sourceFiles.chains[i].files[0].EndsWith(".cb"))
                {
                    //read "decompressed file size" from first rct backup
                    decompressedFileSize = this.readableChains[i].NonFullBackups[0].cbStructure.vhdxSize;
                }
                else if (sourceFiles.chains[i].files[0].EndsWith("lb"))
                {
                    //read "decompressed file size" from first lb backup
                    decompressedFileSize = this.readableChains[i].NonFullBackups[0].lbStructure.vhdxSize;
                }

                //get vhdx file name
                string[] stringSplitter = sourceFiles.chains[i].files[sourceFiles.chains[i].files.Length - 1].Split("\\".ToCharArray());
                string vhdxName = stringSplitter[stringSplitter.Length - 1];

                this.mountFiles[i] = getMountHDDPath(decompressedFileSize, vhdxName);
                if (this.mountFiles == null)
                {
                    DBQueries.addLog("LR: mount file could not be created", Environment.StackTrace, null);
                    mountState = ProcessState.error;
                    return;
                }

                //build dummy dest vhdx file
                try
                {
                    string hddDirectory = System.IO.Path.GetDirectoryName(this.MountFiles[i]);
                    mountDirectory = System.IO.Directory.GetParent(hddDirectory).FullName;
                    System.IO.Directory.CreateDirectory(hddDirectory);
                    this.destStream = new System.IO.FileStream(this.MountFiles[i], System.IO.FileMode.Create, System.IO.FileAccess.Write);
                    this.destStream.SetLength((long)decompressedFileSize);
                    this.destStream.Close();
                    this.destStream.Dispose();
                }catch(Exception ex)
                {
                    DBQueries.addLog("error on creating dummy dest file", Environment.StackTrace, ex);
                    mountState = ProcessState.error;
                    foreach (string file in this.mountFiles)
                    {
                        if (System.IO.File.Exists(file))
                        {
                            System.IO.File.Delete(file);
                        }
                    }
                    return;
                }
            }

            //restore vm config files            
            string configFile;
            try
            {
                configFile = transferVMConfig(basePath, mountDirectory);

            }
            catch (Exception ex)
            {
                DBQueries.addLog("LR: error on transfering vm config files", Environment.StackTrace, ex);
                mountState = ProcessState.error;
                foreach (string file in this.mountFiles)
                {
                    System.IO.File.Delete(file);
                }
                return;
            }

            //return if no config file found
            if (configFile == "")
            {
                DBQueries.addLog("LR: no config file found", Environment.StackTrace, null);
                mountState = ProcessState.error;
                foreach (string file in this.mountFiles)
                {
                    System.IO.File.Delete(file);
                }
                return;
            }


            
            //connect to MF Kernel Mode
            this.kmConnection = new MFUserMode(this.readableChains, lbTimeLimit, this.useEncryption, this.aesKey);
            if (this.kmConnection.connectToKM("\\nxmLRPort", "\\BaseNamedObjects\\nxmmflr"))
            {
                //send target vhdx path to km
                sendVHDXTargetPathsToKM(this.mountFiles);

                //import to hyperv
                System.Threading.Thread mountThread = new System.Threading.Thread(() => startImportVMProcess(configFile, mountDirectory, true, vmName + "_LiveRestore"));
                mountThread.Start();


                while (!this.processStopped)
                {
                    this.kmConnection.handleLRMessage();
                }


            }
            else
            {
                DBQueries.addLog("LR: Failed to connect to KM", Environment.StackTrace, null);
                this.mountState = ProcessState.error;
            }
        }

        //starts the vm import process
        private void startImportVMProcess(string configFile, string mountDirectory, bool newId, string newName)
        {
            System.Threading.Thread.Sleep(1000);

            //import vm to hyperv
            try
            {
                string vmID = HVRestoreCore.VMImporter.importVM(configFile, mountDirectory, newId, newName);
                this.lrVMID = vmID;

                //create helper snapshot to redirect writes to avhdx file
                createHelperSnapshot(vmID);
                this.mountState = ProcessState.successful;
            }
            catch (Exception ex)
            {
                DBQueries.addLog("LR: importing vm failed", Environment.StackTrace, ex);
                this.mountState = ProcessState.error;
            }

        }

        //create helper snapshot
        private void createHelperSnapshot(string vmID)
        {
            const UInt16 SnapshotTypeRecovery = 32768;
            ManagementScope scope = new ManagementScope("\\\\localhost\\root\\virtualization\\v2", null);

            // Get the management service and the VM object.
            using (ManagementObject vm = WmiUtilities.GetVirtualMachine(vmID, scope))
            using (ManagementObject service = WmiUtilities.GetVirtualMachineSnapshotService(scope))
            using (ManagementObject settings = WmiUtilities.GetVirtualMachineSnapshotSettings(scope))
            using (ManagementBaseObject inParams = service.GetMethodParameters("CreateSnapshot"))
            {
                //set settings
                settings["ConsistencyLevel"] = ConsistencyLevel.CrashConsistent;
                settings["IgnoreNonSnapshottableDisks"] = true;
                inParams["AffectedSystem"] = vm.Path.Path;
                inParams["SnapshotSettings"] = settings.GetText(TextFormat.WmiDtd20);
                inParams["SnapshotType"] = SnapshotTypeRecovery;

                using (ManagementBaseObject outParams = service.InvokeMethod(
                    "CreateSnapshot",
                    inParams,
                    null))
                {
                    //wait for the snapshot to be created
                    WmiUtilities.ValidateOutput(outParams, scope);

                }


            }
        }


        //sends a target vhdx to km for lr
        private void sendVHDXTargetPathsToKM(string[] paths)
        {
            byte pathsCount;
            if (paths.Length >= 254)
            {
                pathsCount = 254;
            }
            else
            {
                pathsCount = (byte)paths.Length;
            }

            //send paths count
            byte[] pathsCountBuffer = new byte[1];
            pathsCountBuffer[0] = (byte)pathsCount;

            //send pathsCountBuffer to km
            this.kmConnection.writeMessage(pathsCountBuffer);


            //send paths
            foreach (string path in paths)
            {
                //replace driveletter with DOS device path
                string dosPath = replaceDriveLetterByDevicePath(path);

                //get unicode bytes
                byte[] buffer = Encoding.Unicode.GetBytes(dosPath);

                //build send buffer, to zero-terminate unicode string
                byte[] sendBuffer = new byte[buffer.Length + 2];
                Array.Copy(buffer, sendBuffer, buffer.Length);
                sendBuffer[sendBuffer.Length - 1] = 0;
                sendBuffer[sendBuffer.Length - 2] = 0;


                this.kmConnection.writeMessage(sendBuffer);
            }
        }

        //replaces the drive letter with nt device path
        private string replaceDriveLetterByDevicePath(string path)
        {
            StringBuilder builder = new StringBuilder(255);
            QueryDosDevice(path.Substring(0, 2), builder, 255);
            path = path.Substring(3);
            return builder.ToString() + "\\" + path;
        }

        //transfer vm config files from backup archive and return vmcx file
        public string transferVMConfig(string archivePath, string destination)
        {
            List<string> hddFiles = new List<string>();

            string configFile = "";

            Common.IArchive archive;


            archive = new Common.LZ4Archive(archivePath, null, this.useEncryption, this.aesKey, this.usingDedupe, null);


            archive.open(System.IO.Compression.ZipArchiveMode.Read);

            //get all archive entries
            List<string> entries = archive.listEntries();

            //iterate through all entries
            foreach (string entry in entries)
            {

                //ignore vhdx files
                if (entry.EndsWith(".vhdx"))
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

                //is config file?
                if (fileDestination.EndsWith(".vmcx", StringComparison.OrdinalIgnoreCase))
                {
                    configFile = fileDestination;
                }

            }

            archive.close();
            return configFile;
        }

        //starts the mount process for FLR
        public void startMfHandlingForFLR(string[] sourceFiles, ref ProcessState mountState, UInt64 lbTimeLimit)
        {
            //build readable backup chain structure first
            this.readableChains = new BackupChainReader[1]; //on flr there can be just one chain
            this.mountFiles = new string[1];
            this.readableChains[0] = buildReadableBackupChain(sourceFiles);

            //check if chain si uilt properly
            if (this.readableChains[0] == null)
            {
                //error on building chain
                DBQueries.addLog("FLR: could not build readable chain", Environment.StackTrace, null);
                mountState = ProcessState.error;
                return;
            }

            ulong decompressedFileSize = 0;
            //open source file and read "decompressed file size" when flr on full backup
            if (sourceFiles.Length == 1)
            {
                FileStream sourceStream = new System.IO.FileStream(sourceFiles[sourceFiles.Length - 1], System.IO.FileMode.Open, System.IO.FileAccess.Read);
                byte[] buffer = new byte[8];

                //read IV length
                sourceStream.Read(buffer, 0, 4);
                int ivLength = BitConverter.ToInt32(buffer, 0);

                //jump over iv and signature (16 bytes)
                sourceStream.Seek(ivLength + 16, SeekOrigin.Current);

                sourceStream.Read(buffer, 0, 8);
                decompressedFileSize = BitConverter.ToUInt64(buffer, 0);
                sourceStream.Close();
                sourceStream.Dispose();
            }
            else if (sourceFiles[0].EndsWith(".cb"))
            {
                //read "decompressed file size" from first rct backup
                decompressedFileSize = this.readableChains[0].NonFullBackups[0].cbStructure.vhdxSize;
            }
            else if (sourceFiles[0].EndsWith("lb"))
            {
                //read "decompressed file size" from first lb backup
                decompressedFileSize = this.readableChains[0].NonFullBackups[0].lbStructure.vhdxSize;
            }

            this.mountFiles[0] = getMountHDDPath(decompressedFileSize, "mount.vhdx");
            if (this.mountFiles == null || this.mountFiles.Length == 0 || this.mountFiles[0] == null)
            {
                mountState = ProcessState.error;
                return;
            }

            //build dummy dest file
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(this.MountFiles[0]));
                this.destStream = new System.IO.FileStream(this.MountFiles[0], System.IO.FileMode.Create, System.IO.FileAccess.Write);
            }catch(Exception ex)
            {
                DBQueries.addLog("FLR: Failed to create dummy file", Environment.StackTrace, null);
                mountState = ProcessState.error;
                return;
            }

            this.destStream.SetLength((long)decompressedFileSize);
            this.destStream.Close();
            this.destStream.Dispose();

            //connect to MF Kernel Mode
            this.kmConnection = new MFUserMode(this.readableChains, lbTimeLimit, this.useEncryption, this.aesKey);
            if (this.kmConnection.connectToKM("\\nxmFLRPort", "\\BaseNamedObjects\\nxmmfflr"))
            {
                mountState = ProcessState.successful;

                while (!this.processStopped)
                {
                    //try
                    //{
                        this.kmConnection.handleFLRMessage();
                    //}
                    //catch (Exception ex)
                    //{
                    //    DBQueries.addLog("FLR: Error on handling flr message", Environment.StackTrace, ex);
                    //    mountState = ProcessState.error;
                    //}
                }
            }
            else
            {
                DBQueries.addLog("FLR: Failed to connect to KM", Environment.StackTrace, null);
                mountState = ProcessState.error;
            }
        }

        //gets the path to the user given mount path with enough hdd space and a given file name
        private string getMountHDDPath(ulong fileSize, string vhdxName)
        {
            //read mountPath from DB
            string mountPathObj = DBQueries.readGlobalSetting("mountpath");
            string mountPath;

            //mount path not valid?
            if (mountPathObj == null)
            {
                Common.DBQueries.addLog("mountpath cannot be read from DB", Environment.StackTrace, null);
                return null;
            }
            else
            {
                //parse object to string
                mountPath = mountPathObj;
            }

            //check if drive-path exists
            if (!Directory.Exists(Path.GetPathRoot(mountPath)))
            {
                Common.DBQueries.addLog("drive for mountpath does not exist", Environment.StackTrace, null);
                return null;
            }

            DriveInfo drive = new DriveInfo((Path.GetPathRoot(mountPath)));

            try
            {
                //remaining free space has to be 1GB
                if (drive.AvailableFreeSpace - (long)fileSize > 1000000000)
                {
                    //try to create mountfolder
                    string combinedPath = System.IO.Path.Combine(mountPath, "nxmMount");
                    System.IO.Directory.CreateDirectory(combinedPath);
                    string mountFile = System.IO.Path.Combine(combinedPath, "Virtual Hard Disks\\" + vhdxName);
                    return mountFile;
                }
                else
                {
                    //not enough free disk space
                    Common.DBQueries.addLog("not enough free disk space for creating mount file", Environment.StackTrace, null);
                }

            }
            catch (Exception ex) //catch for drives not ready
            {
                Common.DBQueries.addLog("cannot create mount path", Environment.StackTrace, ex);
            }


            return null;
        }

        //builds a readable backup chain structure
        public BackupChainReader buildReadableBackupChain(string[] sourceFiles)
        {
            BackupChainReader chain = new BackupChainReader();
            chain.AesKey = this.aesKey;
            chain.NonFullBackups = new List<BackupChainReader.ReadableNonFullBackup>();

            //iterate through cb files first
            for (int i = 0; i < sourceFiles.Length - 1; i++)
            {
                BackupChainReader.ReadableNonFullBackup nonFullBackup = new BackupChainReader.ReadableNonFullBackup();

                //get type by file extension

                if (sourceFiles[i].EndsWith(".cb"))
                {
                    try
                    {
                        //parse cb file
                        FileStream inputStream = new FileStream(sourceFiles[i], FileMode.Open, FileAccess.Read);
                        BlockCompression.LZ4BlockStream blockStream = new BlockCompression.LZ4BlockStream(inputStream, BlockCompression.AccessMode.read, this.useEncryption, this.aesKey, this.usingDedupe);
                        if (!blockStream.init())
                        {
                            Common.DBQueries.addLog("Error while initializing backup source file (cb)", Environment.StackTrace, null);
                            return null;
                        }
                        CbStructure cbStruct = CBParser.parseCBFile(blockStream, false);

                        nonFullBackup.cbStructure = cbStruct;
                        nonFullBackup.backupType = BackupChainReader.NonFullBackupType.rct;
                        nonFullBackup.sourceStreamRCT = blockStream;
                    }
                    catch(Exception ex)
                    {
                        Common.DBQueries.addLog("Error while opening backup source file (cb)", Environment.StackTrace, ex);
                        return null;
                    }                    

                }
                else if (sourceFiles[i].EndsWith(".lb"))
                {
                    FileStream inputStream = new FileStream(sourceFiles[i], FileMode.Open, FileAccess.Read);
                    LBStructure lbStruct = LBParser.parseLBFile(inputStream, false);
                    nonFullBackup.lbStructure = lbStruct;
                    nonFullBackup.backupType = BackupChainReader.NonFullBackupType.lb;
                    nonFullBackup.sourceStreamLB = inputStream;

                }

                chain.NonFullBackups.Add(nonFullBackup);
            }

            //build readable full backup
            BackupChainReader.ReadableFullBackup readableFullBackup = new BackupChainReader.ReadableFullBackup();

            FileStream inputStreamFull;
            try
            {
                inputStreamFull = new FileStream(sourceFiles[sourceFiles.Length - 1], FileMode.Open, FileAccess.Read);
            }
            catch (Exception ex)
            {
                //source file not accesible
                Common.DBQueries.addLog("Error while opening backup source file", Environment.StackTrace, ex);
                return null;
            }

            BlockCompression.LZ4BlockStream blockStreamFull = new BlockCompression.LZ4BlockStream(inputStreamFull, BlockCompression.AccessMode.read, this.useEncryption, this.aesKey, this.usingDedupe);
            if (!blockStreamFull.init())
            {
                return null;
            }

            readableFullBackup.sourceStream = blockStreamFull;
            chain.FullBackup = readableFullBackup;

            return chain;

        }

        //stops the mount process
        public void stopMfHandling()
        {
            this.processStopped = true;

            //close km connection
            if (this.kmConnection != null)
            {
                this.kmConnection.closeConnection(false);
            }

            //iterate through all rct backups in all chains
            foreach (BackupChainReader backupChain in this.readableChains)
            {
                if (backupChain != null)
                {
                    foreach (BackupChainReader.ReadableNonFullBackup nonFullBackup in backupChain.NonFullBackups)
                    {
                        switch (nonFullBackup.backupType)
                        {
                            case BackupChainReader.NonFullBackupType.lb:
                                nonFullBackup.sourceStreamLB.Close();
                                break;
                            case BackupChainReader.NonFullBackupType.rct:
                                nonFullBackup.sourceStreamRCT.Close();
                                break;
                        }

                    }
                }

                //close full backup if necessary
                if (backupChain != null && backupChain.FullBackup.sourceStream != null)
                {
                    backupChain.FullBackup.sourceStream.Close();
                }
            }



            //delete dummy files
            if (this.restoreMode == RestoreMode.flr)
            {
                try
                {
                    foreach (string file in this.mountFiles)
                    {
                        System.IO.File.Delete(file);
                    }
                }
                catch
                {
                    //ignore when mount file cannot be deleted
                }
            }

            if (this.restoreMode == RestoreMode.lr)
            {
                foreach (string file in this.mountFiles)
                {
                    if (file != null)
                    {
                        string mountDir = Directory.GetParent(System.IO.Path.GetDirectoryName(file)).FullName;

                        try
                        {
                            System.IO.Directory.Delete(mountDir + "\\Virtual Hard Disks", true);
                            System.IO.Directory.Delete(mountDir + "\\Virtual Machines", true);
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }
            }

        }

        public enum ProcessState
        {
            pending,
            successful,
            error
        }

        public enum RestoreMode
        {
            lr,
            flr
        }

        public struct WriteCache
        {
            public List<WriteCachePosition> positions;
            public System.IO.FileStream writeCacheStream;
        }

        public struct WriteCachePosition
        {
            public UInt64 offset;
            public UInt64 length;
            public UInt64 filePosition;
        }





    }


}
