using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]

    public class FLRBrowserController : ApiController
    {
        // Get all filesystem entries from current directory
        public HttpResponseMessage Post([FromBody]Folder ioElement)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            //cancel request if flr not running
            if (App_Start.RunningRestoreJobs.CurrentFileLevelRestore == null || App_Start.RunningRestoreJobs.CurrentFileLevelRestore.StopRequest || App_Start.RunningRestoreJobs.CurrentFileLevelRestore.State.type != HVRestoreCore.FileLevelRestoreHandler.flrStateType.running)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                return response;
            }

            //list directory?
            if (isDirectory(ioElement.path))
            {
                //get files
                string[] files = System.IO.Directory.GetFiles(ioElement.path, "*", System.IO.SearchOption.TopDirectoryOnly);

                //get directories
                string[] directories = System.IO.Directory.GetDirectories(ioElement.path, "*", System.IO.SearchOption.TopDirectoryOnly);

                //build ret val
                List<FSEntry> fsEntries = new List<FSEntry>();
                foreach (string file in files)
                {
                    FSEntry newEntry = new FSEntry();
                    newEntry.type = "file";
                    newEntry.path = file;
                    fsEntries.Add(newEntry);
                }
                foreach (string directory in directories)
                {
                    FSEntry newEntry = new FSEntry();
                    newEntry.type = "directory";
                    newEntry.path = directory;
                    fsEntries.Add(newEntry);
                }

                //build json string
                string retVal = Newtonsoft.Json.JsonConvert.SerializeObject(fsEntries);
                response.StatusCode = HttpStatusCode.OK;
                response.Content = new StringContent(retVal);

            }
            else //file download requested
            {
                response.Content = new StreamContent(new System.IO.FileStream(ioElement.path, System.IO.FileMode.Open, System.IO.FileAccess.Read));
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = "testing.xlsx";
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                //response.Content.Headers.Add("x-filename", "testing.xlsx"); //We will use this below
            }
            return response;
        }

        //checks whether a given path is a directory or not
        private bool isDirectory(string path)
        {
            System.IO.FileAttributes attr = System.IO.File.GetAttributes(path);

            return attr.HasFlag(System.IO.FileAttributes.Directory);
        }

        // GET api/<controller>/5
        public string Get(int id)
        {
            return "value";
        }

        // PUT api/<controller>/5
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<controller>/5
        public void Delete(int id)
        {
        }

        public class FSEntry
        {
            public string path { get; set; }
            public string type { get; set; } //directory || file
        }

        public class Folder
        {
            public string path { get; set; }
        }
    }
}