using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class LoadTextController : ApiController
    {     
        // gets the text for a given language
        public HttpResponseMessage Post([FromBody] TextDescriptor descriptor)
        {
            HttpResponseMessage response = new HttpResponseMessage();

            //read currently set language
            string currentLanguage = DBQueries.readGlobalSetting("language");

            //get text
            string text = nxmBackup.Language.LanguageHandler.getString(descriptor.name, currentLanguage);

            //build retval
            response.Content = new StringContent(text);
            return response;
        }

       public class TextDescriptor
        {
            public string name { get; set; }
        }
    }
}