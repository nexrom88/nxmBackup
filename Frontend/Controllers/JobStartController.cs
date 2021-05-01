using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Threading;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class JobStartController : ApiController
    {
        // GET api/<controller>
       

        // GET api/<controller>/5
        public void Get(int id)
        {
            Thread jobThread = new Thread(() => App_Start.GUIJobHandler.jobHandler.startManually(id));
            jobThread.Start();
        }

    }
}