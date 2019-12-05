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
            Common.IArchive arch = new Common.LZ4Archive("e:\\nxm\\abc.nxm", null);
            //arch.create();
            //arch.addFile("c:\\Win10.vhdx", "vhd/folder", System.IO.Compression.CompressionLevel.Fastest);
            arch.listEntries();
        }
    }
}
