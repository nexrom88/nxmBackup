using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class DBQueries
    {
        // Adds a new job execution before performing backup process.
        public static int addJobExecution(string jobId, string type)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    List<Dictionary<string, string>> jobExecutionIds = dbConn.doReadQuery("INSERT INTO JobExecutions (jobId, isRunning, transferRate, alreadyRead, alreadyWritten, successful, warnings, errors, type) " +
                        "VALUES(@jobId, @isRunning, @transferRate, @alreadyRead, @alreadyWritten, @successful, @warnings, @errors, @type);SELECT SCOPE_IDENTITY() AS id;",
                        new Dictionary<string, string>() {
                            { "jobId", jobId },
                            { "isRunning", "1" },
                            { "transferRate", "0" },
                            { "alreadyRead", "0" },
                            { "alreadyWritten", "0" },
                            { "successful", "0" },
                            { "warnings", "0" },
                            { "errors", "0" },
                            { "type", type }
                        }, null);

                    if (jobExecutionIds.Count != 1)
                    {
                        throw new Exception("Error during insert operation (no insert id)");
                    }

                    return int.Parse(jobExecutionIds[0]["id"]);
                }
            }
            catch (Exception exp)
            {
                EventHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
                return -1;
            }
        }

        // Adds events to db while performing backup process.
        public static int addEvent(Common.EventProperties eventProperties, string vmName)
        {
            //if eventStatus is not set, use "inProgress"
            if (eventProperties.eventStatus == "")
            {
                eventProperties.eventStatus = "inProgress";
            }

            //check whether the given event is an update
            if (eventProperties.isUpdate)
            {
                updateEvent(eventProperties, vmName);
                return -1;
            }

            //check whether the given event is "setDone" event
            if (eventProperties.setDone)
            {
                setDoneEvent(eventProperties, vmName);
                return -1;
            }

            //not an update: do insert
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    List<Dictionary<string, string>> jobExecutionEventIds = dbConn.doReadQuery("INSERT INTO JobExecutionEvents (vmId, info, jobExecutionId, status) VALUES (@vmId, @info, @jobExecutionId, (SELECT id FROM EventStatus WHERE text= @status));SELECT SCOPE_IDENTITY() AS id;",
                        new Dictionary<string, string>() { { "vmId", vmName }, { "info", eventProperties.text }, { "jobExecutionId", eventProperties.jobExecutionId.ToString()}, {"status", eventProperties.eventStatus} }, null);

                    if (jobExecutionEventIds == null || jobExecutionEventIds.Count == 0)
                    {
                        throw new Exception("Error during insert operation (affectedRows != 1)");
                    }
                    else
                    {
                        return int.Parse(jobExecutionEventIds[0]["id"]);
                    }
                }
            }
            catch (Exception exp)
            {
                EventHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
                return -1;
            }
        }

        // Updates an existing event (e.g. progress) while performing backup process.
        public static void updateEvent(Common.EventProperties eventProperties, string vmName)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    int affectedRows = dbConn.doWriteQuery("UPDATE JobExecutionEvents SET info=@info, status=(SELECT id FROM EventStatus WHERE text= @status) WHERE id=@id;",
                        new Dictionary<string, string>() { { "info", eventProperties.text }, { "id", eventProperties.eventIdToUpdate.ToString() }, { "status", eventProperties.eventStatus } }, null);

                    if (affectedRows == 0)
                    {
                        throw new Exception("Error during event update operation (affectedRows != 1)");
                    }
                }
            }
            catch (Exception exp)
            {
                EventHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
            }
        }

        // sets the given event to done
        public static void setDoneEvent(Common.EventProperties eventProperties, string vmName)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    int affectedRows = dbConn.doWriteQuery("UPDATE JobExecutionEvents SET info=concat(info,@info), status=(SELECT id FROM EventStatus WHERE text= @status)  WHERE id=@id;",
                        new Dictionary<string, string>() { { "info", eventProperties.text }, { "id", eventProperties.eventIdToUpdate.ToString() }, { "status", eventProperties.eventStatus } }, null);

                    if (affectedRows == 0)
                    {
                        throw new Exception("Error during event update operation (affectedRows != 1)");
                    }
                }
            }
            catch (Exception exp)
            {
                EventHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
            }
        }

        //gets all events for a given job
        public static List<Dictionary<string,string>> getEvents (string jobId, string type)
        {
            //not an update: do insert
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    //get jobExecutions first
                    List<Dictionary<string, string>> jobExecutions = dbConn.doReadQuery("SELECT max(id) id FROM JobExecutions WHERE jobId = @jobId AND type = @type;",
                        new Dictionary<string, string>() { { "jobId", jobId }, {"type", type } }, null);

                    //check if executionId is available
                    string jobExecutionId = jobExecutions[0]["id"];
                    if (jobExecutionId == "" || jobExecutionId == "null")
                    {
                        return new List<Dictionary<string, string>>();
                    }


                    List<Dictionary<string, string>> jobExecutionEventIds = dbConn.doReadQuery("SELECT vmId, timeStamp, info, EventStatus.text AS status FROM JobExecutionEvents INNER JOIN EventStatus ON EventStatus.id = JobExecutionEvents.status WHERE jobExecutionId = @jobExecutionId;",
                        new Dictionary<string, string>() { { "jobExecutionId", jobExecutionId } }, null);

                    if (jobExecutionEventIds.Count == 0)
                    {
                        throw new Exception("Error during insert operation (affectedRows != 1)");
                    }
                    else
                    {
                        return jobExecutionEventIds;
                    }
                }
            }
            catch (Exception exp)
            {
                EventHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
                return new List<Dictionary<string, string>>();
            }
        }

        //deletes a given job (set deleted flag)
        public static bool deleteJob(int jobDBId)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {

                    int affectedRows = dbConn.doWriteQuery("UPDATE Jobs SET deleted=1 WHERE id=@id", new Dictionary<string, string>() { { "id", jobDBId.ToString() } }, null);
                    return affectedRows == 1;

                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}