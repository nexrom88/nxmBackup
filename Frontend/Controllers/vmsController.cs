using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Management;
using Common;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class vmsController : ApiController
    {

        public HttpResponseMessage Get(int hostid)
        {
            HttpResponseMessage response;
            //translate hostid to host ip/name
            string hostName = DBQueries.getHostByID(hostid);

            List<Common.WMIHelper.OneVM> vms = Common.WMIHelper.listVMs(hostName);

            //error handling
            if (vms == null)
            {
                response = new HttpResponseMessage(HttpStatusCode.NotFound);
                return response;
            }

            response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(vms));
            return response;
        }

    }
}