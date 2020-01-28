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
            MFUserMode.MFUserModeWrapper wrapper = new MFUserMode.MFUserModeWrapper();
            wrapper.connectToMF();

        }
    }
}
