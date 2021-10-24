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
using nxmBackup.HVBackupCore;

namespace ConfigHandler
{
    public class JobConfigHandler
    {
        private static Object lockObj = new object();
        private static List<OneJob> jobs;

        public static List<OneJob> Jobs { get => jobs; }

        //reads all jobs from the DB
        public static void readJobsFromDB()
        {
            jobs = new List<OneJob>();

            //open DB connection
            using (Common.DBConnection connection = new Common.DBConnection())
            {
                List<Dictionary<string, object>> jobsDB = connection.doReadQuery("SELECT jobs.id, jobs.name, jobs.incremental, jobs.isRunning, jobs.basepath, jobs.maxelements, jobs.blocksize, jobs.day, jobs.hour, jobs.minute, jobs.interval, jobs.livebackup, jobs.useencryption, jobs.aeskey, rotationtype.name AS rotationname FROM jobs INNER JOIN rotationtype ON jobs.rotationtypeid=rotationtype.id WHERE jobs.deleted=FALSE;", null, null);

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
                    newJob.DbId = (int)jobDB["id"];
                    newJob.BasePath = jobDB["basepath"].ToString();
                    newJob.Name = jobDB["name"].ToString();
                    newJob.BlockSize = (int)jobDB["blocksize"];
                    newJob.IsRunning = (bool)jobDB["isrunning"];
                    newJob.LiveBackup = (bool)jobDB["livebackup"];
                    newJob.Incremental = (bool)jobDB["incremental"];
                    newJob.UseEncryption = (bool)jobDB["useencryption"];

                    if (newJob.UseEncryption)
                    {
                        newJob.AesKey = (byte[])jobDB["aeskey"];
                    }

                    // build nextRun string
                    newJob.NextRun = $"{((int)jobDB["hour"]).ToString("00")}:{((int)jobDB["minute"]).ToString("00")}";
                    if (jobDB["day"].ToString() != "") newJob.NextRun += $" ({jobDB["day"]})";

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

                    rota.maxElementCount = (int)jobDB["maxelements"];
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
                    interval.minute = (int)jobDB["minute"];
                    interval.hour = (int)jobDB["hour"];
                    newJob.Interval = interval;

                    //query VMs
                    Dictionary<string, object> paramaters = new Dictionary<string, object>();
                    paramaters.Add("jobid", (int)jobDB["id"]);
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
                        foreach(Dictionary<string,object> oneHDD in hdds)
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
                    paramaters.Add("jobid", (int)jobDB["id"]);
                    List<Dictionary<string, object>> jobExecutions = connection.doReadQuery("SELECT * FROM jobexecutions WHERE jobexecutions.jobid=@jobid and jobexecutions.id = (SELECT MAX(id) FROM jobexecutions WHERE jobexecutions.jobid=@jobid)", paramaters, null);

                    if (jobExecutions.Count > 1) MessageBox.Show("db error: jobExecutions hat mehr als 1 result");
                    else
                    {
                        foreach (Dictionary<string, object> jobExecution in jobExecutions)
                        {
                            newJob.LastRun = jobExecution["startstamp"].ToString();
                            newJob.Successful = jobExecution["successful"].ToString();
                        }
                            
                    }

                    jobs.Add(newJob);

                }
            }
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
                NpgsqlTransaction transaction = connection.beginTransaction();

                List<Dictionary<string, object>> values;

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
                parameters.Add("basepath", job.BasePath);
                parameters.Add("blocksize", job.BlockSize);
                parameters.Add("maxelements", job.Rotation.maxElementCount);
                parameters.Add("rotationtypeID", rotationID);
                parameters.Add("livebackup", job.LiveBackup);
                parameters.Add("updatejobID", updatedJobID);


                values = connection.doReadQuery("UPDATE jobs SET name = @name, incremental = @incremental, interval = @interval, minute = @minute, hour = @hour, day = @day, basepath = @basepath, blocksize = @blocksize, maxelements = @maxelements, livebackup = @livebackup, rotationtypeid = @rotationtypeID WHERE id=@updatejobID;", parameters, transaction);

