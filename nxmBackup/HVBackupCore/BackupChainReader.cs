using System;
using System.Collections.Generic;
using System.Linq;
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
        public void readFromChain(Int64 offset, Int64 length, byte[] buffer, Int32 bufferOffset)
        {
            //read from vhdx header (first 1MB) on rct backup?
            if (nonFullBackups.Count > 0)
            {
                if (offset < 1048576) // within vhdx header?
                {
                    for (Int64 i = 0; i < length; i++)
                    {
                        buffer[bufferOffset + i] = NonFullBackups[0].cbStructure.rawHeader.rawData[offset + i];
                    }
                    return;
                }
            }

            //read from bat table on flr on rct backup?
            if (nonFullBackups.Count > 0)
            {
                UInt64 vhdxBatOffset = NonFullBackups[0].cbStructure.batTable.vhdxOffset;
                UInt64 vhdxBatEndOffset = vhdxBatOffset + (UInt64)NonFullBackups[0].cbStructure.batTable.rawData.Length;
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
                        buffer[(UInt64)bufferOffset + i] = NonFullBackups[0].cbStructure.batTable.rawData[readOffset + i];
                    }

                    //request completed?
                    if ((Int64)readableBytes == length)
                    {
                        return;
                    }
                    else
                    {
                        //bytes missing
                        readFromChain(offset + (Int64)readableBytes, length - (Int64)readableBytes, buffer, bufferOffset + (Int32)readableBytes);
                        return;
                    }
                }
            }

            //payload reads:


            //iterate through all non-full backups first to see if data is within rct backup
            foreach (ReadableNonFullBackup nonFullBackup in this.NonFullBackups)
            {
                switch (nonFullBackup.backupType)
                {
                    case NonFullBackupType.lb: //lb backup

                        //iterate through all changed blocks

                        break;
                    case NonFullBackupType.rct: //rct backup

                        UInt64 vhdxBlockSize = nonFullBackup.cbStructure.vhdxBlockSize;
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

                                //is offset within location?
                                if ((UInt64)offset >= currentLocation.vhdxOffset && (UInt64)offset < currentLocation.vhdxOffset + currentLocation.vhdxLength)
                                {
                                    //where to start reading within cb file?
                                    UInt64 cbOffset = ((UInt64)offset - currentLocation.vhdxOffset) + skippedBytes + nonFullBackup.cbStructure.blocks[i].cbFileOffset;

                                    //can everything be read?
                                    if (cbOffset + (UInt64)length < nonFullBackup.cbStructure.blocks[i].cbFileOffset + nonFullBackup.cbStructure.blocks[i].changedBlockLength)
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
                                        UInt64 availableBytes = (UInt64)length - ((cbOffset + (UInt64)length) - (nonFullBackup.cbStructure.blocks[i].cbFileOffset + nonFullBackup.cbStructure.blocks[i].changedBlockLength));

                                        nonFullBackup.sourceStreamRCT.Read(buffer, bufferOffset, (Int32)availableBytes);

                                        //read remaining bytes recursive
                                        readFromChain(offset + (Int64)availableBytes, length - (Int64)availableBytes, buffer, bufferOffset + (Int32)availableBytes);

                                        return;
                                    }
                                }
                                else
                                {
                                    skippedBytes += currentLocation.vhdxLength;
                                }
                            }
                        }
                        break;
                }

                
            }

            //data not found within rct backups => read from full backup
            fullBackup.sourceStream.Seek(offset, System.IO.SeekOrigin.Begin);
            fullBackup.sourceStream.Read(buffer, bufferOffset, (Int32)length);

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
