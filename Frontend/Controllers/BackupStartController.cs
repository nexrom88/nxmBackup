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
        public void Post([FromBody] BackupStartDetails backupStartDetails)
        {
            ConfigHandler.OneJob jobObject = null;
            //look for the matching job object
            foreach(ConfigHandler.OneJob job in ConfigHandler.JobConfigHandler.Jobs)
            {
                if (job.DbId == backupStartDetails.jobID)
                {
                    jobObject = job;
                }
            }

            //look for vm object
            Common.JobVM vmObject = null;
            foreach(Common.JobVM vm in jobObject.JobVMs)
            {
                if (vm.vmID == backupStartDetails.vmID)
                {
                    vmObject = vm;
                }
            }

           

            switch (backupStartDetails.type)
            {
                case "full":
                case "fullImport":
                    int jobExecutionId = Common.DBQueries.addJobExecution(backupStartDetails.jobID, "restore");
                    HVRestoreCore.FullRestoreHandler fullRestoreHandler = new HVRestoreCore.FullRestoreHandler(new Common.EventHandler(vmObject, jobExecutionId), jobObject.UseEncryption, jobObject.AesKey);
                    //fullRestoreHandler.performFullRestoreProcess(sourcePath, "f:\\target", ((ComboBoxItem)cbVMs.SelectedItem).Content.ToString() + "_restored", restorePoint.InstanceId, importToHyperV);

                    break;
            }
        }

        public class BackupStartDetails
        {
            public string type { get; set; }
            public string sourcePath { get; set; }
            public string destPath { get; set; }
            public string vmName { get; set; }
            public string instanceID { get; set; }
            public int jobID { get; set; }
            public string vmID { get; set; }
        }
    }
}