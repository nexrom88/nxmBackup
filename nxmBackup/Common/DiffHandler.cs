using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Common
{
    public class DiffHandler
    {
        private Common.EventHandler eventHandler;
        private const int NO_RELATED_EVENT = -1;

        public DiffHandler(Common.EventHandler eventHandler)
        {
            this.eventHandler = eventHandler;
        }

        //translates one changed block to vhdxOffsets
        private UInt64[] getVhdxBlockOffsets(ulong blockOffset, ulong blockLength, Common.BATTable vhdxBATTable, UInt32 vhdxBlockSize)
        {
            //calculate start BAT entry
            UInt32 startEntry = (UInt32)Math.Floor((float)blockOffset / (float)vhdxBlockSize);
            UInt32 endEntry = (UInt32)Math.Floor(((float)blockOffset + (float)blockLength) / (float)vhdxBlockSize);

            //translate to vhdxOffsets
            UInt64[] vhdxOffsets = new UInt64[(endEntry - startEntry) + 1];
            for (UInt32 i = startEntry; i <= endEntry; i++)
            {
                switch (vhdxBATTable.entries[(int)i].state)
                {
                    case 6: //block is fully present
                        vhdxOffsets[i - startEntry] = vhdxBATTable.entries[(int)i].FileOffsetMB * 1048576; // multiple with 1024^2 to get byte offset
                        break;
                    default: //block is not present
                        vhdxOffsets[i - startEntry] = 0;
                        break;
                }
                
            }


            return vhdxOffsets;
        }

        //writes the diff file using cbt information
        [Obsolete]
        public void writeDiffFile(ChangedBlock[] changedBlocks, VirtualDiskHandler diskHandler, UInt32 vhdxBlockSize, Common.IArchive archive, Compression compressionType, string hddName, Common.BATTable vhdxBATTable, UInt64 bufferSize, RawBatTable rawBatTable, RawHeader rawHeader, UInt64 vhdxSize)
        {

            //calculate changed bytes count for progress calculation
            ulong totalBytesCount = 0;
            ulong bytesReadCount = 0;
            int lastPercentage = -1;
            foreach (ChangedBlock bl in changedBlocks)
            {
                totalBytesCount += bl.length;
            }
            int relatedEventId = this.eventHandler.raiseNewEvent("Erstelle Inkrement - 0%", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            //fetch disk file handle
            IntPtr diskHandle = diskHandler.getHandle();

            //open destination file
            BlockCompression.LZ4BlockStream outStream = (BlockCompression.LZ4BlockStream)archive.createAndGetFileStream(hddName + ".cb");
            //FileStream outStream = new FileStream("f:\\" + hddName + ".cb", FileMode.Create);

            //open filestream to make it possible for readfile to read blocks (check why?)
            FileStream inputStream = new FileStream(diskHandle, FileAccess.Read, false, (int)bufferSize, true);

            //write block count to destination file
            outStream.Write(BitConverter.GetBytes((UInt32)changedBlocks.Length), 0, 4);

            //write vhdx block size
            outStream.Write(BitConverter.GetBytes(vhdxBlockSize), 0, 4);

            //write vhdx size
            outStream.Write(BitConverter.GetBytes(vhdxSize), 0, 8);

            //write raw vhdx header
            outStream.Write(rawHeader.rawData, 0, rawHeader.rawData.Length);

            //write raw bat table:

            //write raw bat table header
            outStream.Write(BitConverter.GetBytes(rawBatTable.vhdxOffset), 0, 8); //bat vhdx offset
            outStream.Write(BitConverter.GetBytes((UInt64)rawBatTable.rawData.Length), 0, 8); //bat length

            //write raw bat table payload
            outStream.Write(rawBatTable.rawData, 0, rawBatTable.rawData.Length);


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

                //get vhdx blocks
                UInt64[] vhdxBlocks = getVhdxBlockOffsets(block.offset, block.length, vhdxBATTable, vhdxBlockSize);

                //write vhdx blocks
                outStream.Write(BitConverter.GetBytes((UInt32)vhdxBlocks.Length), 0, 4); //vhdx block offsets count


                //iterate through all block offsets
                UInt64 remainingLength = block.length;
                for (int i = 0; i < vhdxBlocks.Length; i++)
                {
                    UInt64 currentOffset = vhdxBlocks[i];
                    UInt64 currentLength = vhdxBlockSize;

                    //first element? adjust offset and length
                    if (i == 0)
                    {
                        UInt64 offsetDelta = block.offset % vhdxBlockSize;
                        currentOffset += offsetDelta;
                        currentLength -= offsetDelta;
                    }

                    //last element? adjust length;
                    if (i + 1 == vhdxBlocks.Length)
                    {
                        currentLength = remainingLength;
                    }

                    remainingLength -= currentLength;

                    outStream.Write(BitConverter.GetBytes((UInt64)currentOffset), 0, 8); //write one offset
                    outStream.Write(BitConverter.GetBytes((UInt64)currentLength), 0, 8); //write one length

                }


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
                        this.eventHandler.raiseNewEvent("Erstelle Inkrement - " + percentage + "%", false, true, relatedEventId, EventStatus.inProgress);
                        lastPercentage = percentage;
                    }
                }
            }

            this.eventHandler.raiseNewEvent("Erstelle Inkrement - 100%", false, true, relatedEventId, EventStatus.successful);

            //close destination stream
            GC.KeepAlive(inputStream);
            outStream.Close();
            inputStream.Close();

        }


        //merges a rct diff file with a vhdx
        public void merge(BlockCompression.LZ4BlockStream diffStream, string destinationSnapshot)
        {
            int relatedEventId = this.eventHandler.raiseNewEvent("Verarbeite Inkrement...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            //open file streams
            VirtualDiskHandler diskHandler = new VirtualDiskHandler(destinationSnapshot);
            diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.All);
            diskHandler.attach(VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST);

            //get sector size
            int sectorSize = (int)diskHandler.getSize().SectorSize;

            FileStream snapshotStream = new FileStream(diskHandler.getHandle(), FileAccess.Write, false, sectorSize, true);

            HyperVBackupRCT.CbStructure cbStruct = HyperVBackupRCT.CBParser.parseCBFile(diffStream, false);

            //restored bytes count for progress calculation
            long bytesRestored = 0;
            string lastProgress = "";

            //iterate through all blocks
            byte[] buffer;
            for (int i = 0; i < cbStruct.blockCount; i++)
            {
                ulong bytesRead = 0;
                ulong writeOffset = 0;

                //read data block buffered, has to be 2^X
                int bufferSize = 16777216;
                bytesRead = 0;
                
                buffer = new byte[bufferSize];

                while ((ulong)bytesRead < cbStruct.blocks[i].changedBlockLength) //read blockwise until everything is read
                {
                    int bytesReadBlock = 0;

                    //shrink buffer size?
                    if (cbStruct.blocks[i].changedBlockLength - (ulong)bytesRead < (ulong)bufferSize)
                    {
                        bufferSize = (int)(cbStruct.blocks[i].changedBlockLength - (ulong)bytesRead);
                        buffer = new byte[bufferSize];
                    }


                    //read until buffer is full (by using lz4 it can occur that readBytes < bufferSize)
                    diffStream.Seek((Int64)cbStruct.blocks[i].cbFileOffset + (Int64)bytesRead, SeekOrigin.Begin);
                    while (bytesReadBlock < bufferSize)
                    {
                        int currentBytesCount = diffStream.Read(buffer, bytesReadBlock, bufferSize - bytesReadBlock);
                        bytesReadBlock += currentBytesCount;
                        bytesRead += (ulong)currentBytesCount;

                        //add length to progress
                        bytesRestored += currentBytesCount;
                    }

                    //write block to target file
                    diskHandler.write(cbStruct.blocks[i].changedBlockOffset + writeOffset, buffer);
                    writeOffset += (ulong)bufferSize;

                    //show progress
                    string progress = Common.PrettyPrinter.prettyPrintBytes(bytesRestored);
                    if (progress != lastProgress)
                    {
                        this.eventHandler.raiseNewEvent("Verarbeite Inkrement... " + progress, false, true, NO_RELATED_EVENT, EventStatus.inProgress);
                        lastProgress = progress;
                    }

                }

            }
            this.eventHandler.raiseNewEvent("Verarbeite Inkrement... erfolgreich", false, true, NO_RELATED_EVENT, EventStatus.successful);
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
                //still possible to fill the whole buffer?
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


    }
}


//cb file type
//
//uint32 = 4 bytes = changed block count
//uint32 = 4 bytes = vhdx block size
//uint64 = 8 bytes = vhdx size
//
//1048576 bytes = vhdx header
//
//bat table:
//ulong = 8 bytes = bat vhdx offset
//ulong = 8 bytes = bat length in bytes
//bat table data (size = bat length)
//
//one block:
//ulong = 8 bytes = changed block offset
//ulong = 8 bytes = changed block length

//uint32 = 4 bytes = vhdx block offsets count

//ulong = 8 bytes = vhdx block offset 1
//ulong = 8 bytes = vhdx block length 1

//ulong = 8 bytes = vhdx block offset 2
//ulong = 8 bytes = vhdx block length 2
//...

//block data (size = changed block length)