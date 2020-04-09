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
        public static int addJobExecution(string jobId)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    List<Dictionary<string, string>> jobExecutionIds = dbConn.doReadQuery("INSERT INTO JobExecutions (jobId, isRunning, transferRate, alreadyRead, alreadyWritten, successful, warnings, errors) " +
                        "VALUES(@jobId, @isRunning, @transferRate, @alreadyRead, @alreadyWritten, @successful, @warnings, @errors);SELECT SCOPE_IDENTITY() AS id;",
                        new Dictionary<string, string>() {
                            { "jobId", jobId },
                            { "isRunning", "1" },
                            { "transferRate", "0" },
                            { "alreadyRead", "0" },
                            { "alreadyWritten", "0" },
                            { "successful", "0" },
                            { "warnings", "0" },
                            { "errors", "0" }
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
                ErrorHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
                return -1;
            }
        }

        // Adds events to db while performing backup process.
        public static int addEvent(Common.EventProperties eventProperties, string vmName)
        {
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
                    List<Dictionary<string, string>> jobExecutionEventIds = dbConn.doReadQuery("INSERT INTO JobExecutionEvents (vmId, info, jobExecutionId) VALUES (@vmId, @info, @jobExecutionId);SELECT SCOPE_IDENTITY() AS id;",
                        new Dictionary<string, string>() { { "vmId", vmName }, { "info", eventProperties.text }, { "jobExecutionId", eventProperties.jobExecutionId.ToString()} }, null);

                    if (jobExecutionEventIds.Count == 0)
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
                ErrorHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
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
                    int affectedRows = dbConn.doWriteQuery("UPDATE JobExecutionEvents SET info=@info WHERE id=@id;",
                        new Dictionary<string, string>() { { "info", eventProperties.text }, { "id", eventProperties.eventIdToUpdate.ToString() } }, null);

                    if (affectedRows == 0)
                    {
                        throw new Exception("Error during event update operation (affectedRows != 1)");
                    }
                }
            }
            catch (Exception exp)
            {
                ErrorHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
            }
        }

        // sets the given event to done
        public static void setDoneEvent(Common.EventProperties eventProperties, string vmName)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    int affectedRows = dbConn.doWriteQuery("UPDATE JobExecutionEvents SET info=concat(info,@info) WHERE id=@id;",
                        new Dictionary<string, string>() { { "info", eventProperties.text }, { "id", eventProperties.eventIdToUpdate.ToString() } }, null);

                    if (affectedRows == 0)
                    {
                        throw new Exception("Error during event update operation (affectedRows != 1)");
                    }
                }
            }
            catch (Exception exp)
            {
                ErrorHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
            }
        }

        //gets all events for a given job
        public static List<Dictionary<string,string>> getEvents (string jobId)
        {
            //not an update: do insert
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    //get jobExecutions first
                    List<Dictionary<string, string>> jobExecutions = dbConn.doReadQuery("SELECT max(id) id FROM JobExecutions WHERE jobId = @jobId;",
                        new Dictionary<string, string>() { { "jobId", jobId } }, null);

                    //check if executionId is available
                    string jobExecutionId = jobExecutions[0]["id"];
                    if (jobExecutionId == "" || jobExecutionId == "null")
                    {
                        return new List<Dictionary<string, string>>();
                    }


                    List<Dictionary<string, string>> jobExecutionEventIds = dbConn.doReadQuery("SELECT vmId, timeStamp, info FROM JobExecutionEvents WHERE jobExecutionId = @jobExecutionId;",
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
                ErrorHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
                return new List<Dictionary<string, string>>();
            }
        }
    }
}