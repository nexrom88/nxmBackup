using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class BackupChainController : ApiController
    {
        // POST api/<controller>
        public void Post([FromBody] RestoreDetails jobDetails)
        {
            string sourcePath = jobDetails.basePath + "\\" + jobDetails.jobName + "\\" + jobDetails.vmName;

            //read backup chain
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backups = ConfigHandler.BackupConfigHandler.readChain(sourcePath);

            backups = null;
        }

        // PUT api/<controller>/5
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<controller>/5
        public void Delete(int id)
        {
        }

        public class RestoreDetails
        {
            public string basePath { get; set; }
            public string jobName { get; set; }
            public string vmName { get; set; }
        }
    }
}