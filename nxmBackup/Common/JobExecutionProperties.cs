﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public struct JobExecutionProperties
    {
        public DateTime startStamp;
        public DateTime stopTime;
        public bool isRunning;
        public int transferRate;
        public int alreadyRead;
        public int alreadyWritten;
        public bool successful;
        public int warnings;
        public int errors;
    }
}