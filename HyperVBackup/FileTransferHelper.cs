using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace HyperVBackup
{
    class FileTransferHelper
    {
        private string sourceFile, destFile;

        public FileTransferHelper(string sourceFile, string destFile)
        {
            this.sourceFile = sourceFile;
            this.destFile = destFile;
        }

        //starts the transfer with exception handling
        public void startTransfer()
        {
            try
            {
                doTransfer();
            }catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }


        //transfers the given file to its destination
        private void doTransfer(){
            long bytesToTransfer;
            long bytesTransfered = 0;
            FileStream outStream = new FileStream(this.destFile, FileMode.Create, FileAccess.Write);
            FileStream inStream = new FileStream(this.sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            bytesToTransfer = inStream.Length;
            long bufferSize = 512;
            byte[] buffer = new byte[bufferSize];
            double lastPercentage = -1.0;
            //read all bytes
            while (bytesToTransfer > bytesTransfered)
            {
                //last read?
                if (bytesToTransfer - bytesTransfered < bufferSize)
                {
                    bufferSize = bytesToTransfer - bytesTransfered;
                }

                //read the buffersize
                inStream.Read(buffer, 0, (int)bufferSize);

                //write the buffer to the destination file
                outStream.Write(buffer, 0, (int)bufferSize);
                bytesTransfered += bufferSize;

                //calculate the progress
                double percentage = ((float)bytesTransfered / (float)bytesToTransfer) * 100.0;
                percentage = Math.Round(percentage, 2);
                if (lastPercentage != percentage)
                {
                    lastPercentage = percentage;
                    Console.WriteLine(percentage + "%");
                }
            }

            //done
            inStream.Close();
            outStream.Close();
        }



    }
}
