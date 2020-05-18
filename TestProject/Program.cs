using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject
{
    class Program
    {
        static void Main(string[] args)
        {
            HyperVBackupRCT.SnapshotHandler sh = new HyperVBackupRCT.SnapshotHandler("94921741-1567-4C42-84BF-4385F7E4BF9E", -1);
            sh.cleanUp();

            //Common.vhdxParser parser = new Common.vhdxParser(@"G:\target\Virtual Hard Disks\CentOS.vhdx");
            //Common.RegionTable regionTable =  parser.parseRegionTable();
            //Common.MetadataTable metadataTable = parser.parseMetadataTable(regionTable);
            //UInt32 lss =  parser.getLogicalSectorSize(metadataTable);
        }
    }
}
