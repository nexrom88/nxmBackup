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
        
        //adds a given credential to cache
        private static void add(string host, string username, string password)
        {
            NetworkCredential newCredential = new NetworkCredential(host + @"\" + username , password);
            credentialCache.Add(new Uri( @"\\" + host), "Basic", newCredential);
        }

        //wipes all credentials from cache
        private static void wipe()
        {
            System.Collections.IEnumerator enumerator = credentialCache.GetEnumerator();

            while (enumerator.MoveNext())
            {
                credentialCache.
            }
        }
    }
}
