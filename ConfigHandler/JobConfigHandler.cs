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
            lock (lockObj)
            {
                //create the xml if it doesn't exist
                createXML();

                List<OneJob> jobs = new List<OneJob>();

                //open xml
                FileStream baseStream = new FileStream("jobs.xml", FileMode.Open, FileAccess.ReadWrite);
                XmlDocument xml = new XmlDocument();
                xml.Load(baseStream);
                baseStream.Close();

                //open VMBackup
                XmlElement rootElement = (XmlElement)xml.SelectSingleNode("VMJobs");
                XmlElement jobsElement = (XmlElement)rootElement.SelectSingleNode("Jobs");

                //iterate through all Jobs
                for (int i = 0; i < jobsElement.ChildNodes.Count; i++)
                {
                    //build structure
                    OneJob job = new OneJob();
                    job.basePath = jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("basePath").Value;
                    job.name = jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("name").Value;
                    job.blockSize = uint.Parse(jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("blocksize").Value);

                    //build rotation structure
                    switch (jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("rotationtype").Value)
                    {
                        case "merge":
                            job.rotation.type = RotationType.merge;
                            break;
                        case "blockrotation":
                            job.rotation.type = RotationType.blockRotation;
                            break;
                    }
                    job.rotation.maxElementCount = uint.Parse(jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("maxelements").Value);

                    //build compression level
                    switch (jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("compression").Value)
                    {
                        case "zip":
                            job.compression = Compression.zip;
                            break;
                        case "lz4":
                            job.compression = Compression.lz4;
                            break;
                    }

                    //build interval structure
                    Interval interval = new Interval();
                    switch (jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("interval").Value)
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
                    interval.day = jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("day").Value;
                    interval.minute = jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("minute").Value;
                    interval.hour = jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("hour").Value;
                    job.interval = interval;

                    //build vms structure
                    List<JobVM> vms = new List<JobVM>();
                    XmlElement currentJobElement = (XmlElement)jobsElement.ChildNodes.Item(i);
                    for (int j = 0; j < currentJobElement.ChildNodes.Count; j++)
                    {
                        JobVM vm = new JobVM();
                        //just use node if it is a "VM" type
                        if (currentJobElement.ChildNodes.Item(j).Name == "VM")
                        {
                            vm.vmID = currentJobElement.ChildNodes.Item(j).Attributes.GetNamedItem("vmID").Value;
                            vm.vmName = currentJobElement.ChildNodes.Item(j).Attributes.GetNamedItem("vmName").Value;
                        }
                        vms.Add(vm);
                    }
                    job.jobVMs = vms;
                    jobs.Add(job);
                }
                return jobs;

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

        //creates the jobs.xml file if it doesn't exist
        private static void createXML()
        {
            //check whether config file already exists
            if (!File.Exists("jobs.xml"))
            {
                XmlDocument doc = new XmlDocument();

                //(1) the xml declaration is recommended, but not mandatory
                XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                XmlElement root = doc.DocumentElement;
                doc.InsertBefore(xmlDeclaration, root);

                //generate root node
                XmlElement bodyElement = doc.CreateElement(string.Empty, "VMJobs", string.Empty);
                bodyElement.SetAttribute("version", "1.0");
                doc.AppendChild(bodyElement);

                //generate attributes node
                XmlElement attrElement = doc.CreateElement(string.Empty, "Attributes", string.Empty);
                bodyElement.AppendChild(attrElement);

                //generate BackupChain node
                XmlElement chainElement = doc.CreateElement(string.Empty, "Jobs", string.Empty);
                bodyElement.AppendChild(chainElement);


                doc.Save(Path.Combine("jobs.xml"));
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
