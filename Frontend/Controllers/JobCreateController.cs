using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class JobCreateController : ApiController
    {

        
        // POST api/<controller>
        public void Post([FromBody] NewFrontendJob value)
        {
            ConfigHandler.OneJob newJob = new ConfigHandler.OneJob();

            //convert Frontend job to OneJob object
            newJob.Name = value.name;
            newJob.BasePath = value.target;
            newJob.LiveBackup = value.livebackup;
            newJob.UseEncryption = value.useencryption;

            if (newJob.UseEncryption) {
                newJob.AesKey = Common.SHA256Provider.computeHash(System.Text.Encoding.UTF8.GetBytes(value.encpassword));
            }
            else
            {
                newJob.AesKey = new byte[1];
            }

            Common.Interval interval = new Common.Interval();
            
            switch (value.interval)
            {
                case "hourly":
                    interval.intervalBase = Common.IntervalBase.hourly;
                    break;
                case "daily":
                    interval.intervalBase = Common.IntervalBase.daily;
                    break;
                case "weekly":
                    interval.intervalBase = Common.IntervalBase.weekly;
                    break;
            }
            interval.day = value.day;
            interval.hour =  int.Parse(value.hour);
            interval.minute = int.Parse(value.minute);
            newJob.Interval = interval;

            Common.Rotation rotation = new Common.Rotation();
            switch (value.rotationtype)
            {
                case "merge":
                    rotation.type = Common.RotationType.merge;
                    break;
                case "blockrotation":
                    rotation.type = Common.RotationType.blockRotation;
                    break;
            }
            rotation.maxElementCount = int.Parse(value.maxelements);
            newJob.Rotation = rotation;
            newJob.BlockSize = int.Parse(value.blocksize);

            //load vms to get vm hdds
            List<Common.WMIHelper.OneVM> currentVMs = Common.WMIHelper.listVMs();

            List<Common.JobVM> vms = new List<Common.JobVM>();
            foreach (string vmid in value.vms)
            {
                Common.JobVM vm = new Common.JobVM();
                vm.vmID = vmid;

                //search for vm
                foreach(Common.WMIHelper.OneVM oneVM in currentVMs)
                {
                    if (oneVM.id == vmid)
                    {
                        List<Common.WMIHelper.OneVMHDD> hdds = oneVM.hdds;
                        List<Common.VMHDD> newHDDS = new List<Common.VMHDD>();

                        foreach(Common.WMIHelper.OneVMHDD tempHDD in hdds)
                        {
                            Common.VMHDD newHDD = new Common.VMHDD();
                            newHDD.name = tempHDD.name;
                            newHDD.path = tempHDD.path;
                            newHDDS.Add(newHDD);
                        }

                        vm.vmHDDs = newHDDS;

                        break;
                    }
                }

                vms.Add(vm);
            }

            newJob.JobVMs = vms;
                       

            //add job to db
            ConfigHandler.JobConfigHandler.addJob(newJob);

            //refresh jobs
            App_Start.GUIJobHandler.initJob();
        }

       
        public class NewFrontendJob
        {
            public string name { get; set; }
            public bool useencryption { get; set; }
            public string encpassword { get; set; }
            public bool livebackup { get; set; }
            public string target { get; set; }
            public string interval { get; set; }
            public string minute { get; set; }
            public string hour { get; set; }
            public string day { get; set; }
            public string maxelements { get; set; }
            public string blocksize { get; set; }
            public string rotationtype { get; set; }
            public string[] vms { get; set; }
        }
    }
}