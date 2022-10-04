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
    public class JobStartStopController : ApiController
    {
        // GET api/<controller>
       

        // GET api/<controller>/5
        public void Get(int id)
        {
            //is job running => stop it
            if (App_Start.GUIJobHandler.jobHandler.isJobRunning(id)){
                App_Start.GUIJobHandler.jobHandler.stopJob(id);
            }
            else //job not running => start it
            {
                Thread jobThread = new Thread(() => App_Start.GUIJobHandler.jobHandler.startManually(id));
                jobThread.Start();
            }
        }

    }
}