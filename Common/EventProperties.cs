using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public struct EventProperties
    {
        public string text; //event text
        public bool setDone; //sets the last event to "done"
        public bool isUpdate; //updates the last event
        public double progress; //optional: progress in percentage
    }
}
