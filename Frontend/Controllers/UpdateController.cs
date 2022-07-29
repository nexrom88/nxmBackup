using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Reflection;
using System.IO;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class UpdateController : ApiController
    {

        // return whether a new update is available or not
        public HttpResponseMessage Get()
        {
            UpdateStruct updateStruct = new UpdateStruct();
            HttpResponseMessage response = new HttpResponseMessage();
            response.StatusCode = HttpStatusCode.OK;


            //read own version
            string installedVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string availableVersion = "";
            updateStruct.InstalledVersion = installedVersion;

            //read available version
            try
            {
                using (var client = new WebClient())
                {
                    byte[] buffer = client.DownloadData("https://nxmBackup.com/nxmBackup/currentversion.txt");
                    availableVersion = System.Text.Encoding.UTF8.GetString(buffer);
                }
            }catch(Exception ex)
            {
                Common.DBQueries.addLog("update check failed", Environment.StackTrace, ex);
            }
            updateStruct.AvailableVersion = availableVersion;

            if (availableVersion != "")
            {
                //compare versions
                string[] availableBuffer = updateStruct.AvailableVersion.Split(".".ToCharArray());
                string[] installedBuffer = updateStruct.InstalledVersion.Split(".".ToCharArray());
                for (int i = 0; i < availableBuffer.Length; i++)
                {
                    if (int.Parse(availableBuffer[i]) > int.Parse(installedBuffer[i]))
                    {
                        updateStruct.UpdateAvailable = true;
                    }
                }
            }


            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(updateStruct));
            return response;
        }

        private struct UpdateStruct
        {
            public string InstalledVersion { get; set; }
            public string AvailableVersion { get; set; }
            public bool UpdateAvailable { get; set; }
        }

       
    }
}