using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Security.Cryptography;

namespace Frontend.Controllers
{
    public class SupportPackageController : ApiController
    {
        // starts the encrypted file download
        public HttpResponseMessage Get()
        {
            HttpResponseMessage response = new HttpResponseMessage();

            //read base path from registry if necessary
            string basePath = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\nxmBackup", "BasePath", "");
            if (basePath == null)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }
            string dbPath = System.IO.Path.Combine(basePath, "nxm.db");

            //read whole db file
            byte[] dbBuffer = System.IO.File.ReadAllBytes(dbPath);

            //init rsa module
            RSACryptoServiceProvider csp = new RSACryptoServiceProvider();


        }

       
    }
}