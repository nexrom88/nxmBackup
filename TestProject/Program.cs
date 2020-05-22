using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TestProject
{
    class Program
    {
        static void Main(string[] args)
        {
            //CbStructure parsedFile = parseCBFile(@"F:\nxm\Job_1VM\DF86F44C-037D-4111-8EF8-3DC2B3C2F553\15223a46-4172-4e3a-b105-ccbb6a09ebb9.nxm\Win10.vhdx.cb");
            //string output = JsonConvert.SerializeObject(parsedFile);

            HyperVBackupRCT.SnapshotHandler sh = new HyperVBackupRCT.SnapshotHandler("DF86F44C-037D-4111-8EF8-3DC2B3C2F553", -1);
            sh.cleanUp();

            //Common.vhdxParser parser = new Common.vhdxParser(@"C:\VMs\Win10.vhdx");
            //Common.RegionTable regionTable =  parser.parseRegionTable();
            //Common.MetadataTable metadataTable = parser.parseMetadataTable(regionTable);
            //Common.BATTable table = parser.parseBATTable(regionTable, 0, false);
            //string output = JsonConvert.SerializeObject(table);
            //UInt32 blockSize = parser.getBlockSize(metadataTable);
        }

        //parses a cb file (just for testing, but DO NOT DELETE)
        


        
    }
}
