using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class CheckSMBCredentialsController : ApiController
    {
        // POST api/<controller>
        public HttpResponseMessage Post([FromBody] SMBCredentials credentials)
        {
            HttpResponseMessage response = new HttpResponseMessage();
            //stop jobs first
            Frontend.App_Start.GUIJobHandler.jobHandler.stopAllTimers();

            //wipe all saved credentials
            Common.CredentialCacheManager.wipe();

            //add credentials to cacheManager
            Common.CredentialCacheManager.add(credentials.Path, credentials.Username, credentials.Password);

            //try to access targetPath
            try
            {
                System.IO.Directory.GetFiles(credentials.Path);
                response.StatusCode = HttpStatusCode.OK;

                //reinit jobs
                Frontend.App_Start.GUIJobHandler.initJobs();

                return response;

            }
            catch (Exception ex)
            {
                response.StatusCode = HttpStatusCode.NotFound;

                //reinit jobs
                Frontend.App_Start.GUIJobHandler.initJobs();

                return response;
            }
        }
    }

    public class SMBCredentials
    {
        public string Path { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}