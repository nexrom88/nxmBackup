using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace ConfigHandler
{
    public class JobConfigHandler
    {
        private static Object lockObj = new object();

        //reads and returns all jobs
        public static List<OneJob> readJobs() {
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

                    //build compression level
                    switch (jobsElement.ChildNodes.Item(i).Attributes.GetNamedItem("name").Value)
                    {
                        case "nocompression":
                            job.compression = System.IO.Compression.CompressionLevel.NoCompression;
                            break;
                        case "fastest":
                            job.compression = System.IO.Compression.CompressionLevel.Fastest;
                            break;
                        case "optimal":
                            job.compression = System.IO.Compression.CompressionLevel.Optimal;
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
        public static void addJob (OneJob job)
        {
            lock (lockObj)
            {
                //create the xml if it doesn't exist
                createXML();

                //file exists, open it
                FileStream baseStream = new FileStream("jobs.xml", FileMode.Open, FileAccess.ReadWrite);
                XmlDocument xml = new XmlDocument();
                xml.Load(baseStream);
                baseStream.Close();

                //add a job entry
                string intervalString = job.interval.intervalBase.ToString();                
                XmlElement rootElement = (XmlElement)xml.SelectSingleNode("VMJobs");
                XmlElement backupsElement = (XmlElement)rootElement.SelectSingleNode("Jobs");
                XmlElement newElement = xml.CreateElement(String.Empty, "Job", String.Empty);
                newElement.SetAttribute("name", job.name);
                newElement.SetAttribute("interval", intervalString);
                newElement.SetAttribute("minute", job.interval.minute);
                newElement.SetAttribute("hour", job.interval.hour);
                newElement.SetAttribute("day", job.interval.day);
                newElement.SetAttribute("basePath", job.basePath);
                newElement.SetAttribute("compression", job.compression.ToString().ToLower());
                newElement.SetAttribute("blocksize", job.blockSize.ToString());
                newElement.SetAttribute("maxElements", job.rotation.maxElementCount.ToString());
                newElement.SetAttribute("rotationType", job.rotation.type.ToString());
                XmlElement newJob = (XmlElement)backupsElement.AppendChild(newElement);

                //now build the job VMs
                foreach(JobVM vm in job.jobVMs)
                {
                    XmlElement newVM = xml.CreateElement(String.Empty, "VM", String.Empty);
                    newVM.SetAttribute("vmID", vm.vmID);
                    newVM.SetAttribute("vmName", vm.vmName);
                    newJob.AppendChild(newVM);
                }

                //save the xml file
                baseStream = new FileStream("jobs.xml", FileMode.Create, FileAccess.ReadWrite);
                xml.Save(baseStream);
                baseStream.Close();

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
        public System.IO.Compression.CompressionLevel compression;
        public uint blockSize;
        public Rotation rotation;
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
