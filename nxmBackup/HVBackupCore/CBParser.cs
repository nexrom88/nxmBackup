﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Common;
using System.Runtime.CompilerServices;

namespace HyperVBackupRCT
{
    public class CBParser
    {
        //parses a given cb file compressed by using LZ4 BC
        public static CbStructure parseCBFile(BlockCompression.LZ4BlockStream blockStream, bool closeAfterFinish)
        {            
            blockStream.CachingMode = true;

            byte[] buffer = new byte[16];
            blockStream.Read(buffer, 0, 16);

            CbStructure parsedCBFile = new CbStructure();
            parsedCBFile.blocks = new List<CbBlock>();

            //parse file header
            UInt32 blockCount = BitConverter.ToUInt32(buffer, 0);
            UInt32 vhdxBlockSize = BitConverter.ToUInt32(buffer, 4);
            UInt64 vhdxSize = BitConverter.ToUInt64(buffer, 8);
            parsedCBFile.blockCount = blockCount;
            parsedCBFile.vhdxBlockSize = vhdxBlockSize;
            parsedCBFile.vhdxSize = vhdxSize;

            //parse vhdx header
            parsedCBFile.rawHeader = new RawHeader();
            parsedCBFile.rawHeader.rawData = new byte[1048576]; // 1MB * 1024 * 1024
            blockStream.Read(parsedCBFile.rawHeader.rawData, 0, parsedCBFile.rawHeader.rawData.Length);

            //parse bat table:

            //read bat header
            blockStream.Read(buffer, 0, 16);
            RawBatTable rawBatTable = new RawBatTable();
            rawBatTable.vhdxOffset = BitConverter.ToUInt64(buffer, 0);
            UInt64 batLength = BitConverter.ToUInt64(buffer, 8);
            rawBatTable.rawData = new byte[batLength];

            //read bat payload
            blockStream.Read(rawBatTable.rawData, 0, (Int32)batLength);
            parsedCBFile.batTable = rawBatTable;

            //read log section header
            blockStream.Read(buffer, 0, 16);
            RawLog rawLog = new RawLog();
            rawLog.vhdxOffset = BitConverter.ToUInt64(buffer, 0);
            rawLog.logLength = BitConverter.ToUInt64(buffer, 8);
            rawLog.rawData = new byte[rawLog.logLength];

            //read log payload
            blockStream.Read(rawLog.rawData, 0, (Int32)rawLog.logLength);
            parsedCBFile.logSection = rawLog;

            //read metaDataTable header
            blockStream.Read(buffer, 0, 16);
            RawMetadataTable rawMeta = new RawMetadataTable();
            rawMeta.vhdxOffset = BitConverter.ToUInt64(buffer, 0);
            rawMeta.length = BitConverter.ToUInt64(buffer, 8);
            rawMeta.rawData = new byte[rawMeta.length];

            //read metaDataTable payload
            blockStream.Read(rawMeta.rawData, 0, (Int32)rawMeta.length);
            parsedCBFile.metaDataTable = rawMeta;



            buffer = new byte[17];
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
                    blockStream.Read(buffer, 0, 17);
                    location.vhdxOffset = BitConverter.ToUInt64(buffer, 0);

                    //corresponding length
                    location.vhdxLength = BitConverter.ToUInt64(buffer, 8);

                    //state
                    location.vhdxState = buffer[16];

                    oneBlock.vhdxBlockLocations.Add(location);
                }


                oneBlock.cbFileOffset = (ulong)blockStream.Position;

                //jump over data block
                blockStream.Seek((long)blockLength, SeekOrigin.Current);

                //oneBlock.blockData = blockData;

                parsedCBFile.blocks.Add(oneBlock);

            }

            //close stream?
            if (closeAfterFinish)
            {
                blockStream.Close();
            }

            return parsedCBFile;
        }

        
    }

    public struct CbStructure
    {
        public UInt32 blockCount;
        public UInt32 vhdxBlockSize;
        public UInt64 vhdxSize;

        public RawHeader rawHeader;

        public RawBatTable batTable;

        public RawLog logSection;

        public RawMetadataTable metaDataTable;

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
        public byte vhdxState;
    }
}

//cb file type
//
// see spec file