                //delete existing JObVM relation
                deleteJobVMRelation(updatedJobID, connection, transaction);

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
                NpgsqlTransaction transaction = connection.beginTransaction();

                List<Dictionary<string, object>> values;

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
                parameters.Add("basepath", job.BasePath);
                parameters.Add("blocksize", job.BlockSize);
                parameters.Add("maxelements", job.Rotation.maxElementCount);
                parameters.Add("rotationtypeID", rotationID);
                parameters.Add("livebackup", job.LiveBackup);
                parameters.Add("useencryption", job.UseEncryption);
                parameters.Add("aeskey", job.AesKey);


                values = connection.doReadQuery("INSERT INTO jobs (name, incremental, interval, minute, hour, day, basepath, blocksize, maxelements, livebackup, rotationtypeid, useencryption, aeskey) VALUES(@name, @incremental, @interval, @minute, @hour, @day, @basepath, @blocksize, @maxelements, @livebackup, @rotationtypeID, @useencryption, @aeskey) RETURNING id;", parameters, transaction);

                int jobID = (int)(values[0]["id"]);

                //create vms relation
                List<string> alreadyExistedvmIDs = createJobVMRelation(job, jobID, connection, transaction);

                //create hdds relation
                createVMHDDRelation(job.JobVMs, connection, transaction, alreadyExistedvmIDs);

                //commit transaction
                transaction.Commit();
            }

        }

        //creates a vm-hdd relation for all selected vms within a job. vm must be in DB already
        private static void createVMHDDRelation(List<JobVM> vms, Common.DBConnection connection, NpgsqlTransaction transaction, List<string> alreadyExistedvmIDs)
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
                    List<Dictionary<string, object>> result = connection.doReadQuery("INSERT INTO hdds (name, path) VALUES (@name, @path) RETURNING id;", parameters, transaction);
                    hddIDs.Add((int)(result[0]["id"]));
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
        private static void deleteJobVMRelation(int jobID, Common.DBConnection connection, NpgsqlTransaction transaction)
        {
            //build and execute delete query
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("jobID", jobID);
            connection.doWriteQuery("DELETE FROM JobVMRelation WHERE jobid=@jobID;", parameters, transaction);
        }

        //creates a job-vms relation, return already existed vm ids
        private static List<string> createJobVMRelation(OneJob job, int jobID, Common.DBConnection connection, NpgsqlTransaction transaction)
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
    public class OneJob : System.ComponentModel.INotifyPropertyChanged
    {
        private int dbId;
        private bool useEncryption;
        private byte[] aesKey;
        private string name;
        private bool incremental;
        private Interval interval;
        private List<JobVM> jobVMs;
        private string basePath;
        private int blockSize;
        private Rotation rotation;
        private bool liveBackup;
        private LiveBackupWorker lbWorker;
        private bool isRunning;
        private string nextRun;
        private string lastRun;
        private bool successful;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get => name; set => name = value; }
        public Interval Interval { get => interval; set => interval = value; }
        public bool UseEncryption { get => useEncryption; set => useEncryption = value; }
        public byte[] AesKey { get => aesKey; set => aesKey = value; }
        public List<JobVM> JobVMs { get => jobVMs; set => jobVMs = value; }
        public string BasePath { get => basePath; set => basePath = value; }
        public int BlockSize { get => blockSize; set => blockSize = value; }
        public Rotation Rotation { get => rotation; set => rotation = value; }
        public bool IsRunning { get => isRunning; set => isRunning = value; }
        public bool Incremental { get => incremental; set => incremental = value; }
        public int DbId { get => dbId; set => dbId = value; }
        public bool LiveBackup { get => liveBackup; set => liveBackup = value; }
        public LiveBackupWorker LiveBackupWorker { get => lbWorker; set => lbWorker = value; }
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