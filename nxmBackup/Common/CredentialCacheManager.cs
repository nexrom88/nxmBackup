using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Runtime.InteropServices;

namespace Common
{
    public class CredentialCacheManager
    {
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
        const int CONNECT_UPDATE_PROFILE = 0x00000001;
        const int RESOURCETYPE_DISK = 0x00000001;

        private static List<string> cachedHosts = new List<string>();
        
        //adds a given credential to cache
        public static void add(string targetPath, string username, string password)
        {
            //to be considered: targetPath == \\host\share
            //get host part
            string host = targetPath.Substring(2);
            host = host.Split(@"\".ToCharArray())[0];

            NETRESOURCE nr = new NETRESOURCE();
            nr.dwType = RESOURCETYPE_DISK;
            nr.lpRemoteName = @"\\" + host;

            int result = WNetUseConnection(IntPtr.Zero, nr, password, username, 0, null, null, null);

            //error on connecting?
            if (result != 0)
            {
                DBQueries.addLog("error on adding smb credentials. Code:" + result, Environment.StackTrace, null);
            }


        }

        //wipes all credentials from cache
        //public static void wipe()
        //{
        //    //iterate through all cached uris and remove them
        //    foreach (string host in cachedHosts)
        //    {
        //        WNetCancelConnection2(@"\\" + host, CONNECT_UPDATE_PROFILE, false);
        //    }

        //    //clear host list
        //    cachedHosts.Clear();
        //}
    }
}
