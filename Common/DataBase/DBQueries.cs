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
        public static string AddJobExecution(string jobId)
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

                    return jobExecutionIds[0]["id"];
                }
            }
            catch (Exception exp)
            {
                ErrorHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
                return null;
            }
        }

        // Adds events to db while performing backup process.
        public static void AddEvent(string text, string jobExecutionId, string vmName)
        {
            try
            {
                using (DBConnection dbConn = new DBConnection())
                {
                    int affectedRows = dbConn.doWriteQuery("INSERT INTO JobExecutionEvents (vmId, info, jobExecutionId) VALUES (@vmId, @info, @jobExecutionId);",
                        new Dictionary<string, string>() { { "vmId", vmName }, { "info", text }, { "jobExecutionId", jobExecutionId } }, null);

                    if (affectedRows != 1)
                    {
                        throw new Exception("Error during insert operation (affectedRows != 1)");
                    }
                }
            }
            catch (Exception exp)
            {
                ErrorHandler.writeToLog(exp.ToString(), new System.Diagnostics.StackTrace());
            }
        }
    }
}