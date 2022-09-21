using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class WebClientWrapper
    {
        public static NxmStorageData translateNxmStorageData(string user, string password)
        {
            //get smb credentials via api call
            using (var client = new WebClient())
            {
                string userData = "user=" + user + "&password=" + password;
                client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                string rawDownloadedData;
                try
                {
                    rawDownloadedData = client.UploadString("https://nxmBackup.com/nxmstorageproxy/getshare.php", userData);
                    NxmStorageData nxmStorageData = new NxmStorageData();
                    Newtonsoft.Json.JsonConvert.PopulateObject(rawDownloadedData, nxmStorageData);
                    return nxmStorageData;

                }
                catch (Exception ex)
                {
                    //nxmstorage login not possible
                    DBQueries.addLog("nxmstorage login failure", Environment.StackTrace, ex);
                    return null;
                }
            }
        }


    }

    public class NxmStorageData
    {
        public string share { get; set; }
        public string share_user { get; set; }
        public string share_password { get; set; }
    }

}
