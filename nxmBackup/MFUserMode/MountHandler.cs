using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperVBackupRCT;
using HVBackupCore;
using nxmBackup.HVBackupCore;
using System.Linq.Expressions;
using System.Net.Http.Headers;

namespace nxmBackup.MFUserMode
{
    public class MountHandler
    {

        private MFUserMode kmConnection;
        private bool processStopped = false;
        private System.IO.FileStream destStream;
        private BackupChainReader readableChain;
        private string mountFile;

        public string MountFile { get => mountFile; }

        //starts the mount process for LR
        public void startMfHandlingForLR(string[] sourceFiles, ref mountState mountState)
        {
            //build readable backup chain structure first
            this.readableChain = buildReadableBackupChain(sourceFiles);

            ulong decompressedFileSize = 0;
            //open source file and read "decompressed file size" (first 8 bytes) when LR on full backup
            if (sourceFiles.Length == 1)
            {
                FileStream sourceStream = new System.IO.FileStream(sourceFiles[sourceFiles.Length - 1], System.IO.FileMode.Open, System.IO.FileAccess.Read);
                byte[] buffer = new byte[8];
                sourceStream.Read(buffer, 0, 8);
                decompressedFileSize = BitConverter.ToUInt64(buffer, 0);
                sourceStream.Close();
                sourceStream.Dispose();
            }
            else if (sourceFiles[0].EndsWith(".cb"))
            {
                //read "decompressed file size" from first rct backup
                decompressedFileSize = this.readableChain.NonFullBackups[0].cbStructure.vhdxSize;
            }
            else if (sourceFiles[0].EndsWith("lb"))
            {
                //read "decompressed file size" from first lb backup
                decompressedFileSize = this.readableChain.NonFullBackups[0].lbStructure.vhdxSize;
            }

            this.mountFile = getMountHDDPath(decompressedFileSize);
            if (this.mountFile == null)
            {
                mountState = mountState.error;
                return;
            }

            //build dummy dest vhdx file
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(this.MountFile));
            this.destStream = new System.IO.FileStream(this.MountFile, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            this.destStream.SetLength((long)decompressedFileSize);
            this.destStream.Close();
            this.destStream.Dispose();

            //restore vm config files
            transferVMConfig()

            //connect to MF Kernel Mode
            this.kmConnection = new MFUserMode(this.readableChain);
            if (this.kmConnection.connectToKM("\\nxmLRPort", "\\BaseNamedObjects\\nxmmflr"))
            {
                mountState = mountState.connected;

                while (!this.processStopped)
                {
                    this.kmConnection.handleLRMessage();
                }
            }
            else
            {
                mountState = mountState.error;
            }
        }

        //transfer vm config files from backup archive
        public List<string> transferVMConfig(string archivePath, string destination)
        {
            List<string> hddFiles = new List<string>();

            Common.IArchive archive;


            archive = new Common.LZ4Archive(archivePath, null);


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

                //add to return list if vhdx
                if (fileDestination.EndsWith(".vhdx"))
                {
                    hddFiles.Add(fileDestination);
                }
            }

            archive.close();
            return hddFiles;
        }

