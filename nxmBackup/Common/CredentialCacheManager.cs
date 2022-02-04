using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace Common
{
    public class CredentialCacheManager
    {
        private static CredentialCache credentialCache = new CredentialCache();
        private static List<Uri> cacheURIs = new List<Uri>();
        
        //adds a given credential to cache
        public static void add(string host, string username, string password)
        {
            NetworkCredential newCredential = new NetworkCredential(host + @"\" + username , password);
            credentialCache.Add(new Uri( @"\\" + host), "Basic", newCredential);
        }

        //wipes all credentials from cache
        public static void wipe()
        {
            //iterate through all cached uris and remove them
            foreach (Uri uri in cacheURIs)
            {
                credentialCache.Remove(uri, "Basic");
            }

            //clear uri-list
            cacheURIs.Clear();
        }
    }
}
