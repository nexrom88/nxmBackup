using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class BackupChainController : ApiController
    {
        // POST api/<controller>
        public HttpResponseMessage Post([FromBody] RestoreDetails jobDetails)
        {
            string sourcePath = jobDetails.basePath + "\\" + jobDetails.jobName + "\\" + jobDetails.vmName;

            //read backup chain
            List<ConfigHandler.BackupConfigHandler.BackupInfo> backups = ConfigHandler.BackupConfigHandler.readChain(sourcePath);

            //build response
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(backups));
            return response;
        }

        public class RestoreDetails
        {
            public string basePath { get; set; }
            public string jobName { get; set; }
            public string vmName { get; set; }
        }
    }
}