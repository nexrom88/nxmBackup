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
        private string publicKey = @"MIICITANBgkqhkiG9w0BAQEFAAOCAg4AMIICCQKCAgBl7HW0pqlpI1ZHLCwa8fA+
YrbuO+o+GdSExvlwhIktDfRQj68xxwrRw15OQnbdHr5caM7cDRC8TRaUCtXEfHdU
W3kcomQPbNdaVumw965kLvWDGKLNxn9Nr/wxYrmNHKw8WAqkoC6BsH1WHYWZVAje
wa1TUnuyA8xFGUv7Fwc8R8aXYnmEZ6gGkrjtofXXFraL7ZJs4Y37L7IJXz3cdCuV
6EqdCWlODor7xRjN9fns4cvMzKLa4l/COxLuVxqP79bLfJGIrXs7YBdxf646j2r+
kSuvdafpnJ098DPF1s7B3K5BEInQk3S29fJJ4MV0eqsoDrUVveDI8LFx2MIKNXeQ
f3Ovxp+4F8eYhJ5XirH6hXXHUhiVrigpH5O2T1sKfIty8vXEqSXF6Q5OL1t7eCJS
l5g2jC1+lj1dp8lbUROmwcJOcopy9ZoF7Jxmq2MjEsDwx5/gR1qHInnfPhVUpItc
fw5yTtevkTHVZUZNPijCD1TSRyAAkE+/BHoQnaL/g6lJEb4Z86WPovBvGpWNigDr
equVl1AnbEkMTN8GvhOhTAdO1QOeAPV/IaZ4kqQ1vKGpoTYi3vAQ3ltC4VVYKIMR
fCDqHW0CJPyFTSeQfOUbWCVJyNHu0l+vkSEg9BIdU37t/8SvrwNg34NzsNbacHtY
RziLVUAIPhJyiIIqo/KoDQIDAQAB";

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
            csp.ImportCspBlob(Convert.FromBase64String(this.publicKey));

            byte[] encryptedData = csp.Encrypt(dbBuffer, true);

        }

       
    }
}