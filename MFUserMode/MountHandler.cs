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

        //starts the mount process
        public void startMountProcess (string sourceFile, string destDummyFile)
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
            System.IO.FileStream destStream = new System.IO.FileStream(destDummyFile, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            destStream.SetLength((long)decompressedFileSize);
            destStream.Close();
            destStream.Dispose();

            //connect to MF Kernel Mode
            sourceStream = new System.IO.FileStream(sourceFile, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            BlockCompression.LZ4BlockStream blockStream = new BlockCompression.LZ4BlockStream(sourceStream, BlockCompression.AccessMode.read);

            this.kmConnection = new MFUserMode(blockStream);
            if (this.kmConnection.connectToKM())
            {
                for (; ; )
                {
                    this.kmConnection.readMessages();
                }
            }
        }

        //stops the mount process
        public void stopMountProcess()
        {
            this.kmConnection.closeConnection();
        }
    }
}
