using nxmBackup.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class SettingsController : ApiController
    {
        // read all global settings
        public HttpResponseMessage Get()
        {
            Dictionary<string, string> result = Common.DBQueries.readGlobalSettings(false);
            HttpResponseMessage response = new HttpResponseMessage();
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            return response;
        }

        
        // POST api/<controller>
        public void Post([FromBody] SettingsObject settings)
        {
            //build settings dictionary
            Dictionary<string, string> settingsDictionary = new Dictionary<string, string>();

            System.Reflection.PropertyInfo[] properties = settings.GetType().GetProperties();
            
            //iterate properties
            foreach(System.Reflection.PropertyInfo property in properties)
            {
                settingsDictionary.Add(property.Name, (string)settings.GetType().GetProperty(property.Name).GetValue(settings));
            }

            //write settings to db
            Common.DBQueries.writeGlobalSettings(settingsDictionary);

            //langauge changed? reload it
            if (LanguageHandler.getLanguageName() != settings.language)
            {
                LanguageHandler.initLanguage();
            }
        }

        public class SettingsObject
        {
            public string mountpath { get; set; }
            public string mailserver { get; set; }
            public string mailssl { get; set; }
            public string mailuser { get; set; }
            public string mailpassword { get; set; }
            public string mailsender { get; set; }
            public string mailrecipient { get; set; }
            public string language { get; set; }
        }

    }
}