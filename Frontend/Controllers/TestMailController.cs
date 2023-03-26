using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Frontend.Controllers
{
    public class TestMailController : ApiController
    {
        // GET api/<controller>/
        public HttpResponseMessage Post([FromBody] MailTestObject mailSettings)
        {
            bool useSSL;
            HttpResponseMessage message = new HttpResponseMessage();

            //when no password given, read it from global settings
            if (mailSettings.mailpassword == "")
            {
                mailSettings.mailpassword = DBQueries.readGlobalSetting("mailpassword");
            }
            //parse usessl value
            if (!bool.TryParse(mailSettings.mailssl, out useSSL))
            {
                message.StatusCode = HttpStatusCode.InternalServerError;
                return message;
            }

            MailClient mailClient = new MailClient(mailSettings.mailserver, mailSettings.mailuser, mailSettings.mailpassword, mailSettings.mailsender, useSSL);

            //send mail
            if (!mailClient.sendMail(nxmBackup.Language.LanguageHandler.getString("test_mail_subject"), nxmBackup.Language.LanguageHandler.getString("test_mail_content"), false, mailSettings.mailrecipient))
            {
                message.StatusCode = HttpStatusCode.InternalServerError;
            }
            else
            {
                message.StatusCode = HttpStatusCode.OK;
            }
            return message;


        }

        public class MailTestObject
        {
            public string mailserver { get; set; }
            public string mailssl { get; set; }
            public string mailuser { get; set; }
            public string mailpassword { get; set; }
            public string mailsender { get; set; }
            public string mailrecipient { get; set; }
        }
    }
}