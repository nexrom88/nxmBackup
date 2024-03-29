using System.Data.SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Security;
using System.Windows.Controls.Primitives;
using System.Data.Entity.Infrastructure;
using System.Xml.Linq;
using ConfigHandler;
using System.IO;

namespace Common
{
    public class DBQueries
    {
        private static byte[] aesStaticKey = { 0x34, 0x2, 0xe3, 0xaa, 0x88, 0xf7, 0xbb, 0x9a, 0x71, 0x4b, 0x28, 0xa1, 0xc5, 0x04, 0xa7, 0xe1};

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
                dbConn.doWriteQuery("INSERT INTO log (text, stacktrace, exception, timestamp) VALUES (@text, @stacktrace, @exception, (datetime('now','localtime')));",
                        new Dictionary<string, object>() { { "text", text }, { "stacktrace", stacktrace }, { "exception", exception } }, null);
            }
        }


        //writes the given settings to db
        public static void writeGlobalSettings(Dictionary<string, string> settings)
        {
            using (DBConnection dbConn = new DBConnection())
            {
                //start transaction
                SQLiteTransaction transaction = dbConn.beginTransaction();

                //iterate through each key
                foreach (string key in settings.Keys)
                {
                    //ignore write when mailpassword is empty => no change
                    if (key == "mailpassword" && settings[key] == "")
                    {
                        continue;
                    }

                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    parameters.Add("value", settings[key]);
                    parameters.Add("name", key);

                    //insert or update
                    dbConn.doWriteQuery("INSERT INTO settings (name, value) VALUES (@name, @value) ON CONFLICT(name) DO UPDATE SET value=@value WHERE name=@name", parameters, transaction);
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

        //returns a host ip/name by a given host id
        public static WMIConnectionOptions getHostByID(int hostID, bool getAuthData)
        {
            WMIConnectionOptions options = new WMIConnectionOptions();

            using (DBConnection dbConn = new DBConnection())
            {
                string query;

                if (getAuthData)
                {
                    query = "SELECT host, user, password FROM hosts WHERE id=@id;";
                }
                else
                {
                    query = "SELECT host FROM hosts WHERE id=@id;";
                }

                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("id", hostID);

                List<Dictionary<string, object>> result = dbConn.doReadQuery(query, parameters, null);
                if (result == null || result.Count == 0)
                {
                    DBQueries.addLog("hyperv host could not be found within db", Environment.StackTrace, null);
                    options.host = null;
                    return options;
                }
                else
                {
                    options.host = (string)result[0]["host"];

                    if (getAuthData)
                    {
                        options.user = (string)result[0]["user"];
                        options.password = (string)result[0]["password"];
                    }
                    return options;
                }
            }
        }

        public static bool saveHyperVHost(HyperVHost hyperVHost)
        {
            using (DBConnection dbConn = new DBConnection())
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("description", hyperVHost.description);
                parameters.Add("host", hyperVHost.host);
                parameters.Add("user", hyperVHost.user);

                //have to add a new host entry?
                if (hyperVHost.id == "-1")
                {                    
                    parameters.Add("password", hyperVHost.password);

                    List<Dictionary<string, object>> result = dbConn.doReadQuery("INSERT INTO hosts(description, host, user, password) VALUES(@description, @host, @user, @password);", parameters, null);
                    return true;
                }
                else
                {
                    //edit a given host entry
                    parameters.Add("id", hyperVHost.id);
                    string updateQuery = "";
                    if (hyperVHost.password != null && hyperVHost.password != "")
                    {
                        //build query for also updating password
                        updateQuery = "UPDATE hosts SET description=@description, host=@host, user=@user, password=@password WHERE id=@id";
                        parameters.Add("password", hyperVHost.password);
                    }
                    else
                    {
                        //build query for not also updating password
                        updateQuery = "UPDATE hosts SET description=@description, host=@host, user=@user WHERE id=@id";
                    }

                    //do update query
                    List<Dictionary<string, object>> result = dbConn.doReadQuery(updateQuery, parameters, null);
                    return true;
                }

                
            }
        }

        //deletes a given hyperv host
        public static bool deleteHyperVHost(string id)
        {
            using (DBConnection dbConn = new DBConnection())
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();                
                parameters.Add("id", id);

                //check that host is not still in use within a job
                foreach (OneJob job in JobConfigHandler.Jobs)
                {
                    foreach (JobVM vm in job.JobVMs)
                    {
                        if (vm.hostID == id)
                        {
                            //host still in use
                            return false;
                        }
                    }
                }

                List<Dictionary<string, object>> result = dbConn.doReadQuery("UPDATE hosts SET deleted=TRUE WHERE id=@id", parameters, null);
                return true;
            }
        }

        //reads all configured HyperV hosts
        public static HyperVHost[] readHyperVHosts(bool readAuthData)
        {
            HyperVHost[] hostsArray;
            string sqlQuery;

            //which sql query to use?
            if (readAuthData)
            {
                sqlQuery = "SELECT id, description, host, user, password FROM hosts WHERE deleted=FALSE;";
            }
            else
            {
                sqlQuery = "SELECT id, description, user, host FROM hosts WHERE deleted=FALSE;";
            }

            using (DBConnection dbConn = new DBConnection())
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                List<Dictionary<string, object>> result = dbConn.doReadQuery(sqlQuery, null, null);

                //result valid?
                if (result == null || result.Count == 0)
                {
                    return null;
                }
                else
                {
                    hostsArray = new HyperVHost[result.Count];
                    int counter = 0;

                    //iterate through every result set
                    foreach (Dictionary<string, object> oneHostSet in result)
                    {
                        hostsArray[counter] = new HyperVHost();
                        hostsArray[counter].id =  oneHostSet["id"].ToString();
                        hostsArray[counter].description = (string)oneHostSet["description"];
                        hostsArray[counter].host = (string)oneHostSet["host"];
                        hostsArray[counter].user = (string)oneHostSet["user"];

                        if (readAuthData)
                        {
                            hostsArray[counter].password = (string)oneHostSet["password"];
                        }

                        counter++;
                    }

                    return hostsArray;

                }
            }
        }

        //reads all global settings from db
        public static Dictionary<string, string> readGlobalSettings(bool readPasswords, bool readOTPKey)
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
                    foreach (Dictionary<string, object> oneSetting in result)
                    {
                        //filter mailpassword to not sending it to frontend
                        if ((string)oneSetting["name"] == "mailpassword" && !readPasswords)
                        {
                            continue;
                        }

                        //filter otpkey to not sending it to frontend
                        if ((string)oneSetting["name"] == "otpkey" && !readOTPKey)
                        {
                            if ((string)oneSetting["value"] != "")
                            {
                                retVal.Add((string)oneSetting["name"], "1");
                            }
                            else
                            {
                                retVal.Add((string)oneSetting["name"], "");
                            }

                            continue;
                        }

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
                SQLiteTransaction transaction = dbConn.beginTransaction();

                //delete vm hdd relation
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("vmid", vmid);
                List<Dictionary<string, object>> result = dbConn.doReadQuery("DELETE FROM vmhddrelation WHERE vmid=@vmid;", parameters, transaction);

                //add new hdds and their relations to vm
                foreach (VMHDD hdd in hdds)
                {
                    //add hdd
                    parameters.Clear();
                    parameters.Add("name", hdd.name);
                    parameters.Add("path", hdd.path);
                    result = dbConn.doReadQuery("INSERT INTO hdds (name, path) VALUES (@name, @path);", parameters, transaction);
                    int newHDDID = (int)dbConn.getLastInsertedID();

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
                    dbConn.doReadQuery("INSERT INTO jobexecutions (jobid, isrunning, bytesprocessed, bytestransfered, successful, warnings, errors, type, startstamp) " +
                        "VALUES(@jobId, @isRunning, @bytesprocessed, @bytestransfered, @successful, @warnings, @errors, @type, (datetime('now','localtime')));",
                        new Dictionary<string, object>() {
                            { "jobId", jobId },
                            { "isRunning", true },
                            { "bytesprocessed", 0 },
                            { "bytestransfered", 0 },
                            { "successful", true },
                            { "warnings", 0 },
                            { "errors", 0 },
                            { "type", type },
                        }, null);

                    int newExecutionID = (int)dbConn.getLastInsertedID();

                    if (newExecutionID <1)
                    {
                        throw new Exception("Error during insert operation (no insert id)");
                    }

                    return newExecutionID;
                }
            }
            catch (Exception ex)
            {
                Common.DBQueries.addLog("error on adding jobExecution to DB", Environment.StackTrace, ex);
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
                addRate(eventProperties.jobExecutionId, eventProperties.transferRate, eventProperties.processRate);
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
                    List<Dictionary<string, object>> jobExecutionEventIds = dbConn.doReadQuery("INSERT INTO jobexecutionevents (vmid, info, jobexecutionid, status, timestamp) VALUES (@vmId, @info, @jobExecutionId, (SELECT id FROM EventStatus WHERE text= @status), (datetime('now','localtime')));",
                        new Dictionary<string, object>() { { "vmId", vmName }, { "info", eventProperties.text }, { "jobExecutionId", eventProperties.jobExecutionId }, { "status", eventProperties.eventStatus } }, null);

                    int newEventID = (int)dbConn.getLastInsertedID();

                    if (newEventID <1)
                    {
                        throw new Exception("Error during insert operation (affectedRows != 1)");
                    }
                    else
                    {
                        return newEventID;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.DBQueries.addLog("error on adding event to DB", Environment.StackTrace, ex);
                return -1;
            }
        }

        //adds a tranferrate to DB
        private static void addRate(int jobExecutionid, Int64 transferrate, Int64 processrate)
        {
            using (DBConnection dbConn = new DBConnection())
            {
                dbConn.doWriteQuery("INSERT INTO rates (jobexecutionid, transferrate, processrate) VALUES (@jobexecutionid, @transferrate, @processrate);",
                        new Dictionary<string, object>() { { "jobexecutionid", jobExecutionid }, { "transferrate", transferrate }, { "processrate", processrate } }, null);
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
            catch (Exception ex)
            {
                Common.DBQueries.addLog("error on updating event", Environment.StackTrace, ex);
            }
        }

        // sets the given event to done
        public static void setDoneEvent(Common.EventProperties eventProperties, string vmName)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    int affectedRows = dbConn.doWriteQuery("UPDATE JobExecutionEvents SET info= info || @info, status=(SELECT id FROM EventStatus WHERE text= @status)  WHERE id=@id;",
                        new Dictionary<string, object>() { { "info", eventProperties.text }, { "id", eventProperties.eventIdToUpdate }, { "status", eventProperties.eventStatus } }, null);

                    if (affectedRows == 0)
                    {
                        throw new Exception("Error during event update operation (affectedRows != 1)");
                    }
                }
            }
            catch (Exception ex)
            {
                Common.DBQueries.addLog("error on setting done event", Environment.StackTrace, ex);
            }
        }


        //checks whether a given jobexecution is still running (used for full restore)
        public static bool isRestoreRunning(int jobId)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    //get jobExecutions first
                    List<Dictionary<string, object>> jobExecutions = dbConn.doReadQuery("SELECT isrunning FROM JobExecutions WHERE jobId = @jobId AND type = 'restore' ORDER BY id DESC LIMIT 1;",
                        new Dictionary<string, object>() { { "jobId", jobId }}, null);

                    //is retval available?
                    if (jobExecutions == null || jobExecutions.Count == 0)
                    {
                        return false;
                    }


                    return int.Parse(jobExecutions[0]["isrunning"].ToString()) == 1 ? true : false ;

                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        //gets all events for a given job
        public static List<Dictionary<string, object>> getEvents(int jobId, string type)
        {
            //not an update: do insert
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    //get jobExecutions first
                    List<Dictionary<string, object>> jobExecutions = dbConn.doReadQuery("SELECT max(id) id FROM JobExecutions WHERE jobId = @jobId AND type = @type;",
                        new Dictionary<string, object>() { { "jobId", jobId }, { "type", type } }, null);

                    //check if executionId is available
                    string jobExecutionId = jobExecutions[0]["id"].ToString();
                    if (jobExecutionId == "" || jobExecutionId == "null")
                    {
                        return new List<Dictionary<string, object>>();
                    }


                    List<Dictionary<string, object>> jobExecutionEventIds = dbConn.doReadQuery("SELECT jobexecutionevents.id, vmid, timestamp, info, eventstatus.text AS status FROM jobexecutionevents INNER JOIN eventstatus ON eventstatus.id = jobexecutionevents.status WHERE jobexecutionid = @jobexecutionid ORDER BY jobexecutionevents.id DESC;",
                        new Dictionary<string, object>() { { "jobExecutionId", int.Parse(jobExecutionId) } }, null);


                    return jobExecutionEventIds;

                }
            }
            catch (Exception ex)
            {
                Common.DBQueries.addLog("error on getting events from DB", Environment.StackTrace, ex);
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
                    //mark job as deleted
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
        public static void closeJobExecution(Common.JobExecutionProperties executionProperties, string jobExecutionId)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    //close jobexecution
                    int affectedRows = dbConn.doWriteQuery("UPDATE jobexecutions SET stoptime=(datetime('now','localtime')), isrunning=false, bytesProcessed=@bytesProcessed, bytesTransfered=@bytesTransfered, successful=@successful, warnings=@warnings, errors=@errors WHERE id=@id;",
                        new Dictionary<string, object>() { { "bytesprocessed", (Int64)executionProperties.bytesProcessed }, { "bytestransfered", (Int64)executionProperties.bytesTransfered }, { "successful", executionProperties.successful }, { "warnings", executionProperties.warnings }, { "errors", executionProperties.errors }, { "id", int.Parse(jobExecutionId) } }, null);

                    if (affectedRows == 0)
                    {
                        throw new Exception("Error during job execution update operation (affectedRows != 1)");
                    }
                }
            }
            catch (Exception ex)
            {
                Common.DBQueries.addLog("error on closing job execution within DB", Environment.StackTrace, ex);
            }
        }

        //forces to set a given job to "stopped
        public static void forceStopExecution(int jobID)
        {
            using (DBConnection dbConn = new DBConnection())
            {
                //get jobExecutions first
                List<Dictionary<string, object>> jobExecutions = dbConn.doReadQuery("SELECT max(id) id FROM JobExecutions WHERE jobId = @jobId AND type = @type;",
                    new Dictionary<string, object>() { { "jobId", jobID }, { "type", "backup" } }, null);

                //check if executionId is available
                if (jobExecutions == null || jobExecutions.Count == 0)
                {
                    return;
                }
                int jobExecutionId = int.Parse(jobExecutions[0]["id"].ToString());

                //now close the read job execution
                dbConn.doWriteQuery("UPDATE jobexecutions SET stoptime=(datetime('now','localtime')), isrunning=false, bytesProcessed=@bytesProcessed, bytesTransfered=@bytesTransfered, successful=@successful, warnings=@warnings, errors=@errors WHERE id=@id;",
                    new Dictionary<string, object>() { { "bytesprocessed", 0 }, { "bytestransfered", 0 }, { "successful", 0 }, { "warnings", 0 }, { "errors", 1 }, { "id", jobExecutionId } }, null);
            }
        }


        //removes dynamic data from db
        public static void wipeDB()
        {
            using (DBConnection dbConn = new DBConnection())
            {
                SQLiteTransaction transaction = dbConn.beginTransaction();
                dbConn.doWriteQuery("DELETE FROM log;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM storagetarget;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM jobvmrelation;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM vmhddrelation;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM vms;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM hdds;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM jobexecutionevents;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM jobexecutions;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM rates;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM jobs;", null, transaction);
                dbConn.doWriteQuery("DELETE FROM hosts WHERE id > 1;", null, transaction);
                wipeSettings(dbConn, transaction);
                transaction.Commit();
                dbConn.doWriteQuery("VACUUM;", null, null);
            }
        }

        //clears the settings table
        private static void wipeSettings(DBConnection dbConn, SQLiteTransaction transaction)
        {
            dbConn.doWriteQuery("UPDATE settings SET value = \"\" WHERE name=\"mailserver\"", null, transaction);
            dbConn.doWriteQuery("UPDATE settings SET value = \"false\" WHERE name=\"mailssl\"", null, transaction);
            dbConn.doWriteQuery("UPDATE settings SET value = \"\" WHERE name=\"mailuser\"", null, transaction);
            dbConn.doWriteQuery("UPDATE settings SET value = \"\" WHERE name=\"mailpassword\"", null, transaction);
            dbConn.doWriteQuery("UPDATE settings SET value = \"\" WHERE name=\"mailsender\"", null, transaction);
            dbConn.doWriteQuery("UPDATE settings SET value = \"\" WHERE name=\"mailrecipient\"", null, transaction);
            dbConn.doWriteQuery("UPDATE settings SET value = \"en\" WHERE name=\"language\"", null, transaction);
        }

        //encrypts a given plain text password and returns its base64 string
        private static string encrpytPassword (string password)
        {
            AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider ();
            aesProvider.Key = aesStaticKey;
            aesProvider.GenerateIV();

            //init crypto system
            ICryptoTransform encryptor = aesProvider.CreateEncryptor(aesProvider.Key, aesProvider.IV);
            MemoryStream memStream = new MemoryStream ();

            //write iv length to mem stream
            memStream.Write(BitConverter.GetBytes(aesProvider.IV.Length), 0, 4);

            //write iv to mem stream
            memStream.Write(aesProvider.IV, 0, aesProvider.IV.Length);

            //start crypto stream
            CryptoStream cryptoStream = new CryptoStream(memStream, encryptor, CryptoStreamMode.Write);

            //encrypt pw
            byte[] passwordBytes = Encoding.UTF8.GetBytes (password);
            cryptoStream.Write(passwordBytes, 0, passwordBytes.Length);
            cryptoStream.FlushFinalBlock();

            //cleanup
            string retVal = Convert.ToBase64String(memStream.ToArray());
            cryptoStream.Close();
            memStream.Close();

            return retVal;

        }

    }

    public struct LBTimestamps
    {
        public string start;
        public string end;
    }

    public struct HyperVHost
    {
        public string id;
        public string host;
        public string description;
        public string user;
        public string password;
    }

}