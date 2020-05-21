using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TestProject
{
    class Program
    {
        static void Main(string[] args)
        {
            CbStructure parsedFile = parseCBFile(@"F:\nxm\Job_1VM\DF86F44C-037D-4111-8EF8-3DC2B3C2F553\b408d1df-6fbc-45c1-9afc-0824108624f0.nxm\Win10.vhdx.cb");
            string output = JsonConvert.SerializeObject(parsedFile);

            //HyperVBackupRCT.SnapshotHandler sh = new HyperVBackupRCT.SnapshotHandler("94921741-1567-4C42-84BF-4385F7E4BF9E", -1);
            //sh.cleanUp();

            //Common.vhdxParser parser = new Common.vhdxParser(@"G:\target\Virtual Hard Disks\CentOS.vhdx");
            //Common.RegionTable regionTable =  parser.parseRegionTable();
            //Common.MetadataTable metadataTable = parser.parseMetadataTable(regionTable);
            //UInt32 lss =  parser.getLogicalSectorSize(metadataTable);
        }

        //parses a cb file (just for testing, but DO NOT DELETE)
        private static CbStructure parseCBFile(string path)
        {
            FileStream inputStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            BlockCompression.LZ4BlockStream blockStream = new BlockCompression.LZ4BlockStream(inputStream, BlockCompression.AccessMode.read);

            byte[] buffer = new byte[8];
            blockStream.Read(buffer, 0, 8);

            CbStructure parsedCBFile = new CbStructure();
            parsedCBFile.blocks = new List<CbBlock>();

            //parse file header
            UInt32 blockCount = BitConverter.ToUInt32(buffer, 0);
            UInt32 vhdxBlockSize = BitConverter.ToUInt32(buffer, 4);
            parsedCBFile.blockCount = blockCount;
            parsedCBFile.vhdxBlockSize = vhdxBlockSize;

            buffer = new byte[16];
            //iterate through each block
            for (int i = 0; i < blockCount; i++)
            {
                CbBlock oneBlock = new CbBlock();
                oneBlock.blockOffsets = new List<ulong>();

                //parse block header
                blockStream.Read(buffer, 0, 16);
                UInt64 blockOffset = BitConverter.ToUInt64(buffer, 0);
                UInt64 blockLength = BitConverter.ToUInt64(buffer, 8);
                oneBlock.changedBlockOffset = blockOffset;
                oneBlock.changedBlockLength = blockLength;

                //parse vhdx block offset count
                blockStream.Read(buffer, 0, 4);
                UInt32 vhdxBlockOffsetCount = BitConverter.ToUInt32(buffer, 0);

                //read each vhdx offset
                for (int j = 0; j < vhdxBlockOffsetCount; j++)
                {
                    blockStream.Read(buffer, 0, 8);
                    oneBlock.blockOffsets.Add(BitConverter.ToUInt64(buffer, 0));
                }

                //read data block
                byte[] blockData = new byte[blockLength];
                blockStream.Read(blockData, 0, (int)blockLength);

                //oneBlock.blockData = blockData;

                parsedCBFile.blocks.Add(oneBlock);

            }

            return parsedCBFile;

        }


        private struct CbStructure
        {
            public UInt32 blockCount;
            public UInt32 vhdxBlockSize;
            public List<CbBlock> blocks;
        }

        private struct CbBlock
        {
            public UInt64 changedBlockOffset;
            public UInt64 changedBlockLength;

            public List<UInt64> blockOffsets;

            public byte[] blockData;
        }
    }
}
