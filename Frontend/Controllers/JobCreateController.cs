﻿using Common;
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
        public HttpResponseMessage Post([FromBody] NewFrontendJob value) //created or updates a job
        {
            HttpResponseMessage response = new HttpResponseMessage();

            //when lb is activated, vms may just be local
            if (value.livebackup)
            {
                foreach (NewFrontendVM vm in value.vms)
                {
                    if(vm.hostID != "1")
                    {
                        //return error
                        Common.DBQueries.addLog("lb activated on remote vm", Environment.StackTrace, null);

                        response.StatusCode = HttpStatusCode.BadRequest;
                        return response;
                    }
                }
            }


            ConfigHandler.OneJob newJob = new ConfigHandler.OneJob();

            //convert Frontend job to OneJob object
            newJob.Name = value.name;
            newJob.Incremental = value.incremental;
            newJob.TargetPath = value.targetPath;
            newJob.TargetType = value.targetType;
            newJob.TargetUsername = value.targetUsername;
            newJob.TargetPassword = value.targetPassword;
            newJob.LiveBackup = value.livebackup;
            newJob.LiveBackupSize = int.Parse(value.livebackupsize);
            newJob.UseEncryption = value.useencryption;
            newJob.UsingDedupe = value.usingdedupe;
            newJob.MailNotifications = value.mailnotifications;
            newJob.HostID = value.hostID;

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
                case "never":
                    interval.intervalBase = Common.IntervalBase.never;
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

            //load host credentials and build wmi settings
            WMIConnectionOptions wmiOptions = DBQueries.getHostByID(int.Parse(newJob.HostID), true);
            

            //load vms to get vm hdds
            List<Common.WMIHelper.OneVM> currentVMs = Common.WMIHelper.listVMs(wmiOptions); //------------- change ---------------

            List<Common.JobVM> vms = new List<Common.JobVM>();
            foreach (NewFrontendVM frontendVM in value.vms)
            {
                Common.JobVM vm = new Common.JobVM();
                vm.vmID = frontendVM.id;
                vm.vmName = frontendVM.name;
                vm.hostID = frontendVM.hostID;

                //search for vm
                foreach(Common.WMIHelper.OneVM oneVM in currentVMs)
                {
                    if (oneVM.id == frontendVM.id)
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

            //update or new job
            int updatedJobID;
            int jobID;
            if (value.updatedJob != null && int.TryParse(value.updatedJob, out updatedJobID))
            {
                //update job within DB
                ConfigHandler.JobConfigHandler.updateJob(newJob, updatedJobID);
                jobID = updatedJobID;
            }
            else
            {
                //add new job to DB
                jobID = ConfigHandler.JobConfigHandler.addJob(newJob);
            }

            //refresh jobs
            App_Start.GUIJobHandler.initJobs();

            //try to create dest folder
            try
            {
                //translate data when using nxmStorage
                if (value.targetType == "nxmstorage")
                {
                    Common.NxmStorageData nxmData =  Common.WebClientWrapper.translateNxmStorageData(value.targetUsername, value.targetPassword);
                    if (nxmData != null)
                    {
                        value.targetPath = nxmData.share + @"\" + nxmData.share_user + @"\nxmStorage";
                    }
                }

                //try to create dest folder
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(value.targetPath, value.name));
            }
            catch (Exception ex)
            {
                Common.DBQueries.addLog("error creating target path", Environment.StackTrace, ex);

                //remove job again
                ConfigHandler.JobConfigHandler.deleteJob(jobID);
                
                response.StatusCode = HttpStatusCode.BadRequest;
                return response;
            }

            response.StatusCode = HttpStatusCode.OK;
            return response;
        }

       
        public class NewFrontendJob
        {
            public string updatedJob { get; set; }
            public string name { get; set; }
            public bool mailnotifications { get; set; }
            public bool incremental { get; set; }
            public bool useencryption { get; set; }
            public bool usingdedupe { get; set; }
            public string encpassword { get; set; }
            public bool livebackup { get; set; }
            public string livebackupsize { get; set; }
            public string targetType { get; set; }
            public string targetUsername { get; set; }
            public string targetPassword { get; set; }
            public string targetPath { get; set; }
            public string interval { get; set; }
            public string minute { get; set; }
            public string hour { get; set; }
            public string day { get; set; }
            public string maxelements { get; set; }
            public string blocksize { get; set; }
            public string rotationtype { get; set; }
            public NewFrontendVM[] vms { get; set; }
            public string hostID { get; set; }
        }

        public class NewFrontendVM
        {
            public string name { get; set; }
            public string id { get; set; }
            public string hostID { get; set; }
        }
    }
}