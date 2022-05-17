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
            List<string> tempDrivesList = new List<string>();
            string[] returnDirectories = null;

            //request root path?
            if (requestedPath == "/")
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                 returnDirectories = new string[drives.Length];
                for (int i = 0; i < drives.Length; i++)
                {
                    //just list ntfs drives
                    try
                    {
                        if (drives[i].DriveFormat.ToLower() != "ntfs")
                        {
                            continue;
                        }
                    }catch(Exception ex)
                    {
                        continue;
                    }

                    //just add drive if accessible
                    try
                    {
                        System.IO.Directory.GetDirectories(drives[i].RootDirectory.FullName);
                    }catch(Exception ex)
                    {
                        continue;
                    }

                 

                    tempDrivesList.Add(drives[i].RootDirectory.FullName);
                }

                returnDirectories = tempDrivesList.ToArray();
            }
            else //request is "normal" folder
            {
                string[] folders = System.IO.Directory.GetDirectories(requestedPath);

                returnDirectories = new string[folders.Length];
                for (int i = 0; i < folders.Length; i++)
                {
                    DirectoryInfo dir = new DirectoryInfo(folders[i]);
                    returnDirectories[i] = dir.Name;
                }
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