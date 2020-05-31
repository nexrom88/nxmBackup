using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperVBackupRCT;

namespace HVBackupCore
{
    public class BackupChainReader
    {
        private ReadableFullBackup fullBackup;
        private List<ReadableRCTBackup> rctBackups;

        public ReadableFullBackup FullBackup { get => fullBackup; set => fullBackup = value; }
        public List<ReadableRCTBackup> RCTBackups { get => rctBackups; set => rctBackups = value; }

        //reads the given data from backup chain
        public void readFromChain(Int64 offset, Int64 length, ref byte[] buffer, Int32 bufferOffset)
        {
            //read from vhdx header (first 1MB) on rct backup?
            if (rctBackups.Count > 0)
            {
                if (offset < 1048576) // within vhdx header?
                {
                    for (Int64 i = 0; i < length; i++)
                    {
                        buffer[bufferOffset + i] = RCTBackups[0].cbStructure.rawHeader.rawData[offset + i];
                    }
                    return;
                }
            }

            //read from bat table on flr on rct backup?
            if (rctBackups.Count > 0)
            {
                UInt64 vhdxBatOffset = RCTBackups[0].cbStructure.batTable.vhdxOffset;
                UInt64 vhdxBatEndOffset = vhdxBatOffset + (UInt64)RCTBackups[0].cbStructure.batTable.rawData.Length;
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
                        buffer[(UInt64)bufferOffset + i] = RCTBackups[0].cbStructure.batTable.rawData[readOffset + i];
                    }

                    //request completed?
                    if ((Int64)readableBytes == length)
                    {
                        return;
                    }
                    else
                    {
                        //bytes missing
                        readFromChain(offset + (Int64)readableBytes, length - (Int64)readableBytes, ref buffer, bufferOffset + (Int32)readableBytes);
                        return;
                    }
                }
            }

            //payload reads:


            //iterate through all rct backups first to see if data is within rct backup
            foreach (ReadableRCTBackup rctBackup in this.RCTBackups)
            {
                UInt64 vhdxBlockSize = rctBackup.cbStructure.vhdxBlockSize;
                //iterate through all changed blocks
                for (int i = 0; i < rctBackup.cbStructure.blocks.Count; i++)
                {
                    //iterate through all vhdxoffsets
                    UInt64 skippedBytes = 0;
                    for (int j = 0; j < rctBackup.cbStructure.blocks[i].vhdxBlockLocations.Count; j++)
                    {
                        //is vhdxBlocklocation 0? not possible here -> skip this vhdxblocklocation
                        if (rctBackup.cbStructure.blocks[i].vhdxBlockLocations[j].vhdxOffset == 0)
                        {
                            skippedBytes += rctBackup.cbStructure.blocks[i].vhdxBlockLocations[j].vhdxLength;
                            continue;
                        }

                        VhdxBlockLocation currentLocation = rctBackup.cbStructure.blocks[i].vhdxBlockLocations[j];

                        //if (offset == 10506731520)
                        //{
                        //    offset = 10506731520;
                        //}

                        //is offset within location?
                        if ((UInt64)offset >= currentLocation.vhdxOffset && (UInt64)offset < currentLocation.vhdxOffset + currentLocation.vhdxLength)
                        {
                            //where to start reading within cb file?
                            UInt64 cbOffset = ((UInt64)offset - currentLocation.vhdxOffset) + skippedBytes + rctBackup.cbStructure.blocks[i].cbFileOffset;

                            //can everything be read?
                            if (cbOffset + (UInt64)length < rctBackup.cbStructure.blocks[i].cbFileOffset + rctBackup.cbStructure.blocks[i].changedBlockLength)
                            {
                                rctBackup.sourceStream.Seek((Int64)cbOffset, System.IO.SeekOrigin.Begin);

                                rctBackup.sourceStream.Read(buffer, bufferOffset, (Int32)length);
                                return;
                            }
                            else //not everything can be read
                            {
                                //read just available bytes here
                                rctBackup.sourceStream.Seek((Int64)cbOffset, System.IO.SeekOrigin.Begin);

                                //calculate available bytes
                                UInt64 availableBytes = (UInt64)length - ((cbOffset + (UInt64)length) - (rctBackup.cbStructure.blocks[i].cbFileOffset + rctBackup.cbStructure.blocks[i].changedBlockLength));

                                rctBackup.sourceStream.Read(buffer, bufferOffset, (Int32)availableBytes);

                                //read remaining bytes recursive
                                readFromChain(offset + (Int64)availableBytes, length - (Int64)availableBytes, ref buffer, bufferOffset + (Int32)availableBytes);

                                return;
                            }
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


        //one readable full backup
        public struct ReadableFullBackup
        {
            public BlockCompression.LZ4BlockStream sourceStream;
        }

        //one readable rct backup
        public struct ReadableRCTBackup
        {
            public BlockCompression.LZ4BlockStream sourceStream;
            public CbStructure cbStructure;
        }
    }
}
