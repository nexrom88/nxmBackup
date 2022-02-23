using System;
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
        public string eventStatus; //sets the event status (see DB table EventStatus for valid values)
        public bool isUpdate; //updates the last event
        public int eventIdToUpdate; //when isUpdate this value specifies the event to be updated
        public double progress; //optional: progress in percentage
        public Int64 transferRate; //optional: current transfer rate
        public Int64 processRate; //optional: current process rate

        //optional: current element (e.g. restore item 5/10)
        public uint elementsCount;
        public uint currentElement;
    }

    public enum EventStatus
    {
        warning, error, inProgress, successful, info
    }
}
