using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProj
{
    class Program
    {
        static void Main(string[] args)
        {
            Common.IArchive arch = new Common.LZ4Archive("E:\\nxm\\CentOS8\\911b147d-506e-4745-9483-efda1f29ec32.nxm", null);
            //arch.getFile("CentOS8.vhdx.cb", "c:\\restore\\test.cb");

            System.IO.Stream diffStream = arch.openAndGetFileStream("CentOS8.vhdx.cb");

            //read block count
            byte[] buffer = new byte[4];
            diffStream.Read(buffer, 0, 4);
            UInt32 blockCount = BitConverter.ToUInt32(buffer, 0);

            //restored bytes count for progress calculation
            long bytesRestored = 0;
            string lastProgress = "";

            //iterate through all blocks
            for (int i = 0; i < blockCount; i++)
            {
                ulong offset;
                ulong length;

                //read block offset
                buffer = new byte[8];
                diffStream.Read(buffer, 0, 8);
                offset = BitConverter.ToUInt64(buffer, 0);

                //read block length
                int b = diffStream.Read(buffer, 0, 8);
                length = BitConverter.ToUInt64(buffer, 0);

                //read data block
                buffer = new byte[length];
                int a = diffStream.Read(buffer, 0, buffer.Length);

            }
            diffStream.Close();
        }
    }
}
