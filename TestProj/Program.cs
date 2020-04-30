using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using HyperVBackupRCT;
using Common;

namespace TestProj
{
    class Program
    {
        static void Main(string[] args)
        {
            string file = "h:\\Win10.vhdx";
            vhdxParser parser = new vhdxParser(file);

            RegionTable regTable = parser.parseRegionTable();
            MetadataTable table = parser.parseMetadataTable(regTable);
            UInt32 blockSize = parser.getBlockSize(table);
            UInt32 logicalSectorSize = parser.getLogicalSectorSize(table);
            UInt64 vhdxChunkRatio = ((UInt64)8388608 * (UInt64)logicalSectorSize) / (UInt64)blockSize;
        }
        
    }
}
