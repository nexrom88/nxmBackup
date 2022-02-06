﻿using System;
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
        public static void add(string targetPath, string username, string password)
        {
            //to be considered: targetPath == \\host\share
            //get host part
            string host = targetPath.Substring(2);
            host = host.Split(@"\".ToCharArray())[0];

            NetworkCredential newCredential = new NetworkCredential(username , password);
            Uri newUri = new Uri(@"\\" + host);

            try
            {
                credentialCache.Add(newUri, "Basic", newCredential);
            }catch (Exception ex)
            {
                DBQueries.addLog("error on adding credential to cache", Environment.StackTrace, ex);
            }
            

            cacheURIs.Add(newUri);
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
