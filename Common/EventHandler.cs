using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class EventHandler
    {
        private string vmId;
        private int executionId;

        public EventHandler (string vmId, int jobExecutionId)
        {
            this.executionId = jobExecutionId;
            this.vmId = vmId;
        }

        //builds a EventProperties object and raises the "newEvent" event
        public int raiseNewEvent(string text, bool setDone, bool isUpdate, int relatedEventId, EventStatus status)
        {
            Common.EventProperties props = new Common.EventProperties();
            props.text = text;
            props.eventStatus = status.ToString();
            props.setDone = setDone;
            props.isUpdate = isUpdate;
            props.eventIdToUpdate = relatedEventId;
            props.jobExecutionId = this.executionId;

            return Common.DBQueries.addEvent(props, this.vmId);

        }


        //writes errors to the log
        public static void writeToLog(string errorMsg, System.Diagnostics.StackTrace t)
        {

        }

    }
}