using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class wipeDBController : ApiController
    {
        // GET api/<controller>/5
        public void Get()
        {
            Common.DBQueries.wipeDB();
        }

       
    }
}