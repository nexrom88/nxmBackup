﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class RestoreController : ApiController
    {
        // POST api/<controller>
        public HttpResponseMessage Post([FromBody] RestoreStartDetails restoreStartDetails)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            ConfigHandler.OneJob jobObject = null;
            //look for the matching job object
            foreach (ConfigHandler.OneJob job in ConfigHandler.JobConfigHandler.Jobs)
            {
                if (job.DbId == restoreStartDetails.jobID)
                {
                    jobObject = job;
                }
            }

            //look for vm object
            Common.JobVM vmObject = null;
            foreach (Common.JobVM vm in jobObject.JobVMs)
            {
                if (vm.vmID == restoreStartDetails.vmID)
                {
                    vmObject = vm;
                }
            }

            //build source patch
            string sourcePath = restoreStartDetails.basePath + "\\" + jobObject.Name + "\\" + restoreStartDetails.vmID;


            //restore already running? -> quit
            if (App_Start.RunningRestoreJobs.CurrentLiveRestore != null)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                return response;
            }

            switch (restoreStartDetails.type)
            {
                case "full":
                case "fullImport":
                    int jobExecutionId = Common.DBQueries.addJobExecution(restoreStartDetails.jobID, "restore");
                    HVRestoreCore.FullRestoreHandler fullRestoreHandler = new HVRestoreCore.FullRestoreHandler(new Common.EventHandler(vmObject, jobExecutionId), jobObject.UseEncryption, jobObject.AesKey);
                    //fullRestoreHandler.performFullRestoreProcess(sourcePath, "f:\\target", ((ComboBoxItem)cbVMs.SelectedItem).Content.ToString() + "_restored", restorePoint.InstanceId, importToHyperV);

                    break;
                case "lr":
                    HVRestoreCore.LiveRestore lrHandler = new HVRestoreCore.LiveRestore(jobObject.UseEncryption, jobObject.AesKey);

                    //set global object
                    App_Start.RunningRestoreJobs.CurrentLiveRestore = lrHandler;


                    System.Threading.Thread lrThread = new System.Threading.Thread(() => lrHandler.performLiveRestore(sourcePath, vmObject.vmName, restoreStartDetails.instanceID, false));
                    lrThread.Start();

                    //wait for init
                    while (lrHandler.State == HVRestoreCore.LiveRestore.lrState.initializing)
                    {
                        System.Threading.Thread.Sleep(100);
                    }

                    if (lrHandler.State == HVRestoreCore.LiveRestore.lrState.running)
                    {
                        //set heartbeat timer to now
                        App_Start.RunningRestoreJobs.LastHeartbeat = DateTimeOffset.Now.ToUnixTimeSeconds();

                        response.StatusCode = HttpStatusCode.OK;
                    }
                    else
                    {
                        App_Start.RunningRestoreJobs.CurrentLiveRestore = null;
                        response.StatusCode = HttpStatusCode.InternalServerError;
                    }
                    break;

                case "flr":
                    HVRestoreCore.FileLevelRestoreHandler flrHandler = new HVRestoreCore.FileLevelRestoreHandler(jobObject.UseEncryption, jobObject.AesKey);

                    //set global object
                    App_Start.RunningRestoreJobs.CurrentFileLevelRestore = flrHandler;

                    //start thread
                    System.Threading.Thread flrThread = new System.Threading.Thread(() => flrHandler.performGuestFilesRestore(sourcePath, restoreStartDetails.instanceID, false, restoreStartDetails.selectedHDD));
                    flrThread.Start();

                    //wait for init
                    while (flrHandler.State.type == HVRestoreCore.FileLevelRestoreHandler.flrStateType.initializing)
                    {
                        System.Threading.Thread.Sleep(100);
                    }

                    //check state
                    if (flrHandler.State.type == HVRestoreCore.FileLevelRestoreHandler.flrStateType.running) //flr running
                    {
                        //set heartbeat timer to now
                        App_Start.RunningRestoreJobs.LastHeartbeat = DateTimeOffset.Now.ToUnixTimeSeconds();
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(flrHandler.GuestVolumes));
                        response.StatusCode = HttpStatusCode.OK;
                    }
                    else if (flrHandler.State.type == HVRestoreCore.FileLevelRestoreHandler.flrStateType.waitingForHDDSelect) //hdd select required
                    {
                        App_Start.RunningRestoreJobs.CurrentFileLevelRestore = null;
                        response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(flrHandler.State.hddsToSelect));
                        response.StatusCode = HttpStatusCode.Conflict;
                    }
                    else //server error
                    {
                        App_Start.RunningRestoreJobs.CurrentFileLevelRestore = null;
                        response.StatusCode = HttpStatusCode.InternalServerError;
                    }


                    break;
            }

            return response;
        }

        //gets triggered when a restore job should get stopped
        public void Delete()
        {
            //stop lr
            if (App_Start.RunningRestoreJobs.CurrentLiveRestore != null)
            {
                App_Start.RunningRestoreJobs.CurrentLiveRestore.StopRequest = true;
                App_Start.RunningRestoreJobs.CurrentLiveRestore = null;
            }

            //stop flr
            if (App_Start.RunningRestoreJobs.CurrentFileLevelRestore != null)
            {
                App_Start.RunningRestoreJobs.CurrentFileLevelRestore.StopRequest = true;
                App_Start.RunningRestoreJobs.CurrentFileLevelRestore = null;
            }
        }

        //receives the restore heartbeat
        public void Put()
        {
            App_Start.RunningRestoreJobs.LastHeartbeat = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public class RestoreStartDetails
        {
            public string type { get; set; }
            public string basePath { get; set; }
            public string destPath { get; set; }
            public string vmName { get; set; }
            public string instanceID { get; set; }
            public int jobID { get; set; }
            public string vmID { get; set; }
            public string selectedHDD { get; set; }
        }
    }
}