using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class ImportBackupController : ApiController
    {
        // POST api/<controller>
        public HttpResponseMessage Post([FromBody] ImportPath path)
        {
            string[] files = System.IO.Directory.GetFiles(path.path);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            return response;
        }

        public class ImportPath
        {
            public string path { get; set; }
        }
    }
}