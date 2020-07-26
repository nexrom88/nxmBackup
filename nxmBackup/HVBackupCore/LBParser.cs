using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nxmBackup.HVBackupCore
{
    public class LBParser
    {
        //parses a given LB file
        public static List<LBStructure> parseLBFile (System.IO.FileStream inStream, bool closeAfterFinish)
        {
            UInt64 readBytes = 0;
            List<LBStructure> retVal = new List<LBStructure>();

            //read until everything is read
            while (readBytes < (ulong)inStream.Length)
            {
                LBStructure currentStructure = new LBStructure();
                //read 16 header bytes
                byte[] buffer = new byte[16];
                inStream.Read(buffer, 0, 16);

                UInt64 offset = BitConverter.ToUInt64(buffer, 0);
                UInt64 length = BitConverter.ToUInt64(buffer, 8);
                currentStructure.offset = offset;
                currentStructure.length = length;

                //read payload data
                currentStructure.payload = new byte[length];
                inStream.Read(currentStructure.payload, 0, (int)length);

                //add to list
                retVal.Add(currentStructure);

                //increase counter
                readBytes += 16 + length;
            }

            return retVal;
        }

        //LB file structure:
        //8 bytes: payload offset
        //8 bytes: payload length
        //x bytes: payload
    }

    public struct LBStructure
    {
        public UInt64 offset;
        public UInt64 length;
        public byte[] payload;
    }
}
