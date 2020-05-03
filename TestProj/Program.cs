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
            HyperVBackupRCT.SnapshotHandler ssHandler = new HyperVBackupRCT.SnapshotHandler("94921741-1567-4C42-84BF-4385F7E4BF9E", 0);
            ssHandler.cleanUp();
        }
        
    }
}
