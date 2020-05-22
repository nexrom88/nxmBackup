using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperVBackupRCT;

namespace HVBackupCore
{
    public class ReadableBackupChain
    {
        private ReadableFullBackup fullBackup;
        private List<ReadableRCTBackup> rctBackups;

        public ReadableFullBackup FullBackup { get => fullBackup; set => fullBackup = value; }
        public List<ReadableRCTBackup> RCTBackups { get => rctBackups; set => rctBackups = value; }

        //reads the given data from backup chain
        public byte[] readFromChain (Int64 offset, Int64 length)
        {
            //iterate through all rct backups first to see if data is within rct backup
            foreach (ReadableRCTBackup rctBackup in this.RCTBackups)
            {
                UInt64 vhdxBlockSize = rctBackup.cbStructure.vhdxBlockSize;
            }
        }


        //one readable full backup
        public struct ReadableFullBackup
        {
            public BlockCompression.LZ4BlockStream sourceStream;
        }

        //one readable rct backup
        public struct ReadableRCTBackup
        {
            public BlockCompression.LZ4BlockStream sourceStream;
            public CbStructure cbStructure;
        }
    }
}
