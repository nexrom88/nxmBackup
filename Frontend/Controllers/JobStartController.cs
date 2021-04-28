using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Threading;

namespace Frontend.Controllers
{
    public class JobStartController : ApiController
    {
        // GET api/<controller>
       

        // GET api/<controller>/5
        public void Get(int id)
        {
            Thread jobThread = new Thread(() => this.jobHandler.startManually(dbId));
            jobThread.Start();
        }

    }
}