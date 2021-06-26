using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]

    public class FLRBrowserController : ApiController
    {
        // GET all filesystem entries from current directory
        //public HttpResponseMessage Get()
        //{
        //    HttpResponseMessage response = new HttpResponseMessage();

        //    //cancel request if flr not running
        //    if (App_Start.RunningRestoreJobs.CurrentFileLevelRestore == null)
        //    {
        //        response.StatusCode = HttpStatusCode.BadRequest;
        //        return response;
        //    }


        //}

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