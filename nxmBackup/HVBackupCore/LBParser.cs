using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace HyperVBackupRCT
{
    public class LBParser
    {
        //parses a given LB file
        public static LBStructure parseLBFile (System.IO.FileStream inStream, bool closeAfterFinish)
        {
            UInt64 readBytes = 0;
            LBStructure retVal = new LBStructure();
            retVal.blocks = new List<LBBlock>();

            //read vhdx size
            byte[] vhdxSizeBuffer = new byte[8];
            inStream.Read(vhdxSizeBuffer, 0, 8);
            retVal.vhdxSize = BitConverter.ToUInt64(vhdxSizeBuffer, 0);
            readBytes += 8;


            //read until everything is read
            while (readBytes < (ulong)inStream.Length)
            {
                LBBlock currentStructure = new LBBlock();
                //read 24 header bytes
                byte[] buffer = new byte[24];
                int read = inStream.Read(buffer, 0, 24);

                UInt64 timestamp = BitConverter.ToUInt64(buffer, 0);
                UInt64 offset = BitConverter.ToUInt64(buffer, 8);
                UInt64 length = BitConverter.ToUInt64(buffer, 16);

                if (offset < 1048576)
                {
                    offset = offset;
                }

                currentStructure.timestamp = DateTime.ParseExact(timestamp.ToString(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                currentStructure.offset = offset;
                currentStructure.length = length;

                //read payload data
                currentStructure.payload = new byte[length];
                inStream.Read(currentStructure.payload, 0, (int)length);

                //add to list
                retVal.blocks.Add(currentStructure);

                //increase counter
                readBytes += (UInt64)buffer.Length + length; //header size + payload size
            }

            //close filestream?
            if (closeAfterFinish)
            {
                inStream.Close();
            }

            return retVal;
        }


    }

    public struct LBBlock
    {
        public DateTime timestamp;
        public UInt64 offset;
        public UInt64 length;
        public byte[] payload;
    }

    public struct LBStructure
    {
        public UInt64 vhdxSize;
        public List<LBBlock> blocks;
    }

}

//file structure
//8 bytes = vhdx size

//one block:
//8 bytes: timestamp (yyyyMMddHHmmss)
//8 bytes: payload offset
//8 bytes: payload length
//x bytes: payload
