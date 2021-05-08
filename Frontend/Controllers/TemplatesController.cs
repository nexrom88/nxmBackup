using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class TemplatesController : ApiController
    {

        // GET templates
        public HttpResponseMessage Get(string name)
        {
            string file = System.IO.Path.Combine(AppContext.BaseDirectory, "Templates/" + name + ".html");
            
            //does file exist?
            if (!System.IO.File.Exists(file))
            {
                HttpResponseMessage responseError = new HttpResponseMessage(HttpStatusCode.NotFound);

                return responseError;
            }

            string templateContent = System.IO.File.ReadAllText(file);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent (templateContent);

            return response;
        }
    }
}