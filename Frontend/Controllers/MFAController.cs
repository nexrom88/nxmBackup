using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class MFAController : ApiController
    {
        //inits a new key and returns qr image
        public string Get()
        {
            return "value";
        }

        // verify otp
        public void Post([FromBody] OTPLoginParams otpParams)
        {

        }
    }

    public class OTPLoginParams
    {
        public string otp;
    }
}