using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Common;
using System.Windows.Forms;
using Npgsql;

namespace ConfigHandler
{
    public class JobConfigHandler
    {
        private static Object lockObj = new object();

        //reads and returns all jobs
        public static List<OneJob> readJobs()
        {
            List<OneJob> retVal = new List<OneJob>();

            //open DB connection
            using (Common.DBConnection connection = new Common.DBConnection())
            {
                List<Dictionary<string, string>> jobs = connection.doReadQuery("SELECT jobs.id, jobs.name, jobs.isRunning, jobs.basepath, jobs.maxelements, jobs.blocksize, jobs.day, jobs.hour, jobs.minute, jobs.interval, rotationtype.name AS rotationname FROM jobs INNER JOIN rotationtype ON jobs.rotationtypeid=rotationtype.id WHERE jobs.deleted=FALSE;", null, null);

                //check that jobs != null
                if (jobs == null) //DB error
                {
                    return null;
                }

                //iterate through all jobs
                foreach (Dictionary<string, string> job in jobs)
                {
                    //build structure
                    OneJob newJob = new OneJob();
                    newJob.DbId = int.Parse(job["id"]);
                    newJob.BasePath = job["basepath"];
                    newJob.Name = job["name"];
                    newJob.BlockSize = int.Parse(job["blocksize"]);
                    newJob.IsRunning = bool.Parse(job["isrunning"]);

                    // build nextRun string
                    newJob.NextRun = $"{int.Parse(job["hour"]).ToString("00")}:{int.Parse(job["minute"]).ToString("00")}";
                    if (job["day"] != "") newJob.NextRun += $" ({job["day"]})";

                    var rota = new Rotation();
                    //build rotation structure
                    switch (job["rotationname"])
                    {
                        case "merge":
                            rota.type = RotationType.merge;
                            break;
                        case "blockrotation":
                            rota.type = RotationType.blockRotation;
                            break;
                    }

                    rota.maxElementCount = int.Parse(job["maxelements"]);
                    newJob.Rotation = rota;


                    //build interval structure
                    Interval interval = new Interval();
                    switch (job["interval"])
                    {
                        case "hourly":
                            interval.intervalBase = IntervalBase.hourly;
                            break;
                        case "daily":
                            interval.intervalBase = IntervalBase.daily;
                            break;
                        case "weekly":
                            interval.intervalBase = IntervalBase.weekly;
                            break;
                    }
                    interval.day = job["day"];
                    interval.minute = int.Parse(job["minute"]);
                    interval.hour = int.Parse(job["hour"]);
                    newJob.Interval = interval;

                    //query VMs
                    Dictionary<string, object> paramaters = new Dictionary<string, object>();
                    paramaters.Add("jobid", int.Parse(job["id"]));
                    List<Dictionary<string, string>> vms = connection.doReadQuery("SELECT VMs.id, VMs.name FROM vms INNER JOIN jobvmrelation ON JobVMRelation.jobid=@jobid AND jobvmrelation.vmid=VMs.id", paramaters, null);
                    newJob.JobVMs = new List<JobVM>();

                    //iterate through all vms
                    foreach (Dictionary<string, string> vm in vms)
                    {
                        JobVM newVM = new JobVM();
                        newVM.vmID = vm["id"];
                        newVM.vmName = vm["name"];
                        newJob.JobVMs.Add(newVM);
                    }

                    //get last jobExecution attributes

                    List<Dictionary<string, string>> jobExecutions = connection.doReadQuery("SELECT * FROM jobexecutions WHERE jobexecutions.jobid=@jobid and jobexecutions.id = (SELECT MAX(id) FROM jobexecutions WHERE jobexecutions.jobid=@jobid)", paramaters, null);

                    if (jobExecutions.Count > 1) MessageBox.Show("db error: jobExecutions hat mehr als 1 result");
                    else
                    {
                        foreach (Dictionary<string, string> jobExecution in jobExecutions)
                        {
                            newJob.LastRun = jobExecution["startstamp"];
                            newJob.Successful = jobExecution["successful"];
                        }
                            
                    }

                    retVal.Add(newJob);

                }

                return retVal;

            }
        }

