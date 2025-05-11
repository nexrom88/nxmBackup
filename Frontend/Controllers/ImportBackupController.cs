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
            HttpResponseMessage response;
            string configPath = path.path + "\\config.xml";
            if (!System.IO.File.Exists(configPath))
            {
                //config file does not exist -> error
                response = new HttpResponseMessage(HttpStatusCode.NotFound);
                return response;
            }

            response = new HttpResponseMessage(HttpStatusCode.OK);
            return response;
        }

        public class ImportPath
        {
            public string path { get; set; }
        }
    }
}