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
        private bool isReady;
        private System.IO.FileStream destStream;

        //starts the mount process
        public void startMountProcess (string sourceFile, string destDummyFile, ref mountState mountState)
        {
            //open source file and read "decompressed file size" (first 8 bytes)
            System.IO.FileStream sourceStream = new System.IO.FileStream(sourceFile, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            byte[] buffer = new byte[8];
            sourceStream.Read(buffer, 0, 8);
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
            sourceStream = new System.IO.FileStream(sourceFile, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            BlockCompression.LZ4BlockStream blockStream = new BlockCompression.LZ4BlockStream(sourceStream, BlockCompression.AccessMode.read);

            this.kmConnection = new MFUserMode(blockStream);
            if (this.kmConnection.connectToKM())
            {
                mountState = mountState.connected;

                for (; ; )
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
        public void stopMountProcess()
        {
            this.kmConnection.closeConnection();
        }
    
        public enum mountState
        {
            pending,
            connected,
            error
        }

    }


}
