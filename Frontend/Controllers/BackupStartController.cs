using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class BackupStartController : ApiController
    {
        // POST api/<controller>
        public void Post([FromBody] RestoreStartDetails restoreStartDetails)
        {
            ConfigHandler.OneJob jobObject = null;
            //look for the matching job object
            foreach(ConfigHandler.OneJob job in ConfigHandler.JobConfigHandler.Jobs)
            {
                if (job.DbId == restoreStartDetails.jobID)
                {
                    jobObject = job;
                }
            }

            //look for vm object
            Common.JobVM vmObject = null;
            foreach(Common.JobVM vm in jobObject.JobVMs)
            {
                if (vm.vmID == restoreStartDetails.vmID)
                {
                    vmObject = vm;
                }
            }

            //build source patch
            string sourcePath = restoreStartDetails.basePath + "\\" + jobObject.Name + "\\" + restoreStartDetails.vmID;


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
                    lrHandler.performLiveRestore(sourcePath, vmObject.vmName, restoreStartDetails.instanceID);
                    break;
            }
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
        }
    }
}