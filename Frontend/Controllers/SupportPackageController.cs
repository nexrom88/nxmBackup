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
        private string publicKey = @"BgIAAACkAABSU0ExABAAAAEAAQCB2pWs9AOXhjuFyUgcDC6lJ86bQ5hDwM14GvBLQ1A+KATplmKCkc3LT7o7TBqaCxrHjwBcV6a2rhvYQeyzCNZGxgQsju1Zk82F3R7iKSHa6zgvzJcIYEI7w8WvVDFkPcM9ntAX/b/dUVGY3GH2+WiKJyLJoanjN0qYFLSQa0Tu5552OUSBBJZgdNNhM8LAtclmJB+A+4yyVNdGrsaVcxv4NH78OZ9A0ACtMiAMAFQYIpFj/uvuaY94lOci81piR3/zHrtSyMdsI6ITwXQLRnW4aew/8WuOoLwsir5k9c0QuSX91HSwJwob8vwWlH7pfrVeIDoa/RNEJLlM32r5sgx6dvDZ+3/xtWFhhAyDHrfIiezegu1xugkEFoc2X/4DjO4K0N9Udg3F8Y1PandJ/bNNgeVq0cFi3GghBzQzUvP7bbROSlPAxXM25OR/0gvgft4WGFn37m+PEvbOtoYA1U0U1yXFqE71i/4ibiUizh7APySG8/RTQDnl81UzNZo28XOrh2SNiiy623V7WGRHQ4P6E8T8/d7cAJS4d2mg8cho3Su5FFAeR47tKv7EEhFlHWl3n/2oU9ovPWdr1eUxI3MlIMLi1hYjdbOXFpH89Q5kInnJ9TLtJsGUtLqZ4j1Uu6GNrJkpzit9YQpmoRCW4bVpXGiTs7kB3G5C1A3AX/iAwg==";

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

            //init aes module
            AesManaged aes = new AesManaged();
            aes.GenerateKey();
            aes.GenerateIV();

            //build download stream
            System.IO.MemoryStream downloadStream = new System.IO.MemoryStream();
            downloadStream.Write(BitConverter.GetBytes(aes.KeySize / 8), 0, 4);

            //build buffer for key header
            byte[] keyBuffer = new byte[16 + aes.KeySize / 8];
            Array.Copy(aes.Key, 0, keyBuffer, 0, aes.Key.Length);
            Array.Copy(aes.IV, 0, keyBuffer, aes.Key.Length, 16);


            //init rsa module
            RSACryptoServiceProvider csp = new RSACryptoServiceProvider();
            
            csp.ImportCspBlob(Convert.FromBase64String(this.publicKey));

            //encrypt key by using rsa
            byte[] encryptedData = csp.Encrypt(keyBuffer, true);
            
            //write encrypted data to download-stream
            downloadStream.Write(encryptedData, 0, encryptedData.Length);

            //encrypt db file with aes key
            ICryptoTransform encryptor = aes.CreateEncryptor();
            System.IO.MemoryStream aesOutputStream = new System.IO.MemoryStream();
            using (CryptoStream cs = new CryptoStream(aesOutputStream, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(dbBuffer, 0, dbBuffer.Length);
                //move encrypted data to download-stream
                aesOutputStream.WriteTo(downloadStream);
                downloadStream.Seek(0, System.IO.SeekOrigin.Begin);
            }

            //start file download
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(downloadStream);
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = "nxmBackup_Support.nxms";
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            return response;

        }

       
    }
}

//file structure
// 4 byte: key length
// key length bytes: key
//16 length bytes: iv
//db byte data