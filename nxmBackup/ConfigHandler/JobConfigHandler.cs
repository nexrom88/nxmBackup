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
using System.Data.SQLite;
using nxmBackup.HVBackupCore;

namespace ConfigHandler
{
    public class JobConfigHandler
    {
        private static Object lockObj = new object();
        private static List<OneJob> jobs;

        public static List<OneJob> Jobs { get => jobs; }

        //reads all jobs or just a given job from db to a given object
        public static void readJobsFromDB(List<OneJob> target, int jobid = -1)
        {

            //open DB connection
            using (Common.DBConnection connection = new Common.DBConnection())
            {
                string queryExtension = ";";

                //build query extension when just one job should be read
                if (jobid > -1)
                {
                    queryExtension = " AND jobs.id=" + jobid;
                }

                List<Dictionary<string, object>> jobsDB = connection.doReadQuery("SELECT storagetarget.targettype, storagetarget.targetpath, storagetarget.targetuser,storagetarget.targetpassword, jobs.id, jobs.enabled, jobs.name, jobs.incremental, jobs.maxelements, jobs.blocksize, jobs.day, jobs.hour, jobs.minute, jobs.interval, jobs.livebackup, jobs.useencryption, jobs.aeskey, jobs.usededupe, rotationtype.name AS rotationname FROM jobs INNER JOIN rotationtype ON jobs.rotationtypeid=rotationtype.id INNER JOIN storagetarget ON jobs.id=storagetarget.targetjobid WHERE jobs.deleted=FALSE" + queryExtension, null, null);

                //check that jobs != null
                if (jobsDB == null) //DB error
                {
                    return;
                }

                int lbHDDObjectID = 1;
                //iterate through all jobs
                foreach (Dictionary<string, object> jobDB in jobsDB)
                {
                    //build structure
                    OneJob newJob = new OneJob();
                    newJob.DbId = Convert.ToInt32(jobDB["id"]);
                    newJob.Enabled = Convert.ToBoolean(jobDB["enabled"]);
                    newJob.Name = jobDB["name"].ToString();
                    newJob.BlockSize = Convert.ToInt32(jobDB["blocksize"]);
                    newJob.LiveBackup = Convert.ToBoolean(jobDB["livebackup"]);
                    newJob.Incremental = Convert.ToBoolean(jobDB["incremental"]);
                    newJob.UsingDedupe = Convert.ToBoolean(jobDB["usededupe"]);
                    newJob.TargetType = jobDB["targettype"].ToString();
                    newJob.TargetPath = jobDB["targetpath"].ToString();
                    newJob.TargetUsername = jobDB["targetuser"].ToString();
                    newJob.TargetPassword = jobDB["targetpassword"].ToString();

                    newJob.UseEncryption = Convert.ToBoolean(jobDB["useencryption"]);

                    if (newJob.UseEncryption)
                    {
                        newJob.AesKey = (byte[])jobDB["aeskey"];
                    }

                    // build nextRun string
                    //newJob.NextRun = $"{((int)jobDB["hour"]).ToString("00")}:{((int)jobDB["minute"]).ToString("00")}";
                    //if (jobDB["day"].ToString() != "") newJob.NextRun += $" ({jobDB["day"]})";



                    var rota = new Rotation();
                    //build rotation structure
                    switch (jobDB["rotationname"])
                    {
                        case "merge":
                            rota.type = RotationType.merge;
                            break;
                        case "blockrotation":
                            rota.type = RotationType.blockRotation;
                            break;
                    }

                    rota.maxElementCount = Convert.ToInt32(jobDB["maxelements"]);
                    newJob.Rotation = rota;


                    //build interval structure
                    Interval interval = new Interval();
                    switch (jobDB["interval"])
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
                    interval.day = jobDB["day"].ToString();
                    interval.minute = Convert.ToInt32(jobDB["minute"]);
                    interval.hour = Convert.ToInt32(jobDB["hour"]);
                    newJob.Interval = interval;

                    //query VMs
                    Dictionary<string, object> paramaters = new Dictionary<string, object>();
                    paramaters.Add("jobid", Convert.ToInt32(jobDB["id"]));
                    List<Dictionary<string, object>> vms = connection.doReadQuery("SELECT VMs.id, VMs.name FROM vms INNER JOIN jobvmrelation ON JobVMRelation.jobid=@jobid AND jobvmrelation.vmid=VMs.id", paramaters, null);
                    newJob.JobVMs = new List<JobVM>();

                    //iterate through all vms
                    foreach (Dictionary<string, object> vm in vms)
                    {
                        JobVM newVM = new JobVM();
                        newVM.vmID = vm["id"].ToString();
                        newVM.vmName = vm["name"].ToString();

                        //read vm hdds
                        paramaters.Clear();
                        paramaters.Add("vmid", vm["id"]);
                        List<Dictionary<string, object>> hdds = connection.doReadQuery("SELECT hdds.name, hdds.path FROM hdds INNER JOIN vmhddrelation ON hdds.id=vmhddrelation.hddid WHERE vmhddrelation.vmid=@vmid;", paramaters, null);

                        newVM.vmHDDs = new List<VMHDD>();

                        //iterate through all hdds
                        foreach (Dictionary<string, object> oneHDD in hdds)
                        {
                            VMHDD newHDD = new VMHDD();
                            newHDD.name = oneHDD["name"].ToString();
                            newHDD.path = oneHDD["path"].ToString();
                            newHDD.lbObjectID = lbHDDObjectID;
                            newVM.vmHDDs.Add(newHDD);
                            lbHDDObjectID++;
                        }

                        newJob.JobVMs.Add(newVM);

                    }



                    //get last jobExecution attributes
                    paramaters.Clear();
                    paramaters.Add("jobid", Convert.ToInt32(jobDB["id"]));
                    List<Dictionary<string, object>> jobExecutions = connection.doReadQuery("SELECT * FROM jobexecutions WHERE jobexecutions.jobid=@jobid and jobexecutions.id = (SELECT MAX(id) FROM jobexecutions WHERE jobexecutions.jobid=@jobid AND jobexecutions.type='backup')", paramaters, null);

                    if (jobExecutions.Count > 1) MessageBox.Show("db error: jobExecutions hat mehr als 1 result");
                    else if (jobExecutions.Count == 1){
                        foreach (Dictionary<string, object> jobExecution in jobExecutions)
                        {
                            newJob.LastRun = jobExecution["startstamp"].ToString();

                            newJob.LastStop = jobExecution["stoptime"].ToString();

                            newJob.Successful = Convert.ToBoolean(jobExecution["successful"]).ToString();
                           
                            newJob.IsRunning = Convert.ToBoolean(jobExecution["isrunning"]);

                            newJob.LastBytesProcessed = UInt64.Parse(jobExecution["bytesprocessed"].ToString());

                            newJob.LastBytesTransfered = UInt64.Parse(jobExecution["bytestransfered"].ToString());

                            //read last transferrate
                            paramaters.Clear();
                            paramaters.Add("jobexecutionid", jobExecution["id"]);
                            List<Dictionary<string, object>> rates = connection.doReadQuery("SELECT transferrate, processrate FROM rates WHERE jobexecutionid=@jobexecutionid ORDER BY id ASC", paramaters, null);
                            if (rates != null)
                            {
                                newJob.Rates = new OneJob.Rate[rates.Count];
                                UInt32 counter = 0;
                                foreach (Dictionary<String, Object> rate in rates)
                                {
                                    newJob.Rates[counter].transfer = UInt32.Parse(rate["transferrate"].ToString());
                                    newJob.Rates[counter].process = UInt32.Parse(rate["processrate"].ToString());
                                    counter++;
                                }
                            }

                        }

                    }
                    else
                    { //no jobExecutions yet
                        newJob.LastRun = "";

                        newJob.Successful = "true";

                        newJob.IsRunning = false;
                    }

                    target.Add(newJob);

                }
            }
        }


