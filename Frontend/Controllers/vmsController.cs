using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Management;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class vmsController : ApiController
    {

        public HttpResponseMessage Get()
        {
            List<Common.WMIHelper.OneVM> vms = Common.WMIHelper.listVMs();
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(vms));
            return response;
        }

    }
}