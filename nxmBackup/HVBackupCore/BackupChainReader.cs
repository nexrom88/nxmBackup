using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using HyperVBackupRCT;
using nxmBackup.HVBackupCore;

namespace HVBackupCore
{
    public class BackupChainReader
    {
        private ReadableFullBackup fullBackup;
        private List<ReadableNonFullBackup> nonFullBackups;

        public ReadableFullBackup FullBackup { get => fullBackup; set => fullBackup = value; }
        public List<ReadableNonFullBackup> NonFullBackups { get => nonFullBackups; set => nonFullBackups = value; }

        //reads the given data from backup chain
        public void readFromChain(Int64 offset, Int64 length, byte[] buffer, Int32 bufferOffset, int callDepth = 0)
        {

            int firstRCTIndex = 0;
            if (nonFullBackups.Count > 0 && nonFullBackups[0].backupType == NonFullBackupType.lb)
            {
                firstRCTIndex = 1;
            }


            //read from vhdx header (first 1MB) on rct backup?
            if (nonFullBackups.Count > firstRCTIndex)
            {
                if (offset < 1048576) // within vhdx header?
                {
                    for (Int64 i = 0; i < length; i++)
                    {
                        buffer[bufferOffset + i] = NonFullBackups[firstRCTIndex].cbStructure.rawHeader.rawData[offset + i];
                    }


                    return;
                }
            }

            //read from bat table on flr on rct backup?
            if (nonFullBackups.Count > firstRCTIndex)
            {
                UInt64 vhdxBatOffset = NonFullBackups[firstRCTIndex].cbStructure.batTable.vhdxOffset;
                UInt64 vhdxBatEndOffset = vhdxBatOffset + (UInt64)NonFullBackups[firstRCTIndex].cbStructure.batTable.rawData.Length;
                if (vhdxBatOffset <= (UInt64)offset && (UInt64)offset < vhdxBatEndOffset)
                {
                    //how much bytes can be read here from raw bat table and where?
                    UInt64 readableBytes = (vhdxBatEndOffset - (UInt64)offset) + 1;
                    UInt64 readOffset = (UInt64)offset - vhdxBatOffset;

                    //do not read more than necessary
                    if (readableBytes > (UInt64)length)
                    {
                        readableBytes = (UInt64)length;
                    }

                    //copy bytes
                    for (UInt64 i = 0; i < readableBytes; i++)
                    {
                        buffer[(UInt64)bufferOffset + i] = NonFullBackups[firstRCTIndex].cbStructure.batTable.rawData[readOffset + i];
                    }

                    //request completed?
                    if ((Int64)readableBytes == length)
                    {
                        return;
                    }
                    else
                    {
                        //bytes missing
                        readFromChain(offset + (Int64)readableBytes, length - (Int64)readableBytes, buffer, bufferOffset + (Int32)readableBytes, callDepth +1);

                        return;
                    }
                }
            }


            //read from log section on flr on rct backup?
            if (nonFullBackups.Count > firstRCTIndex)
            {
                UInt64 vhdxLogOffset = NonFullBackups[firstRCTIndex].cbStructure.logSection.vhdxOffset;
                UInt64 vhdxLogEndOffset = vhdxLogOffset + (UInt64)NonFullBackups[firstRCTIndex].cbStructure.logSection.logLength;
                if (vhdxLogOffset <= (UInt64)offset && (UInt64)offset < vhdxLogEndOffset)
                {
                    //how much bytes can be read here from raw log section and where?
                    UInt64 readableBytes = (vhdxLogEndOffset - (UInt64)offset) + 1;
                    UInt64 readOffset = (UInt64)offset - vhdxLogOffset;

                    //do not read more than necessary
                    if (readableBytes > (UInt64)length)
                    {
                        readableBytes = (UInt64)length;
                    }

                    //copy bytes
                    for (UInt64 i = 0; i < readableBytes; i++)
                    {
                        buffer[(UInt64)bufferOffset + i] = NonFullBackups[firstRCTIndex].cbStructure.logSection.rawData[readOffset + i];
                    }

                    //request completed?
                    if ((Int64)readableBytes == length)
                    {
                        return;
                    }
                    else
                    {
                        //bytes missing
                        readFromChain(offset + (Int64)readableBytes, length - (Int64)readableBytes, buffer, bufferOffset + (Int32)readableBytes, callDepth + 1);

                        return;
                    }
                }
            }



            //read from metaDataTable on flr on rct backup?
            if (nonFullBackups.Count > firstRCTIndex)
            {
                UInt64 vhdxMetaOffset = NonFullBackups[firstRCTIndex].cbStructure.metaDataTable.vhdxOffset;
                UInt64 vhdxMetaEndOffset = vhdxMetaOffset + (UInt64)NonFullBackups[firstRCTIndex].cbStructure.metaDataTable.length;
                if (vhdxMetaOffset <= (UInt64)offset && (UInt64)offset < vhdxMetaEndOffset)
                {
                    //how much bytes can be read here from raw log section and where?
                    UInt64 readableBytes = (vhdxMetaEndOffset - (UInt64)offset) + 1;
                    UInt64 readOffset = (UInt64)offset - vhdxMetaOffset;

                    //do not read more than necessary
                    if (readableBytes > (UInt64)length)
                    {
                        readableBytes = (UInt64)length;
                    }

                    //copy bytes
                    for (UInt64 i = 0; i < readableBytes; i++)
                    {
                        buffer[(UInt64)bufferOffset + i] = NonFullBackups[firstRCTIndex].cbStructure.metaDataTable.rawData[readOffset + i];
                    }

                    //request completed?
                    if ((Int64)readableBytes == length)
                    {
                        return;
                    }
                    else
                    {
                        //bytes missing
                        readFromChain(offset + (Int64)readableBytes, length - (Int64)readableBytes, buffer, bufferOffset + (Int32)readableBytes, callDepth + 1);

                        return;
                    }
                }
            }




            //payload reads:

            //iterate through all non-full backups first to see if data is within rct backup
            foreach (ReadableNonFullBackup nonFullBackup in this.NonFullBackups)
            {
                //jump over backup if element is lb backup
                if (nonFullBackup.backupType == NonFullBackupType.lb)
                {
                    continue;
                }


                //iterate through all changed blocks
                for (int i = 0; i < nonFullBackup.cbStructure.blocks.Count; i++)
                {
                    //iterate through all vhdxoffsets
                    UInt64 skippedBytes = 0;
                    for (int j = 0; j < nonFullBackup.cbStructure.blocks[i].vhdxBlockLocations.Count; j++)
                    {
                        //is vhdxBlocklocation 0? not possible here -> skip this vhdxblocklocation
                        if (nonFullBackup.cbStructure.blocks[i].vhdxBlockLocations[j].vhdxOffset == 0)
                        {
                            skippedBytes += nonFullBackup.cbStructure.blocks[i].vhdxBlockLocations[j].vhdxLength;
                            continue;
                        }

                        VhdxBlockLocation currentLocation = nonFullBackup.cbStructure.blocks[i].vhdxBlockLocations[j];

                        //is offset within location? (start within location)
                        if ((UInt64)offset >= currentLocation.vhdxOffset && (UInt64)offset < currentLocation.vhdxOffset + currentLocation.vhdxLength)
                        {
                            //where to start reading within cb file?
                            UInt64 cbOffset = ((UInt64)offset - currentLocation.vhdxOffset) + skippedBytes + nonFullBackup.cbStructure.blocks[i].cbFileOffset;

                            //can everything be read?
                            if ((UInt64)offset + (UInt64)length <= currentLocation.vhdxOffset + currentLocation.vhdxLength)
                            {
                                nonFullBackup.sourceStreamRCT.Seek((Int64)cbOffset, System.IO.SeekOrigin.Begin);

                                nonFullBackup.sourceStreamRCT.Read(buffer, bufferOffset, (Int32)length);

                                return;
                            }
                            else //not everything can be read
                            {
                                //read just available bytes here
                                nonFullBackup.sourceStreamRCT.Seek((Int64)cbOffset, System.IO.SeekOrigin.Begin);

                                //calculate available bytes
                                //UInt64 availableBytes = (UInt64)length - ((cbOffset + (UInt64)length) - (nonFullBackup.cbStructure.blocks[i].cbFileOffset + nonFullBackup.cbStructure.blocks[i].changedBlockLength));
                                UInt64 availableBytes = (currentLocation.vhdxOffset + currentLocation.vhdxLength) - (UInt64)offset;

                                nonFullBackup.sourceStreamRCT.Read(buffer, bufferOffset, (Int32)availableBytes);


                                //read remaining bytes recursive
                                readFromChain(offset + (Int64)availableBytes, length - (Int64)availableBytes, buffer, bufferOffset + (Int32)availableBytes, callDepth +1);

                                return;
                            }
                        }

                        //is offset + length within location? (end within location)
                        else if ((UInt64)offset + (UInt64)length > currentLocation.vhdxOffset && (UInt64)offset + (UInt64)length < currentLocation.vhdxOffset + currentLocation.vhdxLength)
                        {
                            //where to start reading within cb file?
                            UInt64 cbOffset = nonFullBackup.cbStructure.blocks[i].cbFileOffset + skippedBytes;

                            //how much to read?
                            UInt64 readLength = ((UInt64)offset + (UInt64)length) - currentLocation.vhdxOffset;


                            nonFullBackup.sourceStreamRCT.Seek((Int64)cbOffset, System.IO.SeekOrigin.Begin);
                            nonFullBackup.sourceStreamRCT.Read(buffer, (bufferOffset + (int)length) - (int)readLength, (Int32)readLength);


                            //read the here-ignored start bytes
                            readFromChain(offset, length - (int)readLength, buffer, bufferOffset, callDepth +1);

                            return;

                        }
                        else
                        {
                            skippedBytes += currentLocation.vhdxLength;
                        }
                    }
                }



            }


            //data not found within rct backups => read from full backup
            fullBackup.sourceStream.Seek(offset, System.IO.SeekOrigin.Begin);
            fullBackup.sourceStream.Read(buffer, bufferOffset, (Int32)length);

        }