        //reads all jobs from the DB and loads it
        public static void readJobsFromDB()
        {
            jobs = new List<OneJob>();
            readJobsFromDB(jobs);
        }


        //updates a given job
        public static void updateJob(OneJob job, int updatedJobID)
        {
            //open DB connection
            using (Common.DBConnection connection = new Common.DBConnection())
            {
                string intervalString = job.Interval.intervalBase.ToString();

                Dictionary<string, object> parameters = new Dictionary<string, object>();

                //start DB transaction
                SQLiteTransaction transaction = connection.beginTransaction();

                List<Dictionary<string, object>> values;
                parameters.Add("id", updatedJobID);

                //delete possible storagetarget
                connection.doReadQuery("DELETE FROM storagetarget WHERE targetjobid=@id", parameters, transaction);


                parameters = new Dictionary<string, object>();
                //get rotationtype ID
                parameters.Add("name", job.Rotation.type.ToString().ToLower());
                values = connection.doReadQuery("SELECT id FROM RotationType WHERE name=@name", parameters, transaction);
                int rotationID = (int)(values[0]["id"]);

                //create job entry
                parameters = new Dictionary<string, object>();
                parameters.Add("name", job.Name);
                parameters.Add("incremental", job.Incremental);
                parameters.Add("interval", intervalString);
                parameters.Add("minute", job.Interval.minute);
                parameters.Add("hour", job.Interval.hour);
                parameters.Add("day", job.Interval.day);
                parameters.Add("blocksize", job.BlockSize);
                parameters.Add("maxelements", job.Rotation.maxElementCount);
                parameters.Add("rotationtypeID", rotationID);
                parameters.Add("livebackup", job.LiveBackup);
                parameters.Add("updatejobID", updatedJobID);
                parameters.Add("usededupe", job.UsingDedupe);


                values = connection.doReadQuery("UPDATE jobs SET name = @name, incremental = @incremental, interval = @interval, minute = @minute, hour = @hour, day = @day, blocksize = @blocksize, maxelements = @maxelements, livebackup = @livebackup, rotationtypeid = @rotationtypeID, usededupe = @usededupe WHERE id=@updatejobID;", parameters, transaction);

                //delete existing JObVM relation
                deleteJobVMRelation(updatedJobID, connection, transaction);

                //create target dtore entry
                createTargetStorageEntry(updatedJobID, job.TargetPath, job.TargetUsername, job.TargetPassword, job.TargetType, connection, transaction);

                //create vms relation
                List<string> alreadyExistedvmIDs = createJobVMRelation(job, updatedJobID, connection, transaction);

                //create hdds relation
                createVMHDDRelation(job.JobVMs, connection, transaction, alreadyExistedvmIDs);

                //commit transaction
                transaction.Commit();
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
                SQLiteTransaction transaction = connection.beginTransaction();

                List<Dictionary<string, object>> values;

                parameters = new Dictionary<string, object>();
                //get rotationtype ID
                parameters.Add("name", job.Rotation.type.ToString().ToLower());
                values = connection.doReadQuery("SELECT id FROM RotationType WHERE name=@name", parameters, transaction);
                int rotationID = int.Parse(values[0]["id"].ToString());

                //create job entry
                parameters = new Dictionary<string, object>();
                parameters.Add("name", job.Name);
                parameters.Add("incremental", job.Incremental);
                parameters.Add("interval", intervalString);
                parameters.Add("minute", job.Interval.minute);
                parameters.Add("hour", job.Interval.hour);
                parameters.Add("day", job.Interval.day);
                parameters.Add("blocksize", job.BlockSize);
                parameters.Add("maxelements", job.Rotation.maxElementCount);
                parameters.Add("rotationtypeID", rotationID);
                parameters.Add("livebackup", job.LiveBackup);
                parameters.Add("useencryption", job.UseEncryption);
                parameters.Add("aeskey", job.AesKey);
                parameters.Add("usededupe", job.UsingDedupe);


                values = connection.doReadQuery("INSERT INTO jobs (name, incremental, interval, minute, hour, day, blocksize, maxelements, livebackup, rotationtypeid, useencryption, aeskey, usededupe) VALUES(@name, @incremental, @interval, @minute, @hour, @day, @blocksize, @maxelements, @livebackup, @rotationtypeID, @useencryption, @aeskey, @usededupe);", parameters, transaction);

                int jobID = (int)connection.getLastInsertedID();

                //create target dtore entry
                createTargetStorageEntry(jobID, job.TargetPath, job.TargetUsername, job.TargetPassword, job.TargetType, connection, transaction);

                //create vms relation
                List<string> alreadyExistedvmIDs = createJobVMRelation(job, jobID, connection, transaction);

                //create hdds relation
                createVMHDDRelation(job.JobVMs, connection, transaction, alreadyExistedvmIDs);

                //commit transaction
                transaction.Commit();
            }

        }

