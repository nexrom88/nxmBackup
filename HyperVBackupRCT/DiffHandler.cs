using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HyperVBackupRCT
{
    class DiffHandler
    {
        private Common.Job.newEventDelegate newEvent;

        public DiffHandler(Common.Job.newEventDelegate newEventDelegate)
        {
            this.newEvent = newEventDelegate;
        }

        //writes the diff file using cbt information
        //important: bufferSize has to by a multiple of vhd sector size
        [Obsolete]
        public void writeDiffFile(ChangedBlock[] changedBlocks, VirtualDiskHandler diskHandler, Common.IArchive archive, ConfigHandler.Compression compressionType, ulong bufferSize, string hddName)
        {

            //calculate changed bytes count for progress calculation
            ulong totalBytesCount = 0;
            ulong bytesReadCount = 0;
            int lastPercentage = -1;
            foreach(ChangedBlock bl in changedBlocks)
            {
                totalBytesCount += bl.length;
            }
            raiseNewEvent("Erstelle Inkrement - 0%", false, false);

            //fetch disk file handle
            IntPtr diskHandle = diskHandler.getHandle();

            //open destination file
            Stream outStream = archive.createAndGetFileStream(hddName + ".cb");

            //open filestream to make it possible for readfile to read blocks (check why?)
            FileStream inputStream = new FileStream(diskHandle, FileAccess.Read, false, (int)bufferSize, true);

            //write block count to destination file
            outStream.Write(BitConverter.GetBytes((UInt32)changedBlocks.Length), 0, 4);

            ulong bytesRead;

            //read and write blocks
            foreach (ChangedBlock block in changedBlocks)
            {
                byte[] buffer;
                bytesRead = 0;

                //write block header to diff file
                //write block
                outStream.Write(BitConverter.GetBytes(block.offset), 0, 8); //write offset
                outStream.Write(BitConverter.GetBytes(block.length), 0, 8); //write length

                while (bytesRead < block.length)
                {
                    //still whole buffersize to read?
                    if (bytesRead + bufferSize <= block.length)
                    {
                        //read whole buffer size           
                        buffer = diskHandler.read(block.offset + bytesRead, bufferSize);
                        bytesRead += bufferSize;
                    }
                    else //end of block? read remaining bytes
                    {
                        ulong bytesRemaining = block.length - bytesRead;
                        buffer = diskHandler.read(block.offset + bytesRead, bytesRemaining);
                        bytesRead += bytesRemaining;
                    }

                    //write the current buffer to diff file
                    outStream.Write(buffer, 0, buffer.Length); //write data
                    bytesReadCount += (uint)buffer.Length;

                    //calculate progress
                    int percentage = (int)(((double)bytesReadCount / (double)totalBytesCount) * 100.0);

                    //new progress?
                    if (lastPercentage != percentage)
                    {
                        raiseNewEvent("Erstelle Inkrement - " + percentage + "%", false, true);
                        lastPercentage = percentage;
                    }
                }
            }

            raiseNewEvent("Erstelle Inkrement - 100%", false, true);

            //close destination stream
            GC.KeepAlive(inputStream);
            outStream.Close();

        }


        //merges a rct diff file with a vhdx
        public void merge(Stream diffStream, string destinationSnapshot)
        {
            raiseNewEvent("Verarbeite Inkrement...", false, false);

            //open file streams
            VirtualDiskHandler diskHandler = new VirtualDiskHandler(destinationSnapshot);
            diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.All);
            diskHandler.attach(VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST);

            //get sector size
            int sectorSize = (int)diskHandler.getSize().SectorSize;

            FileStream snapshotStream = new FileStream(diskHandler.getHandle(), FileAccess.Write, false, sectorSize, true);

            //read block count
            byte[] buffer = new byte[4];
            diffStream.Read(buffer, 0, 4);
            UInt32 blockCount = BitConverter.ToUInt32(buffer, 0);

            //restored bytes count for progress calculation
            long bytesRestored = 0;
            string lastProgress = "";

            //iterate through all blocks
            for (int i = 0; i < blockCount; i++)
            {
                ulong offset;
                ulong length;
                ulong originalLength;

                //read block offset
                buffer = new byte[8];
                diffStream.Read(buffer, 0, 8);
                offset = BitConverter.ToUInt64(buffer, 0);

                //read block length
                diffStream.Read(buffer, 0, 8);
                length = BitConverter.ToUInt64(buffer, 0);
                originalLength = length;

                //read data block buffered
                int bufferSize = 4096;
                
                //shrink buffer when data block is smaller than bufferSize
                if (length < (ulong)bufferSize)
                {
                    bufferSize = (int)length;
                }
                
                buffer = new byte[bufferSize];

                while (length > 0) //read blockwise until everything is read
                {
                    int currentOffset = 0;
                    int bytesReadSum = 0;

                    //read until buffer is full (by using lz4 it can occur that readBytes < bufferSize)
                    while (bytesReadSum < bufferSize && length > 0)
                    {
                        int bytesRead = diffStream.Read(buffer, currentOffset, bufferSize);
                        bytesReadSum += bytesRead;
                        currentOffset += bytesRead;
                        length -= (ulong)bytesRead;

                        //add length to progress
                        bytesRestored += bytesRead;
                    }

                    //todo:
                    //write block to target file
                    diskHandler.write(offset + (originalLength - length), buffer);
                }

                //show progress
                string progress = Common.PrettyPrinter.prettyPrintBytes(bytesRestored);
                if(progress != lastProgress)
                {
                    raiseNewEvent("Verarbeite Inkrement... " + progress, false, true);
                    lastProgress = progress;
                }

            }
            GC.KeepAlive(snapshotStream);
            diskHandler.detach();
            diskHandler.close();
            diffStream.Close();
        }

        //copys a given file without fs caching
        private void copyFile (string source, string destination)
        {
            FileStream srcFile = new FileStream(source, FileMode.Open);
            FileStream dstFile = new FileStream(destination, FileMode.Create);

            long bytesWritten = 0;
            byte[] buffer = new byte[1024];

            //iterate blocks
            while (bytesWritten < srcFile.Length)
            {
                //still possible to fille the whole buffer?
                if (bytesWritten + buffer.Length <= srcFile.Length) {
                    srcFile.Read(buffer, 0, buffer.Length);
                    dstFile.Write(buffer, 0, buffer.Length);
                    bytesWritten += buffer.Length;
                }
                else
                { //eof => read just remaining bytes
                    long remainingBytes = srcFile.Length - bytesWritten;
                    srcFile.Read(buffer, 0, (int)remainingBytes);
                    dstFile.Write(buffer, 0, (int)remainingBytes);
                    bytesWritten += remainingBytes;
                }
            }

            //close files when completed
            srcFile.Close();
            dstFile.Close();

        }

        //builds an EventProperties object and raises the "newEvent" event
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


//cb file type
//
//uint32 = 4 bytes = changed block count
//
//one block:
//ulong = 8 bytes = changed block offset
//ulong = 8 bytes = changed block length
//data block (size = changed block length)