        //try read from lb
        public void readFromLB(Int64 offset, Int64 length, byte[] buffer)
        {
            //just continue when LB is first non-full backup
            if (this.nonFullBackups.Count == 0 || this.nonFullBackups[0].backupType != NonFullBackupType.lb)
            {
                return;
            }
            

            //iterate through all blocks
            foreach (LBBlock block in this.nonFullBackups[0].lbStructure.blocks)
            {
                UInt64 sourceOffset = 0, destOffset = 0, sourceAndDestLength = 0;

                //start offset is within current block?
                if (block.offset <= (UInt64)offset && (UInt64)offset < block.offset + block.length)
                {
                    //where and how much to read from this block?
                    destOffset = 0;
                    sourceOffset = (UInt64)offset - block.offset;
                    sourceAndDestLength = (block.offset + block.length) - (UInt64)offset;

                    //adjust blockLength
                    if (sourceAndDestLength > (UInt64)length)
                    {
                        sourceAndDestLength = (UInt64)length;
                    }

                    //end offset is within current block
                }
                else if (block.offset < (UInt64)offset + (UInt64)length && (UInt64)offset + (UInt64)length < block.offset + block.length)
                {
                    sourceOffset = 0;
                    sourceAndDestLength = ((UInt64)offset + (UInt64)length) - block.offset;
                    destOffset = block.offset - (UInt64)offset;
                }

                //copy to dest array
                if (sourceAndDestLength != 0)
                {
                    //read from lb
                    byte[] sourceBuffer = new byte[sourceAndDestLength];
                    this.nonFullBackups[0].sourceStreamLB.Seek((Int64)(block.lbFileOffset + sourceOffset), System.IO.SeekOrigin.Begin);
                    this.nonFullBackups[0].sourceStreamLB.Read(sourceBuffer, 0, (int)sourceAndDestLength);

                    Buffer.BlockCopy(sourceBuffer, 0, buffer,(int)destOffset, (int)sourceAndDestLength);
                }
            }
        }

        //one readable full backup
        public struct ReadableFullBackup
        {
            public BlockCompression.LZ4BlockStream sourceStream;
        }

        //one readable non-full backup
        public struct ReadableNonFullBackup
        {
            public NonFullBackupType backupType;

            //for rct backup:
            public BlockCompression.LZ4BlockStream sourceStreamRCT;
            public CbStructure cbStructure;

            //for lb backup:
            public System.IO.FileStream sourceStreamLB;
            public LBStructure lbStructure;
        }

        //non full backup type
        public enum NonFullBackupType
        {
            rct,
            lb
        }
    }
}
