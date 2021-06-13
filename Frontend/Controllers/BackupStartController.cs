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
        public void Post([FromBody] BackupStartDetails value)
        {
        }

        public class BackupStartDetails
        {
            public bool type { get; set; }
            public string sourcePath { get; set; }
            public string destPath { get; set; }
            public string vmName { get; set; }
            public string instanceID { get; set; }
        }
    }
}