        //starts the mount process for FLR
        public void startMfHandlingForFLR (string[] sourceFiles, ref mountState mountState)
        {
            //build readable backup chain structure first
            this.readableChain = buildReadableBackupChain(sourceFiles);

            ulong decompressedFileSize = 0;
            //open source file and read "decompressed file size" (first 8 bytes) when flr on full backup
            if (sourceFiles.Length == 1)
            {
                FileStream sourceStream = new System.IO.FileStream(sourceFiles[sourceFiles.Length - 1], System.IO.FileMode.Open, System.IO.FileAccess.Read);
                byte[] buffer = new byte[8];
                sourceStream.Read(buffer, 0, 8);
                decompressedFileSize = BitConverter.ToUInt64(buffer, 0);
                sourceStream.Close();
                sourceStream.Dispose();
            }
            else if (sourceFiles[0].EndsWith(".cb"))
            {
                //read "decompressed file size" from first rct backup
                decompressedFileSize = this.readableChain.NonFullBackups[0].cbStructure.vhdxSize;
            }else if (sourceFiles[0].EndsWith("lb"))
            {
                //read "decompressed file size" from first lb backup
                decompressedFileSize = this.readableChain.NonFullBackups[0].lbStructure.vhdxSize;
            }

            this.mountFile = getMountHDDPath(decompressedFileSize);
            if (this.mountFile == null)
            {
                mountState = mountState.error;
                return;
            }

            //build dummy dest file
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(this.MountFile));
            this.destStream = new System.IO.FileStream(this.MountFile, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            this.destStream.SetLength((long)decompressedFileSize);
            this.destStream.Close();
            this.destStream.Dispose();

            //connect to MF Kernel Mode
            this.kmConnection = new MFUserMode(this.readableChain);
            if (this.kmConnection.connectToKM("\\nxmFLRPort", "\\BaseNamedObjects\\nxmmfflr"))
            {
                mountState = mountState.connected;

                while (!this.processStopped)
                {
                    this.kmConnection.handleFLRMessage();
                }
            }
            else
            {
                mountState = mountState.error;
            }
        }

        //gets the path to a mount path with enough hdd space
        private string getMountHDDPath(ulong fileSize)
        {
            //iterate through all drives with a drive letter
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    //remaining free space has to be 1GB
                    if (drive.AvailableFreeSpace - (long)fileSize > 1000000000)
                    {
                        //try to create mountfolder
                        System.IO.Directory.CreateDirectory(drive.Name + "nxmMount\\Virtual Hard Disks");
                        mountFile = drive.Name + "nxmMount\\Virtual Hard Disks\\mount.vhdx";
                        return mountFile;
                    }

                }
                catch (Exception ex) //catch for drives not ready
                {
                }
            }

            return null;
        }

        //builds a readable backup chain structure
        public BackupChainReader buildReadableBackupChain(string[] sourceFiles)
        {
            BackupChainReader chain = new BackupChainReader();
            chain.NonFullBackups = new List<BackupChainReader.ReadableNonFullBackup>();

            //iterate through cb files first
            for (int i = 0; i < sourceFiles.Length - 1; i++)
            {
                BackupChainReader.ReadableNonFullBackup nonFullBackup = new BackupChainReader.ReadableNonFullBackup();

                //get type by file extension

                if (sourceFiles[i].EndsWith(".cb"))
                {
                    //parse cb file
                    CbStructure cbStruct = CBParser.parseCBFile(sourceFiles[i], true);
                    nonFullBackup.cbStructure = cbStruct;
                    nonFullBackup.backupType = BackupChainReader.NonFullBackupType.rct;
                    FileStream inputStream = new FileStream(sourceFiles[i], FileMode.Open, FileAccess.Read);
                    BlockCompression.LZ4BlockStream blockStream = new BlockCompression.LZ4BlockStream(inputStream, BlockCompression.AccessMode.read);
                    nonFullBackup.sourceStreamRCT = blockStream;

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
            FileStream inputStreamFull = new FileStream(sourceFiles[sourceFiles.Length - 1], FileMode.Open, FileAccess.Read);
            BlockCompression.LZ4BlockStream blockStreamFull = new BlockCompression.LZ4BlockStream(inputStreamFull, BlockCompression.AccessMode.read);
            readableFullBackup.sourceStream = blockStreamFull;
            chain.FullBackup = readableFullBackup;

            return chain;

        }

        //stops the mount process
        public void stopMfHandling()
        {
            this.processStopped = true;
            
            //iterate through all rct backups
            foreach (BackupChainReader.ReadableNonFullBackup nonFullBackup in this.readableChain.NonFullBackups)
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

            //close full backup
            this.readableChain.FullBackup.sourceStream.Close();

            //close km connection
            this.kmConnection.closeConnection();

            //delete dummy file
            System.IO.File.Delete(this.MountFile);
        }
    
        public enum mountState
        {
            pending,
            connected,
            error
        }

       

        

    }


}
