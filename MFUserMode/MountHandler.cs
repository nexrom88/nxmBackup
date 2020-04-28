using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFUserMode
{
    public class MountHandler
    {

        private MFUserMode kmConnection;
        private bool processStopped = false;
        private System.IO.FileStream destStream;
        private string destDummyFile;
        System.IO.FileStream sourceStream;
        BlockCompression.LZ4BlockStream blockStream;

        //starts the mount process
        public void startMfHandling (string sourceFile, string destDummyFile, ref mountState mountState)
        {
            this.destDummyFile = destDummyFile;

            //open source file and read "decompressed file size" (first 8 bytes)
            this.sourceStream = new System.IO.FileStream(sourceFile, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            byte[] buffer = new byte[8];
            this.sourceStream.Read(buffer, 0, 8);
            ulong decompressedFileSize = BitConverter.ToUInt64(buffer, 0);
            sourceStream.Close();
            sourceStream.Dispose();

            //build dummy dest file
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destDummyFile));
            this.destStream = new System.IO.FileStream(destDummyFile, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            this.destStream.SetLength((long)decompressedFileSize);
            this.destStream.Close();
            this.destStream.Dispose();

            //connect to MF Kernel Mode
            this.sourceStream = new System.IO.FileStream(sourceFile, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            this.blockStream = new BlockCompression.LZ4BlockStream(sourceStream, BlockCompression.AccessMode.read);
            this.blockStream.CachingMode = true;

            this.kmConnection = new MFUserMode(blockStream);
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

        //stops the mount process
        public void stopMfHandling()
        {
            this.processStopped = true;
            this.blockStream.Close();
            this.sourceStream.Close();
            this.kmConnection.closeConnection();
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
