using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class DirectoryController : ApiController
    {
        // POST api/<controller>
        public HttpResponseMessage Post([FromBody] NavPath value)
        {
            string requestedPath = value.path;
            string[] returnDirectories = null;

            //request root path?
            if (requestedPath == "/")
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                returnDirectories = new string[drives.Length];
                for (int i = 0; i < drives.Length; i++)
                {
                    returnDirectories[i] = drives[i].RootDirectory.FullName;
                }
            }
            else
            {

            }

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(returnDirectories));
            return response;
        }

     
        
        public class NavPath
        {
            public string path { get; set; }
        }

    }
}