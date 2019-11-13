using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace HyperVBackupRCT
{
    public struct ChangedBlock
    {
        public ulong offset;
        public ulong length;
    }
}
