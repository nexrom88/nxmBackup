using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class CheckSMBCredentialsController : ApiController
    {
        [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword,
        int dwLogonType, int dwLogonProvider, ref IntPtr phToken);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern Boolean CloseHandle(IntPtr hObject);


        // POST api/<controller>
        public HttpResponseMessage Post(SMBCredentials credentials)
        {
            HttpResponseMessage message = new HttpResponseMessage();
            string username;
            string domainName;

            //domain user given?
            if (credentials.Username.Contains(@"\")){
                string[] splitter = credentials.Username.Split(@"\".ToCharArray());

                //domain given but no username?
                if (splitter.Length != 2)
                {
                    message.StatusCode = HttpStatusCode.NotFound;
                    return message;
                }
                else
                {
                    username = splitter[1];
                    domainName = splitter[0];
                }
            }
            else
            {
                //just username
                username = credentials.Username;
                domainName = "";
            }

            IntPtr token = IntPtr.Zero;
            try
            {
                var logonSuccess = LogonUser(username, domainName, credentials.Password, 2, 0, ref token);
                if (logonSuccess)
                {
                    using (System.Security.Principal.WindowsImpersonationContext person = new System.Security.Principal.WindowsIdentity(token).Impersonate())
                    {
                        System.IO.Directory.GetFiles(credentials.Path);

                        person.Undo();
                        CloseHandle(token);
                    }
                }

            }catch(Exception ex)
            {
                message.StatusCode = HttpStatusCode.NotFound;
                return message;
            }

            message.StatusCode = HttpStatusCode.OK;
            return message;
        }

    }

    public class SMBCredentials
    {
        public string Path { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}