using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Drawing;
using System.IO;
using System.Net.Http.Headers;

namespace Frontend.Controllers
{
    public class FileIconController : ApiController
    {
        public HttpResponseMessage Get(string path)
        {
            //convert base64 path to string
            byte[] pathBytes = System.Convert.FromBase64String(path);
            path = System.Text.Encoding.Default.GetString(pathBytes);

            Icon fileIcon = Icon.ExtractAssociatedIcon(path);

            //create bitmap
            Bitmap bmp = fileIcon.ToBitmap();

            MemoryStream memStream = new MemoryStream();
            bmp.Save(memStream, System.Drawing.Imaging.ImageFormat.Png);
            memStream.Seek(0, SeekOrigin.Begin);


            HttpResponseMessage response = new HttpResponseMessage();
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StreamContent(memStream);
            response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
            response.Content.Headers.ContentDisposition.FileName = "icon.png";
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

            return response;
        }

       
    }
}