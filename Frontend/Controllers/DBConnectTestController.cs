using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class DBConnectTestController : ApiController
    {
        // checks whether DB is connectable
        public HttpResponseMessage Get()
        {
            HttpResponseMessage response = new HttpResponseMessage();

            using (Common.DBConnection dbConnection = new Common.DBConnection())
            {
                if (dbConnection.ConnectionEstablished)
                {
                    response.StatusCode =  HttpStatusCode.OK;
                }
                else
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                }

            }

            return response;
        }

        
    }
}