        //adds a job to the job list
        public static void addJob(OneJob job)
        {

            //open DB connection
            using (Common.DBConnection connection = new Common.DBConnection())
            {
                string intervalString = job.Interval.intervalBase.ToString();

                Dictionary<string, object> parameters = new Dictionary<string, object>();

                //start DB transaction
                NpgsqlTransaction transaction = connection.beginTransaction();

                List<Dictionary<string, string>> values;

                parameters = new Dictionary<string, object>();
                //get rotationtype ID
                parameters.Add("name", job.Rotation.type.ToString().ToLower());
                values = connection.doReadQuery("SELECT id FROM RotationType WHERE name=@name", parameters, transaction);
                int rotationID = int.Parse(values[0]["id"]);

                //create job entry
                parameters = new Dictionary<string, object>();
                parameters.Add("name", job.Name);
                parameters.Add("interval", intervalString);
                parameters.Add("minute", job.Interval.minute);
                parameters.Add("hour", job.Interval.hour);
                parameters.Add("day", job.Interval.day);
                parameters.Add("basepath", job.BasePath);
                parameters.Add("blocksize", job.BlockSize);
                parameters.Add("maxelements", job.Rotation.maxElementCount);
                parameters.Add("rotationtypeID", rotationID);

                values = connection.doReadQuery("INSERT INTO jobs (name, interval, minute, hour, day, basepath, blocksize, maxelements, rotationtypeid) VALUES(@name, @interval, @minute, @hour, @day, @basepath, @blocksize, @maxelements, @rotationtypeID) RETURNING id;", parameters, transaction);

                int jobID = int.Parse(values[0]["id"]);

                createJobVMRelation(job, jobID, connection, transaction);

                //commit transaction
                transaction.Commit();
            }

        }

        //creates a job-vms relation
        private static void createJobVMRelation(OneJob job, int jobID, Common.DBConnection connection, NpgsqlTransaction transaction)
        {
            //iterate through all vms
            foreach (JobVM vm in job.JobVMs)
            {
                //check whether vm already exists
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("id", vm.vmID);
                List<Dictionary<string, string>> result = connection.doReadQuery("SELECT COUNT(*) AS count From VMs WHERE id=@id", parameters, transaction);

                //does vm already exist in DB?
                if (int.Parse(result[0]["count"]) == 0)
                {
                    //vm does not exist
                    parameters = new Dictionary<string, object>();
                    parameters.Add("id", vm.vmID);
                    parameters.Add("name", vm.vmName);
                    connection.doReadQuery("INSERT INTO VMs(id, name) VALUES (@id, @name) RETURNING id;", parameters, transaction);
                }

                //vm exists now, now create relation
                parameters = new Dictionary<string, object>();
                parameters.Add("jobid", jobID);
                parameters.Add("vmid", vm.vmID);
                connection.doReadQuery("INSERT INTO JobVMRelation(jobid, vmid) VALUES (@jobid, @vmid)", parameters, transaction);
            }

        }

        // Check if job is running.
        public static bool isJobRunning(int jobId)
        {
            using (Common.DBConnection connection = new Common.DBConnection())
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("jobId", jobId.ToString());
                List<Dictionary<string, string>> result = connection.doReadQuery("SELECT isRunning FROM Jobs WHERE id=@jobId", parameters, null);
                if (result != null && result.Count > 0 && result[0]["id"] != "") return bool.Parse(result[0]["id"]);
                else return false;
            }
        }

        // Delete job.
        public static bool deleteJob(int jobDBId)
        {
            return Common.DBQueries.deleteJob(jobDBId);
        }

    }

    // Structures:

    //represents one job within jobs.xml
    public struct OneJob : System.ComponentModel.INotifyPropertyChanged
    {
        private int dbId;
        private string name;
        private Interval interval;
        private List<JobVM> jobVMs;
        private string basePath;
        private int blockSize;
        private Rotation rotation;
        private bool isRunning;
        private string nextRun;
        private string lastRun;
        private bool successful;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get => name; set => name = value; }
        public Interval Interval { get => interval; set => interval = value; }
        public List<JobVM> JobVMs { get => jobVMs; set => jobVMs = value; }
        public string BasePath { get => basePath; set => basePath = value; }
        public int BlockSize { get => blockSize; set => blockSize = value; }
        public Rotation Rotation { get => rotation; set => rotation = value; }
        public bool IsRunning { get => isRunning; set => isRunning = value; }
        public int DbId { get => dbId; set => dbId = value; }
        public string IntervalBaseForGUI
        {
            get
            {
                switch (interval.intervalBase)
                {
                    case IntervalBase.daily:
                        return "täglich";
                    case IntervalBase.hourly:
                        return "stündlich";
                    case IntervalBase.weekly:
                        return "wöchentlich";
                    default:
                        return "default";
                }
            }
        }
        public string IsRunningForGUI
        {
            get
            {
                switch (isRunning)
                {
                    case true:
                        return "läuft";
                    case false:
                        return "angehalten";
                    default:
                        return "default";
                }
            }
        }
  
        public string NextRun { get => nextRun; set => nextRun = value; }
        public string LastRun { get => lastRun; set => lastRun = value; }
        public string Successful 
        { 
            get 
            { 
                switch(successful)
                {
                    case true:
                        return "erfolgreich";
                    case false:
                        return "fehlgeschlagen";
                    default:
                        return "default";                        
                }
            }
            set => successful = bool.Parse(value); 
        }
    }

    

}