using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Common;

namespace nxmBackup.HVBackupCore
{
    public class DiffHandler
    {
        private Common.EventHandler eventHandler;
        private const int NO_RELATED_EVENT = -1;
        private StopRequestWrapper stopRequest;

        public DiffHandler(Common.EventHandler eventHandler, StopRequestWrapper stopRequest)
        {
            this.eventHandler = eventHandler;

            if(stopRequest != null)
            {
                this.stopRequest = stopRequest;
            }
            else
            {
                this.stopRequest = new StopRequestWrapper();
            }
        }

        //translates one changed block to vhdxBlocks
        private vhdxBlock[] getVhdxBlocks(ulong blockOffset, ulong blockLength, Common.BATTable vhdxBATTable, UInt32 vhdxBlockSize)
        {

            //calculate start BAT entry
            UInt32 startEntry = (UInt32)Math.Floor((float)blockOffset / (float)vhdxBlockSize);
            UInt32 endEntry = (UInt32)Math.Floor(((float)blockOffset + (float)blockLength) / (float)vhdxBlockSize);

            //translate to vhdxOffsets
            vhdxBlock[] vhdxOffsets = new vhdxBlock[(endEntry - startEntry) + 1];
            for (UInt32 i = startEntry; i <= endEntry; i++)
            {
                vhdxBlock currentBlock = new vhdxBlock();
                currentBlock.state = vhdxBATTable.entries[(int)i].state;
                currentBlock.offset = vhdxBATTable.entries[(int)i].FileOffsetMB * 1048576; // multiple with 1024^2 to get byte offset

                uint offsetIndex = i - startEntry;
                vhdxOffsets[offsetIndex] = currentBlock;
            }


            return vhdxOffsets;
        }

        //writes the diff file using cbt information and returns transfered bytes count
        [Obsolete]
        public UInt64 writeDiffFile(ChangedBlock[] changedBlocks, VirtualDiskHandler diskHandler, UInt32 vhdxBlockSize, Common.IArchive archive, string hddName, Common.BATTable vhdxBATTable, UInt64 bufferSize, RawBatTable rawBatTable, RawHeader rawHeader, RawLog rawLog, RawMetadataTable rawMeta, UInt64 vhdxSize)
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

            //write raw log section header
            outStream.Write(BitConverter.GetBytes(rawLog.vhdxOffset), 0, 8); //log vhdx offset
            outStream.Write(BitConverter.GetBytes((UInt64)rawLog.logLength), 0, 8); //log length

            //write raw log section payload
            outStream.Write(rawLog.rawData, 0, rawLog.rawData.Length);

            //write raw meta table header
            outStream.Write(BitConverter.GetBytes(rawMeta.vhdxOffset), 0, 8); //meta vhdx offset
            outStream.Write(BitConverter.GetBytes((UInt64)rawMeta.length), 0, 8); //meta length

            //write raw meta section payload
            outStream.Write(rawMeta.rawData, 0, rawMeta.rawData.Length);

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
                vhdxBlock[] vhdxBlocks = getVhdxBlocks(block.offset, block.length, vhdxBATTable, vhdxBlockSize);

                //write vhdx blocks
                outStream.Write(BitConverter.GetBytes((UInt32)vhdxBlocks.Length), 0, 4); //vhdx block offsets count


                //iterate through all block offsets
                UInt64 remainingLength = block.length;
                for (int i = 0; i < vhdxBlocks.Length; i++)
                {
                    UInt64 currentOffset = vhdxBlocks[i].offset;
                    UInt64 currentLength = vhdxBlockSize;
                    byte currentState = vhdxBlocks[i].state;

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

                    outStream.Write(BitConverter.GetBytes(currentOffset), 0, 8); //write one offset
                    outStream.Write(BitConverter.GetBytes(currentLength), 0, 8); //write one length
                    outStream.WriteByte(currentState); //write one state

                }

                long lastTotalByteTransferCounter = 0;
                long lastTransferTimestamp = 0;
                long transferRate = 0;
                long byteProcessCounter = 0;
                long processRate = 0;

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

                    //when buffer is empty -> error while reading
                    if (buffer == null)
                    {
                        outStream.Close();
                        inputStream.Close();
                        this.eventHandler.raiseNewEvent("Erstelle Inkrement - Fehler", false, true, relatedEventId, EventStatus.error);
                        return 0;
                    }

                    //write the current buffer to diff file
                    outStream.Write(buffer, 0, buffer.Length); //write data
                    bytesReadCount += (uint)buffer.Length;

                    //calculate progress
                    int percentage = (int)(((double)bytesReadCount / (double)totalBytesCount) * 100.0);

                    //rate calculations
                    byteProcessCounter += buffer.Length;
                    if (System.DateTimeOffset.Now.ToUnixTimeMilliseconds() - 1000 >= lastTransferTimestamp)
                    {
                        transferRate = (long)outStream.TotalCompressedBytesWritten - lastTotalByteTransferCounter;
                        lastTotalByteTransferCounter = (long)outStream.TotalCompressedBytesWritten;
                        processRate = byteProcessCounter;
                        byteProcessCounter = 0;
                        lastTransferTimestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    }

