using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperVBackupRCT;
using HVBackupCore;

namespace MFUserMode
{
    public class MountHandler
    {

        private MFUserMode kmConnection;
        private bool processStopped = false;
        private System.IO.FileStream destStream;
        private string destDummyFile;
        private BackupChainReader readableChain;

        //starts the mount process
        public void startMfHandling (string[] sourceFiles, string destDummyFile, ref mountState mountState)
        {
            this.destDummyFile = destDummyFile;

            //build readable backup chain structure first
            this.readableChain = buildReadableBackupChain(sourceFiles);

            ulong decompressedFileSize;
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
            else
            {
                //read "decompressed file size" from first rct backup
                decompressedFileSize = this.readableChain.RCTBackups[0].cbStructure.vhdxSize;
            }

            //build dummy dest file
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destDummyFile));
            this.destStream = new System.IO.FileStream(destDummyFile, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            this.destStream.SetLength((long)decompressedFileSize);
            this.destStream.Close();
            this.destStream.Dispose();

            //connect to MF Kernel Mode
            this.kmConnection = new MFUserMode(this.readableChain);
            if (this.kmConnection.connectToKM())
            {
                mountState = mountState.connected;

                while (!this.processStopped)
                {
                    this.kmConnection.readMessages();
                }
            }
            else
            {
                mountState = mountState.error;
            }
        }

        //builds a readable backup chain structure
        public BackupChainReader buildReadableBackupChain(string[] sourceFiles)
        {
            BackupChainReader chain = new BackupChainReader();
            chain.RCTBackups = new List<BackupChainReader.ReadableRCTBackup>();

            //iterate through cb files first
            for (int i = 0; i < sourceFiles.Length - 1; i++)
            {
                BackupChainReader.ReadableRCTBackup rctBackup = new BackupChainReader.ReadableRCTBackup();
                
                //parse cb file
                CbStructure cbStruct = CBParser.parseCBFile(sourceFiles[i]);
                rctBackup.cbStructure = cbStruct;

                //open input stream
                FileStream inputStream = new FileStream(sourceFiles[i], FileMode.Open, FileAccess.Read);
                BlockCompression.LZ4BlockStream blockStream = new BlockCompression.LZ4BlockStream(inputStream, BlockCompression.AccessMode.read);
                rctBackup.sourceStream = blockStream;
                chain.RCTBackups.Add(rctBackup);
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
            foreach (BackupChainReader.ReadableRCTBackup rctBackup in this.readableChain.RCTBackups)
            {
                rctBackup.sourceStream.Close();
            }

            //close full backup
            this.readableChain.FullBackup.sourceStream.Close();

            //close km connection
            this.kmConnection.closeConnection();

            //delete dummy file
            System.IO.File.Delete(this.destDummyFile);
        }
    
        public enum mountState
        {
            pending,
            connected,
            error
        }

       

        

    }


}
