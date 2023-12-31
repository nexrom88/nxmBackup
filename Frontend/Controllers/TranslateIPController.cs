using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class TranslateIPController : ApiController
    {
        public HttpResponseMessage Post([FromBody] TranslateIPObject value)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            //when host is an ip, try to translate
            Regex regExip = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
            if (regExip.IsMatch(value.host))
            {
                //try to translate to hostname
                string hostname = WMIHelper.translateToHostname(value.host, value.user, value.password);

                //propose hostname to frontend when found
                if (hostname != "")
                {
                    response.Content = new StringContent(hostname);
                    response.StatusCode = HttpStatusCode.Found;
                    return response;
                }
                else
                {
                    //error -> hostname not found
                    response.StatusCode = HttpStatusCode.NotFound;
                    return response;
                }

            }
            else
            {
                //no ip -> HTTP 200
                response.StatusCode = HttpStatusCode.OK;
                return response;
            }
        }

 
    }

    public class TranslateIPObject
    {
        public string host { get; set; }
        public string user { get; set; }
        public string password { get; set; }
    }
}