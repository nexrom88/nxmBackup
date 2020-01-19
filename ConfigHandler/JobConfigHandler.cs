using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
                List<Dictionary<string,string>> jobs = connection.doQuery("SELECT Jobs.id, Jobs.name, Jobs.isRunning, Jobs.basepath, Jobs.maxelements, Jobs.blocksize, Jobs.day, Jobs.hour, Jobs.minute, Jobs.interval, Compression.name AS compressionname, RotationType.name AS rotationname FROM Jobs INNER JOIN Compression ON Jobs.compressionID=Compression.id INNER JOIN RotationType ON Jobs.rotationtypeID=RotationType.id", null, null);
            
                //iterate through all jobs
                foreach(Dictionary<string,string> job in jobs)
                {
                    //build structure
                    OneJob newJob = new OneJob();
                    newJob.DbId = int.Parse(job["id"]);
                    newJob.BasePath = job["basepath"]; 
                    newJob.Name = job["name"];
                    newJob.BlockSize = uint.Parse(job["blocksize"]);
                    newJob.IsRunning = bool.Parse(job["isRunning"]);

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

                    rota.maxElementCount = uint.Parse(job["maxelements"]);
                    newJob.Rotation = rota;

                    //build compression level
                    switch (job["compressionname"])
                    {
                        case "zip":
                            newJob.Compression = Compression.zip;
                            break;
                        case "lz4":
                            newJob.Compression = Compression.lz4;
                            break;
                    }

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
                    interval.minute = job["minute"];
                    interval.hour = job["hour"];
                    newJob.Interval = interval;

                    //query VMs
                    Dictionary<string, string> paramaters = new Dictionary<string, string>();
                    paramaters.Add("jobid", job["id"]);
                    List<Dictionary<string, string>> vms = connection.doQuery("SELECT VMs.id, VMs.name FROM VMs INNER JOIN JobVMRelation ON JobVMRelation.jobid=@jobid AND JobVMRelation.vmid=VMs.id", paramaters, null);
                    newJob.JobVMs = new List<JobVM>();

                    //iterate through all vms
                    foreach(Dictionary<string,string> vm in vms)
                    {
                        JobVM newVM = new JobVM();
                        newVM.vmID = vm["id"];
                        newVM.vmName = vm["name"];
                        newJob.JobVMs.Add(newVM);
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

                Dictionary<string, string> parameters = new Dictionary<string, string>();

                //start DB transaction
                SqlTransaction transaction = connection.beginTransaction();

                List<Dictionary<string, string>> values;

                //get compression id
                parameters.Add("name", job.Compression.ToString().ToLower());
                values = connection.doQuery("SELECT id FROM compression WHERE name=@name", parameters, transaction);
                string compressionID = values[0]["id"];

                parameters = new Dictionary<string, string>();
                //get rotationtype ID
                parameters.Add("name", job.Rotation.type.ToString().ToLower());
                values = connection.doQuery("SELECT id FROM RotationType WHERE name=@name", parameters, transaction);
                string rotationID = values[0]["id"];


                //create job entry
                parameters = new Dictionary<string, string>();
                parameters.Add("name", job.Name);
                parameters.Add("interval", intervalString);
                parameters.Add("minute", job.Interval.minute);
                parameters.Add("hour", job.Interval.hour);
                parameters.Add("day", job.Interval.day);
                parameters.Add("basepath", job.BasePath);
                parameters.Add("compressionID", compressionID);
                parameters.Add("blocksize", job.BlockSize.ToString());
                parameters.Add("maxelements", job.Rotation.maxElementCount.ToString());
                parameters.Add("rotationtypeID", rotationID);

                values = connection.doQuery("INSERT INTO Jobs (name, interval, minute, hour, day, basepath, compressionID, blocksize, maxelements, rotationtypeID) VALUES(@name, @interval, @minute, @hour, @day, @basepath, @compressionID, @blocksize, @maxelements, @rotationtypeID); SELECT SCOPE_IDENTITY() AS id;", parameters, transaction);

                string jobID = values[0]["id"];

                createJobVMRelation(job, jobID, connection, transaction);

                //commit transaction
                transaction.Commit();
            }

        }

        //creates a job-vms relation
        private static void createJobVMRelation(OneJob job, string jobID, Common.DBConnection connection, SqlTransaction transaction)
        {
            //iterate through all vms
            foreach (JobVM vm in job.JobVMs)
            {
                //check whether vm already exists
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("id", vm.vmID);
                List<Dictionary<string, string>> result = connection.doQuery("SELECT COUNT(*) AS count From VMs WHERE id=@id", parameters, transaction);

                //does vm already exist in DB?
                if (int.Parse(result[0]["count"]) == 0){
                    //vm does not exist
                    parameters = new Dictionary<string, string>();
                    parameters.Add("id", vm.vmID);
                    parameters.Add("name", vm.vmName);
                    connection.doQuery("INSERT INTO VMs(id, name) VALUES (@id, @name); SELECT SCOPE_IDENTITY() AS id;", parameters, transaction);
                }

                //vm exists now, now create relation
                parameters = new Dictionary<string, string>();
                parameters.Add("jobid", jobID);
                parameters.Add("vmid", vm.vmID);
                connection.doQuery("INSERT INTO JobVMRelation(jobid, vmid) VALUES (@jobid, @vmid)", parameters, transaction);
            }

        }

        // Check if job is running.
        public static bool isJobRunning(int jobId)
        {
            using (Common.DBConnection connection = new Common.DBConnection())
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("jobId", jobId.ToString());
                List<Dictionary<string, string>> result = connection.doQuery("SELECT isRunning FROM Jobs WHERE id=@jobId", parameters, null);
                if (result != null && result.Count > 0 && result[0]["id"] != "") return bool.Parse(result[0]["id"]);
                else return false;
            }
        }

       
    }


    // Structures:

    //represents one job within jobs.xml
    public struct OneJob: System.ComponentModel.INotifyPropertyChanged
    {
        private int dbId;
        private string name;
        private Interval interval;
        private List<JobVM> jobVMs;
        private string basePath;
        private Compression compression;
        private uint blockSize;
        private Rotation rotation;
        private bool isRunning;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get => name; set => name = value; }
        public Interval Interval { get => interval; set => interval = value; }
        public List<JobVM> JobVMs { get => jobVMs; set => jobVMs = value; }
        public string BasePath { get => basePath; set => basePath = value; }
        public Compression Compression { get => compression; set => compression = value; }
        public uint BlockSize { get => blockSize; set => blockSize = value; }
        public Rotation Rotation { get => rotation; set => rotation = value; }
        public bool IsRunning { get => isRunning; set => isRunning = value; }
        public int DbId { get => dbId; set => dbId = value; }
        public string IntervalBaseForGUI {
            get 
            {
                switch (interval.intervalBase) 
                {
                    case ConfigHandler.IntervalBase.daily:
                        return "täglich";
                    case ConfigHandler.IntervalBase.hourly:
                        return "stündlich";
                    case ConfigHandler.IntervalBase.weekly:
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
                        return "default";                }
            }
        }
    }

    //defines compression type
    public enum Compression
    {
        zip, lz4
    }

    //defines rotation type
    public struct Rotation
    {
        public RotationType type;
        public uint maxElementCount;
    }

    //defines rotation type
    public enum RotationType
    {
        merge, blockRotation
    }

    //defines when to start a backup
    public enum IntervalBase
    {
        hourly, daily, weekly
    }

    //defines the interval details
    public struct Interval
    {
        public IntervalBase intervalBase;
        public string minute;
        public string hour;
        public string day;
    }

    //defines one VM within a job
    public struct JobVM
    {
        public string vmID;
        public string vmName;
    }

}
