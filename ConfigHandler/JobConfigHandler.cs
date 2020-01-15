using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;

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
                List<Dictionary<string,string>> jobs = connection.doQuery("SELECT Jobs.id, Jobs.name, Jobs.basepath, Jobs.maxelements, Jobs.blocksize, Jobs.day, Jobs.hour, Jobs.minute, Jobs.interval, Compression.name AS compressionname, RotationType.name AS rotationname FROM Jobs INNER JOIN Compression ON Jobs.compressionID=Compression.id INNER JOIN RotationType ON Jobs.rotationtypeID=RotationType.id", null, null);
            
                //iterate through all jobs
                foreach(Dictionary<string,string> job in jobs)
                {
                    //build structure
                    OneJob newJob = new OneJob();
                    newJob.basePath = job["basepath"];
                    newJob.name = job["name"];
                    newJob.blockSize = uint.Parse(job["blocksize"]);

                    //build rotation structure
                    switch (job["rotationname"])
                    {
                        case "merge":
                            newJob.rotation.type = RotationType.merge;
                            break;
                        case "blockrotation":
                            newJob.rotation.type = RotationType.blockRotation;
                            break;
                    }
                    newJob.rotation.maxElementCount = uint.Parse(job["maxelements"]);

                    //build compression level
                    switch (job["compressionname"])
                    {
                        case "zip":
                            newJob.compression = Compression.zip;
                            break;
                        case "lz4":
                            newJob.compression = Compression.lz4;
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
                    newJob.interval = interval;

                    //query VMs
                    Dictionary<string, string> paramaters = new Dictionary<string, string>();
                    paramaters.Add("jobid", job["id"]);
                    List<Dictionary<string, string>> vms = connection.doQuery("SELECT VMs.id, VMs.name FROM VMs INNER JOIN JobVMRelation ON JobVMRelation.jobid=@jobid AND JobVMRelation.vmid=VMs.id", paramaters, null);
                    newJob.jobVMs = new List<JobVM>();

                    //iterate through all vms
                    foreach(Dictionary<string,string> vm in vms)
                    {
                        JobVM newVM = new JobVM();
                        newVM.vmID = vm["id"];
                        newVM.vmName = vm["name"];
                        newJob.jobVMs.Add(newVM);
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
                string intervalString = job.interval.intervalBase.ToString();

                Dictionary<string, string> parameters = new Dictionary<string, string>();

                //start DB transaction
                SqlTransaction transaction = connection.beginTransaction();

                List<Dictionary<string, string>> values;

                //get compression id
                parameters.Add("name", job.compression.ToString().ToLower());
                values = connection.doQuery("SELECT id FROM compression WHERE name=@name", parameters, transaction);
                string compressionID = values[0]["id"];

                parameters = new Dictionary<string, string>();
                //get rotationtype ID
                parameters.Add("name", job.rotation.type.ToString().ToLower());
                values = connection.doQuery("SELECT id FROM RotationType WHERE name=@name", parameters, transaction);
                string rotationID = values[0]["id"];


                //create job entry
                parameters = new Dictionary<string, string>();
                parameters.Add("name", job.name);
                parameters.Add("interval", intervalString);
                parameters.Add("minute", job.interval.minute);
                parameters.Add("hour", job.interval.hour);
                parameters.Add("day", job.interval.day);
                parameters.Add("basepath", job.basePath);
                parameters.Add("compressionID", compressionID);
                parameters.Add("blocksize", job.blockSize.ToString());
                parameters.Add("maxelements", job.rotation.maxElementCount.ToString());
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
            foreach (JobVM vm in job.jobVMs)
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

        
    }

    //represents one job within jobs.xml
    public struct OneJob
    {
        public string name;
        public Interval interval;
        public List<JobVM> jobVMs;
        public string basePath;
        public Compression compression;
        public uint blockSize;
        public Rotation rotation;
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
