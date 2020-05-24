using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HyperVBackupRCT
{
    public class CBParser
    {
        //parses a given cb file compressed by using LZ4 BC
        public static CbStructure parseCBFile(string path)
        {
            FileStream inputStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            BlockCompression.LZ4BlockStream blockStream = new BlockCompression.LZ4BlockStream(inputStream, BlockCompression.AccessMode.read);
            blockStream.CachingMode = true;

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
                oneBlock.vhdxBlockLocations = new List<VhdxBlockLocation>();

                //parse block header
                blockStream.Read(buffer, 0, 16);
                UInt64 blockOffset = BitConverter.ToUInt64(buffer, 0);
                UInt64 blockLength = BitConverter.ToUInt64(buffer, 8);
                oneBlock.changedBlockOffset = blockOffset;
                oneBlock.changedBlockLength = blockLength;

                //parse vhdx block offset count
                blockStream.Read(buffer, 0, 4);
                UInt32 vhdxBlockOffsetCount = BitConverter.ToUInt32(buffer, 0);

                //read each vhdx offset and length
                for (int j = 0; j < vhdxBlockOffsetCount; j++)
                {
                    VhdxBlockLocation location = new VhdxBlockLocation();
                    //offset
                    blockStream.Read(buffer, 0, 16);
                    location.vhdxOffset = BitConverter.ToUInt64(buffer, 0);

                    //corresponding length
                    location.vhdxLength = BitConverter.ToUInt64(buffer, 8);

                    oneBlock.vhdxBlockLocations.Add(location);
                }


                oneBlock.cbFileOffset = (ulong)blockStream.Position;

                //jump over data block
                blockStream.Seek((long)blockLength, SeekOrigin.Current);

                //oneBlock.blockData = blockData;

                parsedCBFile.blocks.Add(oneBlock);

            }

            blockStream.Close();
            inputStream.Close();

            return parsedCBFile;

        }
    }

    public struct CbStructure
    {
        public UInt32 blockCount;
        public UInt32 vhdxBlockSize;
        public List<CbBlock> blocks;
    }

    public struct CbBlock
    {
        public UInt64 changedBlockOffset;
        public UInt64 changedBlockLength;

        public List<VhdxBlockLocation> vhdxBlockLocations;

        public UInt64 cbFileOffset;
    }

    public struct VhdxBlockLocation
    {
        public UInt64 vhdxOffset;
        public UInt64 vhdxLength;

    }
}