                    //new progress?
                    if (lastPercentage != percentage)
                    {
                        this.eventHandler.raiseNewEvent("Erstelle Inkrement - " + percentage + "%", transferRate, processRate, false, true, relatedEventId, EventStatus.inProgress);
                        lastPercentage = percentage;
                    }
                }
            }

            this.eventHandler.raiseNewEvent("Erstelle Inkrement - 100%", false, true, relatedEventId, EventStatus.successful);


            //close destination stream
            GC.KeepAlive(inputStream);
            outStream.Close();
            inputStream.Close();
            return outStream.TotalCompressedBytesWritten;
        }


        //merges a lb file with a vhdx
        public void merge_lb(System.IO.FileStream lbStream, string destinationSnapshot)
        {
            int relatedEventId = -1;
            if (this.eventHandler != null)
            {
                relatedEventId = this.eventHandler.raiseNewEvent("Verarbeite LiveBackup...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);
            }

            //parse lb file
            HyperVBackupRCT.LBStructure parsedLBFile = HyperVBackupRCT.LBParser.parseLBFile(lbStream, false);

            //open destination stream
            FileStream destinationStream = new FileStream(destinationSnapshot, FileMode.Open, FileAccess.ReadWrite);

            ulong blockCounter = 0;
            int lastProgress = 0;
            //iterate through each lb block
            foreach (HyperVBackupRCT.LBBlock currentBlock in parsedLBFile.blocks)
            {
                //read current block payload from source stream
                lbStream.Seek((long)currentBlock.lbFileOffset, SeekOrigin.Begin);
                byte[] blockPayload = new byte[currentBlock.length];
                lbStream.Read(blockPayload, 0, (int)currentBlock.length);

                //write current block payload to dest stream
                destinationStream.Seek((long)currentBlock.offset, SeekOrigin.Begin);
                destinationStream.Write(blockPayload, 0, (int)currentBlock.length);

                blockCounter++;

                //show progress
                int progress = (int)(((float)blockCounter / (float)(parsedLBFile.blocks.Count)) * 100.0);
                if (progress != lastProgress)
                {
                    lastProgress = progress;
                    this.eventHandler.raiseNewEvent("Verarbeite LiveBackup... " + progress, false, true, relatedEventId, EventStatus.inProgress);
                }
            }

            //close dest stream
            destinationStream.Close();

        }

        //merges a rct diff file with a vhdx
        public bool merge_rct(BlockCompression.LZ4BlockStream diffStream, string destinationSnapshot)
        {
            int relatedEventId = -1;
            if (this.eventHandler != null)
            {
                relatedEventId = this.eventHandler.raiseNewEvent("Verarbeite Inkrement...", false, false, NO_RELATED_EVENT, EventStatus.inProgress);
            }

            //open file streams
            VirtualDiskHandler diskHandler = new VirtualDiskHandler(destinationSnapshot);

            try
            {
                diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.All);
                diskHandler.attach(VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST);
            }catch(Exception ex)
            {
                this.eventHandler.raiseNewEvent("Verarbeite Inkrement... fehlgeschlagen", true, true, NO_RELATED_EVENT, EventStatus.error);
                return false;
            }

            //get sector size
            int sectorSize = (int)diskHandler.getSize().SectorSize;
            FileStream snapshotStream = null;

            try
            {
                snapshotStream = new FileStream(diskHandler.getHandle(), FileAccess.Write, false, sectorSize, true);

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

                    while ((ulong)bytesRead < cbStruct.blocks[i].changedBlockLength && !this.stopRequest.value) //read blockwise until everything is read or stopped by user
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
                        if (progress != lastProgress && this.eventHandler != null)
                        {
                            this.eventHandler.raiseNewEvent("Verarbeite Inkrement... " + progress, false, true, relatedEventId, EventStatus.inProgress);
                            lastProgress = progress;
                        }

                    }

                }

                if (this.eventHandler != null)
                {
                    //finished "normally"?
                    if (!this.stopRequest.value)
                    {
                        this.eventHandler.raiseNewEvent("Verarbeite Inkrement... erfolgreich", true, true, relatedEventId, EventStatus.successful);
                    }
                    else
                    {
                        this.eventHandler.raiseNewEvent("Verarbeite Inkrement... abgebrochen", true, true, relatedEventId, EventStatus.error);
                    }
                }

            }catch(Exception ex)
            {
                //error while merging
                this.eventHandler.raiseNewEvent("Verarbeite Inkrement... fehlgeschlagen", true, true, NO_RELATED_EVENT, EventStatus.error);
                diskHandler.detach();
                diskHandler.close();
                diffStream.Close();
                return false;
            }


            GC.KeepAlive(snapshotStream);
            diskHandler.detach();
            diskHandler.close();
            diffStream.Close();
            return true;
        }

        //copys a given file without fs caching
        private void copyFile(string source, string destination)
        {
            FileStream srcFile = new FileStream(source, FileMode.Open);
            FileStream dstFile = new FileStream(destination, FileMode.Create);

            long bytesWritten = 0;
            byte[] buffer = new byte[1024];

            //iterate blocks
            while (bytesWritten < srcFile.Length)
            {
                //still possible to fill the whole buffer?
                if (bytesWritten + buffer.Length <= srcFile.Length)
                {
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

    public struct vhdxBlock
    {
        public UInt64 offset;
        public byte state;
    }
}