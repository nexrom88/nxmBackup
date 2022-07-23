﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Common
{
    public class MailClient
    {
        private string server, user, password, sender;
        private bool ssl;
        public MailClient(string server, string user, string password, string sender, bool ssl)
        {
            this.server = server;
            this.user = user;
            this.password = password;
            this.sender = sender;
            this.ssl = ssl;
        }

        //sends a given mail by using smtp
        public void sendMail(string subject, string body, bool html, string recipient)
        {
            MailMessage message = new MailMessage(this.sender, recipient);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = html;
            SmtpClient client = new SmtpClient(this.server);
            client.UseDefaultCredentials = true;
            client.EnableSsl = this.ssl;
            client.Credentials = new System.Net.NetworkCredential(this.user, this.password);
            client.Send(message);
        }
    }
}