        //creates a target storage db entry
        private static void createTargetStorageEntry(int jobID, string path, string username, string password, string type, Common.DBConnection connection, SQLiteTransaction transaction)
        {
            //init value which are not set possibly
            if (username == null)
            {
                username = "";
            }
            if (password == null)
            {
                password = "";
            }

            Dictionary<string, object>  parameters = new Dictionary<string, object>();
            parameters.Add("targetjobid", jobID);
            parameters.Add("targettype", type);
            parameters.Add("targetpath", path);
            parameters.Add("targetuser", username);
            parameters.Add("targetpassword", password);

            connection.doReadQuery("INSERT INTO storagetarget (targetjobid, targettype, targetpath, targetuser, targetpassword) VALUES (@targetjobid, @targettype, @targetpath, @targetuser, @targetpassword);", parameters, transaction);
        }

        //sets a given job to enabled/disabled
        public static void setJobEnabled(int jobID, bool enabled)
        {
            //open DB connection
            using (Common.DBConnection connection = new Common.DBConnection())
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("enabled", enabled);
                parameters.Add("jobID", jobID);

                connection.doWriteQuery("UPDATE jobs SET enabled=@enabled WHERE id=@jobID", parameters, null);
            }
        }

        //creates a vm-hdd relation for all selected vms within a job. vm must be in DB already
        private static void createVMHDDRelation(List<JobVM> vms, Common.DBConnection connection, SQLiteTransaction transaction, List<string> alreadyExistedvmIDs)
        {
            //iterate through all vms
            foreach (JobVM vm in vms)
            {
                //did vm already exist? ignore it here
                if (alreadyExistedvmIDs.Contains(vm.vmID))
                {
                    continue;
                }

                List<int> hddIDs = new List<int>();
                //add hdd DB entries
                foreach(VMHDD currentHDD in vm.vmHDDs)
                {
                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    parameters.Add("name", currentHDD.name);
                    parameters.Add("path", currentHDD.path);
                    List<Dictionary<string, object>> result = connection.doReadQuery("INSERT INTO hdds (name, path) VALUES (@name, @path);", parameters, transaction);
                    hddIDs.Add((int)(connection.getLastInsertedID()));
                }

                //add vm hdd relations
                foreach(int hddID in hddIDs)
                {
                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    parameters.Add("vmid", vm.vmID);
                    parameters.Add("hddid", hddID);
                    List<Dictionary<string, object>> result = connection.doReadQuery("INSERT INTO vmhddrelation (vmid, hddid) VALUES (@vmid, @hddid);", parameters, transaction);
                }

            }
        }


