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
            arch.addDirectory("d:\\restore");
        }
    }
}
