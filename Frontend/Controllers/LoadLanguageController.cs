using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class LoadLanguageController : ApiController
    {     
        // gets the text for a given language
        public HttpResponseMessage Get()
        {
            HttpResponseMessage response = new HttpResponseMessage();

            //read currently set language
            //string currentLanguage = DBQueries.readGlobalSetting("language");

            //get language
            Dictionary<string, string> language = nxmBackup.Language.LanguageHandler.getLanguage();

            //build retval
            string jsonLangObject = Newtonsoft.Json.JsonConvert.SerializeObject(language);
            response.Content = new StringContent(jsonLangObject);
            return response;
        }
    }
}