        //deletes the JobVM relation for a given job
        private static void deleteJobVMRelation(int jobID, Common.DBConnection connection, SQLiteTransaction transaction)
        {
            //build and execute delete query
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("jobID", jobID);
            connection.doWriteQuery("DELETE FROM JobVMRelation WHERE jobid=@jobID;", parameters, transaction);
        }

        //creates a job-vms relation, return already existed vm ids
        private static List<string> createJobVMRelation(OneJob job, int jobID, Common.DBConnection connection, SQLiteTransaction transaction)
        {
            List<string> alreadyExistedvmIDs = new List<string>();

            //iterate through all vms
            foreach (JobVM vm in job.JobVMs)
            {
                //check whether vm already exists
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("id", vm.vmID);
                List<Dictionary<string, object>> result = connection.doReadQuery("SELECT COUNT(*) AS count From vms WHERE id=@id", parameters, transaction);

                object a = result[0]["count"];
                //does vm already exist in DB?
                if (Convert.ToInt32(result[0]["count"]) == 0)
                {
                    //vm does not exist
                    parameters = new Dictionary<string, object>();
                    parameters.Add("id", vm.vmID);
                    parameters.Add("name", vm.vmName);
                    connection.doReadQuery("INSERT INTO vms(id, name) VALUES (@id, @name);", parameters, transaction);
                }
                else
                {
                    alreadyExistedvmIDs.Add(vm.vmID);
                }

                //vm exists now, now create relation
                parameters = new Dictionary<string, object>();
                parameters.Add("jobid", jobID);
                parameters.Add("vmid", vm.vmID);
                connection.doReadQuery("INSERT INTO JobVMRelation(jobid, vmid) VALUES (@jobid, @vmid)", parameters, transaction);
            }

            return alreadyExistedvmIDs;

        }

