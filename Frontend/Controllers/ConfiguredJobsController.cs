using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ConfigHandler;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class ConfiguredJobsController : ApiController //api/ConfiguredJobs
    {
        // GET api/<controller>
        public HttpResponseMessage Get()
        {
            HttpResponseMessage response;
            try
            {
                response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(JobConfigHandler.Jobs));
                return response;
            }catch(Exception ex)
            {
                response = new HttpResponseMessage(HttpStatusCode.BadRequest);
                return response;
            }
            
        }

        // GET api/<controller>/5
        public string Get(int id)
        {
            return "value";
        }

        // POST api/<controller>
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<controller>/5
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<controller>/5
        public void Delete(int id)
        {
        }
    }
}