using System;
using System.Collections.Generic;
using System.IO;
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
        public HttpResponseMessage Post([FromBody] Folder ioElement)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            //cancel request if flr not running
            if (App_Start.RunningRestoreJobs.CurrentFileLevelRestore == null || App_Start.RunningRestoreJobs.CurrentFileLevelRestore.StopRequest || App_Start.RunningRestoreJobs.CurrentFileLevelRestore.State.type != HVRestoreCore.FileLevelRestoreHandler.flrStateType.running)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                return response;
            }

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


            return response;
        }

        

        // handle a file or folder download
        public HttpResponseMessage GET(string path)
        {
            //convert base64 path to string
            byte[] pathBytes = System.Convert.FromBase64String(path);
            path = System.Text.Encoding.UTF8.GetString(pathBytes);

            HttpResponseMessage response;

            //check whether file or directory
            System.IO.FileAttributes attr = System.IO.File.GetAttributes(path);

            if (attr.HasFlag(FileAttributes.Directory)) //directory
            {
                FolderDownloader downloader = new FolderDownloader(path);


                //build retVal
                response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new PushStreamContent(downloader.WriteToStream, new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream"));
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = "nxmBackup.zip" };



            }
            else //file
            {      
                //open filestream
                FileStream sourceFile;
                try
                {
                    sourceFile = new FileStream(path, FileMode.Open, FileAccess.Read);
                }
                catch (Exception ex)
                {
                    HttpResponseMessage responseExc = new HttpResponseMessage(HttpStatusCode.NotFound);
                    return responseExc;
                }

                //build retVal
                response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StreamContent(sourceFile);
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
                response.Content.Headers.ContentDisposition.FileName = Path.GetFileName(path);
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            }

            return response;
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