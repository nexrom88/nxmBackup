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
            Common.Archive a = new Common.Archive("c:\\zip\\test.zip", null);
            a.create();
            a.open(System.IO.Compression.ZipArchiveMode.Update);
            a.addDirectory("c:\\Test", System.IO.Compression.CompressionLevel.Optimal);
            Console.WriteLine("done");
            a.close();
        }
    }
}
