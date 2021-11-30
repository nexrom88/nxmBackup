using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class SettingsController : ApiController
    {
        // GET api/<controller>
        public HttpResponseMessage Get()
        {
            Dictionary<string, string> result = Common.DBQueries.readGlobalSettings();
            HttpResponseMessage response = new HttpResponseMessage();
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            return response;
        }

        
        // POST api/<controller>
        public void Post([FromBody] string value)
        {
        }

    }
}