using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace TestProj
{
    class Program
    {
        static void Main(string[] args)
        {
            //System.IO.FileStream target = new System.IO.FileStream("c:\\target\\comp.lz4", System.IO.FileMode.Create, System.IO.FileAccess.ReadWrite);
            //BlockCompression.LZ4BlockStream bs = new BlockCompression.LZ4BlockStream(target, BlockCompression.AccessMode.write);

            //System.IO.FileStream source = new System.IO.FileStream("c:\\target\\source.mp3", System.IO.FileMode.Open, System.IO.FileAccess.Read);

            //int readBytes = -1;
            //while (readBytes != 0)
            //{
            //    byte[] buffer = new byte[512];
            //    readBytes = source.Read(buffer, 0, buffer.Length);
            //    bs.Write(buffer, 0, readBytes);
            //}

            //source.Close();
            //bs.Close();
            //target.Close();


            System.IO.FileStream source = new System.IO.FileStream("c:\\target\\comp.lz4", System.IO.FileMode.Open, System.IO.FileAccess.Read);
            BlockCompression.LZ4BlockStream bs = new BlockCompression.LZ4BlockStream(source, BlockCompression.AccessMode.read);

            System.IO.FileStream target = new System.IO.FileStream("c:\\target\\dest.mp3", System.IO.FileMode.Create, System.IO.FileAccess.Write);

            int readBytes = -1;
            while (readBytes != 0)
            {
                byte[] buffer = new byte[100000];
                readBytes = bs.Read(buffer, 0, buffer.Length);
                target.Write(buffer, 0, readBytes);
            }

            bs.Close();
            target.Close();
        }
    }
}
