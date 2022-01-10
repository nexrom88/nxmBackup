using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public struct JobExecutionProperties
    {
        public DateTime startStamp;
        public UInt64 bytesProcessed;
        public UInt64 bytesTransfered;
        public bool successful;
        public int warnings;
        public int errors;
    }
}
