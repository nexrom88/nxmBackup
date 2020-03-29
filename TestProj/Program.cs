using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using HyperVBackupRCT;

namespace TestProj
{
    class Program
    {
        static void Main(string[] args)
        {
            //FullRestoreHandler restHandler = new FullRestoreHandler();
            //restHandler.performGuestFilesRestore(@"E:\nxm\Win10\Windows 10", "Microsoft:7D9F7CE2-09DF-436C-B1CE-0B0168187F09", ConfigHandler.Compression.lz4);

            //MFUserMode.MountHandler.startMountProcess("c:\\target\\comp.lz4", "c:\\target\\mount.iso");


            //System.IO.FileStream target = new System.IO.FileStream("c:\\target\\comp.lz4", System.IO.FileMode.Create, System.IO.FileAccess.ReadWrite);
            //BlockCompression.LZ4BlockStream bs = new BlockCompression.LZ4BlockStream(target, BlockCompression.AccessMode.write);

            //System.IO.FileStream source = new System.IO.FileStream("C:\\Users\\admin\\Downloads\\centos.iso", System.IO.FileMode.Open, System.IO.FileAccess.Read);

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


            //System.IO.FileStream source = new System.IO.FileStream("c:\\target\\numbers.lz4", System.IO.FileMode.Open, System.IO.FileAccess.Read);
            //BlockCompression.LZ4BlockStream bs = new BlockCompression.LZ4BlockStream(source, BlockCompression.AccessMode.read);

            //System.IO.FileStream target = new System.IO.FileStream("c:\\target\\numbers_dec_part.bin", System.IO.FileMode.Create, System.IO.FileAccess.Write);

            //int readBytes = -1;
            //bs.Seek(4*10000, System.IO.SeekOrigin.Begin);
            //while (readBytes != 0)
            //{
            //    byte[] buffer = new byte[1000000];
            //    readBytes = bs.Read(buffer, 0, buffer.Length);
            //    target.Write(buffer, 0, readBytes);
            //}

            //bs.Close();
            //target.Close();


            //System.IO.FileStream outStream = new System.IO.FileStream("c:\\target\\numbers.bin", System.IO.FileMode.Create, System.IO.FileAccess.Write);
            //for (int i = 0; i < 100000000; i++)
            //{
            //    byte[] buffer = BitConverter.GetBytes(i);
            //    outStream.Write(buffer, 0, 4);
            //}

            //outStream.Close();
        }
    }
}
