﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public struct EventProperties
    {
        public int jobExecutionId; //job execution ID
        public string text; //event text
        public bool setDone; //sets the last event to "done"
        public bool isUpdate; //updates the last event
        public int eventIdToUpdate; //when isUpdate this value specifies the event to be updated
        public double progress; //optional: progress in percentage

        //optional: current element (e.g. restore item 5/10)
        public uint elementsCount;
        public uint currentElement;
    }
}