        // Check if job is running.
        public static bool isJobRunning(int jobId)
        {
            using (Common.DBConnection connection = new Common.DBConnection())
            {
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters.Add("jobId", jobId.ToString());
                List<Dictionary<string, object>> result = connection.doReadQuery("SELECT isRunning FROM Jobs WHERE id=@jobId", parameters, null);
                if (result != null && result.Count > 0 && result[0]["id"].ToString() != "") return (bool)(result[0]["id"]);
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
    public class OneJob
    {
        private int dbId;
        private bool enabled;
        private bool useEncryption;
        private byte[] aesKey;
        private bool usingDedupe;
        private string name;
        private bool incremental;
        private Interval interval;
        private List<JobVM> jobVMs;
        private int blockSize;
        private Rotation rotation;
        private bool liveBackup;
        private bool liveBackupActive;
        private bool isRunning;
        private string lastRun;
        private string lastStop;
        private bool successful;
        private UInt64 lastBytesProcessed;
        private UInt64 lastBytesTransfered;
        private Rate[] rates;
        private string targetType;
        private string targetPath;
        private string targetUsername;
        private string targetPassword;

        public string Name { get => name; set => name = value; }
        public bool Enabled { get => enabled; set => enabled = value; }
        public Interval Interval { get => interval; set => interval = value; }
        public bool UseEncryption { get => useEncryption; set => useEncryption = value; }
        public byte[] AesKey { get => aesKey; set => aesKey = value; }
        public bool UsingDedupe { get => usingDedupe; set => usingDedupe = value; }
        public List<JobVM> JobVMs { get => jobVMs; set => jobVMs = value; }
        public int BlockSize { get => blockSize; set => blockSize = value; }
        public Rotation Rotation { get => rotation; set => rotation = value; }
        public bool IsRunning { get => isRunning; set => isRunning = value; }
        public bool Incremental { get => incremental; set => incremental = value; }
        public int DbId { get => dbId; set => dbId = value; }
        public bool LiveBackupActive { get => liveBackupActive; set => liveBackupActive = value; }
        public bool LiveBackup { get => liveBackup; set => liveBackup = value; }

        public string TargetType { get => targetType; set => targetType = value; }
        public string TargetPath { get => targetPath; set => targetPath = value; }
        public string TargetUsername { get => targetUsername; set => targetUsername = value; }
        public string TargetPassword { get => targetPassword; set => targetPassword = value; }

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

        public Rate[] Rates { get => rates; set => rates = value; }
        public string LastRun { get => lastRun; set => lastRun = value; }

        public string LastStop { get => lastStop; set => lastStop = value; }
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

        public UInt64 LastBytesTransfered { get => lastBytesTransfered; set => lastBytesTransfered = value; }
        public UInt64 LastBytesProcessed { get => lastBytesProcessed; set => lastBytesProcessed = value; }

        public struct Rate
        {
            public UInt64 process;
            public UInt64 transfer;
        }
    }

    

}