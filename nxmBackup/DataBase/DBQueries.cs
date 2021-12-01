using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class DBQueries
    {

        //adds an entry to log table
        public static void addLog(string text, string stacktrace, Exception ex)
        {
            string exception = "no exception";

            if (ex != null)
            {
                exception = ex.Message;
            }

            using (DBConnection dbConn = new DBConnection())
            {
                dbConn.doWriteQuery("INSERT INTO log (text, stacktrace, exception) VALUES (@text, @stacktrace, @exception);",
                        new Dictionary<string, object>() { { "text", text }, { "stacktrace", stacktrace }, {"exception", exception } }, null);
            }
        }


        //writes the given settings to db
        public static void writeGlobalSettings(Dictionary<string, string> settings)
        {
            using (DBConnection dbConn = new DBConnection())
            {
                //start transaction
                NpgsqlTransaction transaction = dbConn.beginTransaction();

                //iterate through each key
                foreach(string key in settings.Keys)
                {
                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    parameters.Add("value", settings[key]);
                    parameters.Add("name", key);
                    dbConn.doWriteQuery("UPDATE settings SET value=@value WHERE name=@name;", parameters, transaction);
                }

                //commit transaction
                transaction.Commit();
            }
        }

        //reads a given global setting from db
        public static string readGlobalSetting(string setting)
        {
            using (DBConnection dbConn = new DBConnection())
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("setting", setting);
                List<Dictionary<string, object>> result = dbConn.doReadQuery("SELECT value FROM settings WHERE name=@setting;", parameters, null);

                //result valid?
                if (result == null || result.Count == 0)
                {
                    return null;
                }
                else
                {
                    return (string)result[0]["value"];
                }
            }
        }

        //reads all global settings from db
        public static Dictionary<string, string> readGlobalSettings()
        {
            using (DBConnection dbConn = new DBConnection())
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                List<Dictionary<string, object>> result = dbConn.doReadQuery("SELECT name, value FROM settings;", null, null);

                //result valid?
                if (result == null || result.Count == 0)
                {
                    return null;
                }
                else
                {
                    //convert obj to string
                    Dictionary<string, string> retVal = new Dictionary<string, string>();
                    foreach(Dictionary<string, object> oneSetting in result)
                    {
                        retVal.Add((string)oneSetting["name"], (string)oneSetting["value"]);
                    }
                    return retVal;
                }
            }
        }

        //deletes the old hdds from a given vm and adds new ones
        public static void refreshHDDs(List<VMHDD> hdds, string vmid)
        {
            using (DBConnection dbConn = new DBConnection())
            {
                NpgsqlTransaction transaction = dbConn.beginTransaction();

                //delete vm hdd relation
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("vmid", vmid);
                List<Dictionary<string, object>> result = dbConn.doReadQuery("DELETE FROM vmhddrelation WHERE vmid=@vmid;", parameters, transaction);

                //add new hdds and their relations to vm
                foreach(VMHDD hdd in hdds)
                {
                    //add hdd
                    parameters.Clear();
                    parameters.Add("name", hdd.name);
                    parameters.Add("path", hdd.path);
                    result = dbConn.doReadQuery("INSERT INTO hdds (name, path) VALUES (@name, @path) RETURNING id;", parameters, transaction);
                    int newHDDID = (int)(result[0]["id"]);

                    //add relation
                    parameters.Clear();
                    parameters.Add("vmid", vmid);
                    parameters.Add("hddid", newHDDID);
                    result = dbConn.doReadQuery("INSERT INTO vmhddrelation (vmid,hddid) VALUES (@vmid, @hddid);", parameters, transaction);

                }

                //commit transaction
                transaction.Commit();

            }
        }

        // Adds a new job execution before performing backup process.
        public static int addJobExecution(int jobId, string type)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    List<Dictionary<string, object>> jobExecutionIds = dbConn.doReadQuery("INSERT INTO jobexecutions (jobid, isrunning, transferrate, alreadyread, alreadywritten, successful, warnings, errors, type) " +
                        "VALUES(@jobId, @isRunning, @transferRate, @alreadyRead, @alreadyWritten, @successful, @warnings, @errors, @type) RETURNING id;",
                        new Dictionary<string, object>() {
                            { "jobId", jobId },
                            { "isRunning", true },
                            { "transferRate", 0 },
                            { "alreadyRead", 0 },
                            { "alreadyWritten", 0 },
                            { "successful", true },
                            { "warnings", 0 },
                            { "errors", 0 },
                            { "type", type }
                        }, null);

                    if (jobExecutionIds == null || jobExecutionIds.Count != 1)
                    {
                        throw new Exception("Error during insert operation (no insert id)");
                    }

                    return (int)(jobExecutionIds[0]["id"]);
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

            //add transferrate
            if (eventProperties.transferRate >= 0)
            {
                addTransferrate(eventProperties.jobExecutionId, eventProperties.transferRate);
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
                    List<Dictionary<string, object>> jobExecutionEventIds = dbConn.doReadQuery("INSERT INTO jobexecutionevents (vmid, info, jobexecutionid, status) VALUES (@vmId, @info, @jobExecutionId, (SELECT id FROM EventStatus WHERE text= @status)) RETURNING id;",
                        new Dictionary<string, object>() { { "vmId", vmName }, { "info", eventProperties.text }, { "jobExecutionId", eventProperties.jobExecutionId}, {"status", eventProperties.eventStatus} }, null);

                    if (jobExecutionEventIds == null || jobExecutionEventIds.Count == 0)
                    {
                        throw new Exception("Error during insert operation (affectedRows != 1)");
                    }
                    else
                    {
                        return (int)(jobExecutionEventIds[0]["id"]);
                    }
                }
            }
            catch (Exception exp)
            {
                EventHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
                return -1;
            }
        }

        //adds a tranferrate to DB
        private static void addTransferrate(int jobExecutionid, Int64 transferrate)
        {
            using (DBConnection dbConn = new DBConnection())
            {
                dbConn.doWriteQuery("INSERT INTO transferrates (jobexecutionid, transferrate) VALUES (@jobexecutionid, @transferrate);",
                        new Dictionary<string, object>() { { "jobexecutionid", jobExecutionid }, { "transferrate", transferrate } }, null);
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
                        new Dictionary<string, object>() { { "info", eventProperties.text }, { "id", eventProperties.eventIdToUpdate }, { "status", eventProperties.eventStatus } }, null);

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
                        new Dictionary<string, object>() { { "info", eventProperties.text }, { "id", eventProperties.eventIdToUpdate }, { "status", eventProperties.eventStatus } }, null);

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
        public static List<Dictionary<string,object>> getEvents (int jobId, string type)
        {
            //not an update: do insert
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    //get jobExecutions first
                    List<Dictionary<string, object>> jobExecutions = dbConn.doReadQuery("SELECT max(id) id FROM JobExecutions WHERE jobId = @jobId AND type = @type;",
                        new Dictionary<string, object>() { { "jobId", jobId }, {"type", type } }, null);

                    //check if executionId is available
                    string jobExecutionId = jobExecutions[0]["id"].ToString();
                    if (jobExecutionId == "" || jobExecutionId == "null")
                    {
                        return new List<Dictionary<string, object>>();
                    }


                    List<Dictionary<string, object>> jobExecutionEventIds = dbConn.doReadQuery("SELECT jobexecutionevents.id, vmid, timestamp, info, eventstatus.text AS status FROM jobexecutionevents INNER JOIN eventstatus ON eventstatus.id = jobexecutionevents.status WHERE jobexecutionid = @jobexecutionid ORDER BY jobexecutionevents.id DESC;",
                        new Dictionary<string, object>() { { "jobExecutionId", int.Parse(jobExecutionId) } }, null);

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
                return new List<Dictionary<string, object>>();
            }
        }

        //deletes a given job (set deleted flag)
        public static bool deleteJob(int jobDBId)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {

                    int affectedRows = dbConn.doWriteQuery("UPDATE Jobs SET deleted=TRUE WHERE id=@id", new Dictionary<string, object>() { { "id", jobDBId } }, null);
                    return affectedRows == 1;

                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        // Updates an existing execution.
        public static void updateJobExecution(Common.JobExecutionProperties executionProperties, string jobExecutionId)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    int affectedRows = dbConn.doWriteQuery("UPDATE JobExecutions SET stopTime=@stopTime, isRunning=@isRunning, transferRate=@transferRate, alreadyRead=@alreadyRead, alreadyWritten=@alreadyWritten, successful=@successful, warnings=@warnings, errors=@errors WHERE id=@id;",
                        new Dictionary<string, object>() { { "stopTime", executionProperties.stopTime.ToString() }, { "isRunning", executionProperties.isRunning.ToString() }, { "transferRate", executionProperties.transferRate.ToString() }, { "alreadyRead", executionProperties.alreadyRead.ToString() }, { "alreadyWritten", executionProperties.alreadyWritten.ToString() }, { "successful", executionProperties.successful.ToString() }, { "warnings", executionProperties.warnings.ToString() }, { "errors", executionProperties.errors.ToString() }, { "id", jobExecutionId.ToString() } }, null);

                    if (affectedRows == 0)
                    {
                        throw new Exception("Error during job execution update operation (affectedRows != 1)");
                    }
                }
            }
            catch (Exception exp)
            {
                EventHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
            }
        }

        //removes dynamic data from db
        public static void wipeDB()
        {
            using (DBConnection dbConn = new DBConnection())
            {
                NpgsqlTransaction transaction = dbConn.beginTransaction();
                dbConn.doWriteQuery("DELETE FROM log;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM jobvmrelation;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM vmhddrelation;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM vms;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM hdds;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM jobexecutionevents;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM jobexecutions;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM transferrates;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM jobs;", null, transaction);
                transaction.Commit();
            }
        }

    }
}