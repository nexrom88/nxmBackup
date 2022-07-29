using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class CheckSMBCredentialsController : ApiController
    {
        const int RESOURCE_CONNECTED = 0x00000001;
        const int RESOURCE_GLOBALNET = 0x00000002;
        const int RESOURCE_REMEMBERED = 0x00000003;

        const int RESOURCETYPE_ANY = 0x00000000;
        const int RESOURCETYPE_DISK = 0x00000001;
        const int RESOURCETYPE_PRINT = 0x00000002;

        const int RESOURCEDISPLAYTYPE_GENERIC = 0x00000000;
        const int RESOURCEDISPLAYTYPE_DOMAIN = 0x00000001;
        const int RESOURCEDISPLAYTYPE_SERVER = 0x00000002;
        const int RESOURCEDISPLAYTYPE_SHARE = 0x00000003;
        const int RESOURCEDISPLAYTYPE_FILE = 0x00000004;
        const int RESOURCEDISPLAYTYPE_GROUP = 0x00000005;

        const int RESOURCEUSAGE_CONNECTABLE = 0x00000001;
        const int RESOURCEUSAGE_CONTAINER = 0x00000002;


        const int CONNECT_INTERACTIVE = 0x00000008;
        const int CONNECT_PROMPT = 0x00000010;
        const int CONNECT_REDIRECT = 0x00000080;
        const int CONNECT_UPDATE_PROFILE = 0x00000001;
        const int CONNECT_COMMANDLINE = 0x00000800;
        const int CONNECT_CMD_SAVECRED = 0x00001000;

        const int CONNECT_LOCALDRIVE = 0x00000100;

        const int NO_ERROR = 0;

        const int ERROR_ACCESS_DENIED = 5;
        const int ERROR_ALREADY_ASSIGNED = 85;
        const int ERROR_BAD_DEVICE = 1200;
        const int ERROR_BAD_NET_NAME = 67;
        const int ERROR_BAD_PROVIDER = 1204;
        const int ERROR_CANCELLED = 1223;
        const int ERROR_EXTENDED_ERROR = 1208;
        const int ERROR_INVALID_ADDRESS = 487;
        const int ERROR_INVALID_PARAMETER = 87;
        const int ERROR_INVALID_PASSWORD = 1216;
        const int ERROR_MORE_DATA = 234;
        const int ERROR_NO_MORE_ITEMS = 259;
        const int ERROR_NO_NET_OR_BAD_PATH = 1203;
        const int ERROR_NO_NETWORK = 1222;

        const int ERROR_BAD_PROFILE = 1206;
        const int ERROR_CANNOT_OPEN_PROFILE = 1205;
        const int ERROR_DEVICE_IN_USE = 2404;
        const int ERROR_NOT_CONNECTED = 2250;
        const int ERROR_OPEN_FILES = 2401;

        [DllImport("Mpr.dll")]
        private static extern int WNetUseConnection(
           IntPtr hwndOwner,
           NETRESOURCE lpNetResource,
           string lpPassword,
           string lpUserID,
           int dwFlags,
           string lpAccessName,
           string lpBufferSize,
           string lpResult
       );

        [DllImport("Mpr.dll")]
        private static extern int WNetCancelConnection2(
            string lpName,
            int dwFlags,
            bool fForce
        );

        [StructLayout(LayoutKind.Sequential)]
        private class NETRESOURCE
        {
            public int dwScope = 0;
            public int dwType = 0;
            public int dwDisplayType = 0;
            public int dwUsage = 0;
            public string lpLocalName = "";
            public string lpRemoteName = "";
            public string lpComment = "";
            public string lpProvider = "";
        }

        // POST api/<controller>
        public HttpResponseMessage Post([FromBody] SMBCredentials credentials)
        {
            //get host from smb path
            string host = credentials.Path.Substring(2).Split(@"\".ToCharArray())[0];

            HttpResponseMessage response = new HttpResponseMessage();
            NETRESOURCE nr = new NETRESOURCE();
            nr.dwType = RESOURCETYPE_DISK;
            nr.lpRemoteName = @"\\" + host;

            int result = WNetUseConnection(IntPtr.Zero, nr, credentials.Password, credentials.Username, 0, null, null, null);

            //error on connecting?
            if (result != NO_ERROR)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }

            //try listing files
            try
            {
                System.IO.Directory.GetFiles(credentials.Path);
            }catch (Exception ex)
            {
                //WNetCancelConnection2(nr.lpRemoteName, CONNECT_UPDATE_PROFILE, false);
                response.StatusCode = HttpStatusCode.NotFound;
                return response;
            }

            //WNetCancelConnection2(nr.lpRemoteName, CONNECT_UPDATE_PROFILE, false);
            response.StatusCode = HttpStatusCode.OK;
            return response;

        }

        
    }

    public class SMBCredentials
    {
        public string Path { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}