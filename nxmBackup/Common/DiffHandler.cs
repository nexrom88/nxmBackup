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
                vhdxOffsets[i - startEntry] = vhdxBATTable.entries[(int)i].FileOffsetMB;
            }

            return vhdxOffsets;
        }

        //writes the diff file using cbt information
        [Obsolete]
        public void writeDiffFile(List<ChangedBlock> changedBlocks, FileStream sourceHDDStream, UInt32 vhdxBlockSize, UInt32 vhdxLogicalSectorSize, Common.IArchive archive, Compression compressionType, string hddName, Common.BATTable vhdxBATTable)
        {

            //calculate changed bytes count for progress calculation
            ulong totalBytesCount = 0;
            ulong bytesReadCount = 0;
            int lastPercentage = -1;
            foreach(ChangedBlock bl in changedBlocks)
            {
                totalBytesCount += bl.length;
            }
            int relatedEventId = this.eventHandler.raiseNewEvent("Erstelle Inkrement - 0%", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

            //open destination file
            BlockCompression.LZ4BlockStream outStream = (BlockCompression.LZ4BlockStream)archive.createAndGetFileStream(hddName + ".cb");


            //write block count to destination file
            outStream.Write(BitConverter.GetBytes(changedBlocks.Count), 0, 4);

            ulong bytesReadBlock;

            //read and write blocks
            foreach (ChangedBlock block in changedBlocks)
            {
                byte[] buffer;
                bytesReadBlock = 0;

                //write block header to diff file
                //write block
                outStream.Write(BitConverter.GetBytes(block.offset), 0, 8); //write offset
                outStream.Write(BitConverter.GetBytes(block.length), 0, 8); //write length

                UInt32 vhdxBlockOffsetsCount = (UInt32)Math.Ceiling((float)block.length / (float)vhdxBlockSize);
                
                //get vhdxBlockOffsets
                UInt64[] vhdxOffsets = getVhdxBlockOffsets(block.offset, block.length, vhdxBATTable, vhdxBlockSize);

                //write vhdxBlockOffsetsCount
                outStream.Write(BitConverter.GetBytes(vhdxOffsets.Length), 0, 4);

                //write block offsets
                foreach (UInt64 offset in vhdxOffsets)
                {
                    outStream.Write(BitConverter.GetBytes(offset), 0, 8);
                }


                for (int i = 0; i < vhdxOffsets.Length; i++)
                {
                    UInt64 startBlockOffset = 0; //where to start read within block 
                    UInt64 endBlockOffset = vhdxBlockSize; //where to end read within block

                    //first block?
                    if (i == 0)
                    {
                        startBlockOffset = block.offset % vhdxBlockSize;
                    }

                    //last block?
                    if (i + 1 == vhdxOffsets.Length) {
                        endBlockOffset = startBlockOffset + (block.length - bytesReadBlock) - 1;
                    }

                    UInt32 bytesToRead = (UInt32)(endBlockOffset - startBlockOffset) + 1;
                    buffer = new byte[bytesToRead];
                    sourceHDDStream.Seek((Int64)(vhdxOffsets[i] + startBlockOffset), SeekOrigin.Begin);
                    sourceHDDStream.Read(buffer, 0, (Int32)bytesToRead);

                    //write the current buffer to diff file
                    outStream.Write(buffer, 0, buffer.Length); //write data
                    bytesReadCount += (uint)buffer.Length;
                    bytesReadBlock += (UInt64)buffer.Length;

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
            outStream.Close();

        }


        //merges a rct diff file with a vhdx
        public void merge(Stream diffStream, string destinationSnapshot)
        {
            int relatedEventId = this.eventHandler.raiseNewEvent("Verarbeite Inkrement...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);

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
                ulong bytesRead = 0;
                ulong writeOffset = 0;

                //read block offset
                buffer = new byte[8];
                //ensure to read 8 bytes, lz4 sometimes reads less
                while (bytesRead < 8)
                {
                    bytesRead += (ulong)diffStream.Read(buffer, (int)bytesRead, 8 - (int)bytesRead);
                }
                offset = BitConverter.ToUInt64(buffer, 0);

                //read block length
                //ensure to read 8 bytes, lz4 sometimes reads less
                bytesRead = 0;
                while (bytesRead < 8)
                {
                    bytesRead += (ulong)diffStream.Read(buffer, (int)bytesRead, 8 - (int)bytesRead);
                }
                length = BitConverter.ToUInt64(buffer, 0);

                //jump over vhdx block offsets

                //read vhdxBlockOffsetsCount
                bytesRead = 0;
                while (bytesRead < 4)
                {
                    bytesRead += (ulong)diffStream.Read(buffer, (int)bytesRead, 4 - (int)bytesRead);
                }
                UInt32 vhdxBlockOffsetsCount = BitConverter.ToUInt32(buffer, 0);

                //read and ignore entries
                bytesRead = 0;
                while (bytesRead < 8* vhdxBlockOffsetsCount)
                {
                    bytesRead += (ulong)diffStream.Read(buffer, (int)bytesRead, 8 * (int)vhdxBlockOffsetsCount - (int)bytesRead);
                }


                //read data block buffered, has to be 2^X
                int bufferSize = 16777216;
                bytesRead = 0;
                
                buffer = new byte[bufferSize];

                while ((ulong)bytesRead < length) //read blockwise until everything is read
                {
                    int bytesReadBlock = 0;

                    //shrink buffer size?
                    if (length - (ulong)bytesRead < (ulong)bufferSize)
                    {
                        bufferSize = (int)(length - (ulong)bytesRead);
                        buffer = new byte[bufferSize];
                    }


                    //read until buffer is full (by using lz4 it can occur that readBytes < bufferSize)
                    while (bytesReadBlock < bufferSize)
                    {
                        int currentBytesCount = diffStream.Read(buffer, bytesReadBlock, bufferSize - bytesReadBlock);
                        bytesReadBlock += currentBytesCount;
                        bytesRead += (ulong)currentBytesCount;

                        //add length to progress
                        bytesRestored += currentBytesCount;
                    }

                    //write block to target file
                    diskHandler.write(offset + writeOffset, buffer);
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
//
//one block:
//ulong = 8 bytes = changed block offset
//ulong = 8 bytes = changed block length

//uint = 4 bytes = vhdx block offsets count
//ulong = 8 bytes = vhdx block offset 1
//ulong = 8 bytes = vhdx block offset 2
//...

//data block (size = changed block length)