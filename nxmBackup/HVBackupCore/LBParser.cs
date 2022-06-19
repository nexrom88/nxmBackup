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

            //read iv length
            byte[] ivLengthBuffer = new byte[4];
            inStream.Read(ivLengthBuffer, 0, 4);
            retVal.ivLength = BitConverter.ToInt32(ivLengthBuffer, 0);
            readBytes += 4;

            //when iv > 0 read iv
            if (retVal.ivLength > 0)
            {
                byte[] iv = new byte[retVal.ivLength];
                inStream.Read(iv, 0, iv.Length);
                retVal.iv = iv;
                readBytes += (ulong)iv.Length;
            }

            //read until everything is read
            while (readBytes < (ulong)inStream.Length)
            {
                LBBlock currentStructure = new LBBlock();
                //read 32 header bytes
                byte[] buffer = new byte[32];
                int read = inStream.Read(buffer, 0, 32);

                UInt64 timestamp = BitConverter.ToUInt64(buffer, 0);
                UInt64 offset = BitConverter.ToUInt64(buffer, 8);
                UInt64 length = BitConverter.ToUInt64(buffer, 16);
                UInt64 compressedEncryptedLength = BitConverter.ToUInt64(buffer, 24);

                currentStructure.timestamp = DateTime.ParseExact(timestamp.ToString(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                currentStructure.offset = offset;
                currentStructure.length = length;
                currentStructure.compressedEncryptedLength = compressedEncryptedLength;

                //set payload offset to current stream offset 
                currentStructure.lbFileOffset = (UInt64)inStream.Position;

                //jump over payload
                inStream.Seek((Int64)compressedEncryptedLength, System.IO.SeekOrigin.Current);

                //add to list
                retVal.blocks.Add(currentStructure);

                //increase counter
                readBytes += (UInt64)buffer.Length + compressedEncryptedLength; //header size + payload size
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
        public UInt64 compressedEncryptedLength;
        public UInt64 lbFileOffset;
    }

    public struct LBStructure
    {
        public UInt64 vhdxSize;
        public Int32 ivLength;
        public byte[] iv;

        public List<LBBlock> blocks;
    }

}

//file structure
//8 bytes = vhdx size
//4 bytes: aes IV length
//IV length bytes: aes IV

//one block:
//8 bytes: timestamp (yyyyMMddHHmmss)
//8 bytes: payload offset
//8 bytes: payload length
//8 bytes: compressed/encrypted length
//x bytes: payload
