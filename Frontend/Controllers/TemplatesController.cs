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
            string templateContent = System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Templates/" + name + ".html"));

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent (templateContent);

            return response;
        }
    }
}