using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class EventHandler
    {
        private string vmId = "";
        private int executionId;

        public EventHandler (JobVM vm, int jobExecutionId)
        {
            this.executionId = jobExecutionId;
            if (vm != null)
            {
                this.vmId = vm.vmID;

            }  
        }

        //builds a EventProperties object and raises the "newEvent" event
        public int raiseNewEvent(string text, bool setDone, bool isUpdate, int relatedEventId, EventStatus status)
        {
            return raiseNewEvent(text, -1, setDone, isUpdate, relatedEventId, status);
        }

        //builds a EventProperties object and raises the "newEvent" event (with transferrate)
        public int raiseNewEvent(string text, Int64 transferRate, bool setDone, bool isUpdate, int relatedEventId, EventStatus status)
        {
            //do not write to DB when execution ID < 0
            if (this.executionId < 0)
            {
                return 0;
            }

            Common.EventProperties props = new Common.EventProperties();
            props.text = text;
            props.eventStatus = status.ToString();
            props.setDone = setDone;
            props.isUpdate = isUpdate;
            props.eventIdToUpdate = relatedEventId;
            props.jobExecutionId = this.executionId;
            props.transferRate = transferRate;

            return Common.DBQueries.addEvent(props, this.vmId);

        }


        //writes errors to the log
        public static void writeToLog(string errorMsg, System.Diagnostics.StackTrace t)
        {

        }

    }
}