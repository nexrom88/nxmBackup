﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.Http.Results;
using Common;
using QRCoder;

namespace Frontend.Controllers
{
    [Frontend.Filter.AuthFilter]
    public class MFAController : ApiController
    {
        //inits a new key and returns qr image
        public HttpResponseMessage Get()
        {
            HttpResponseMessage response = new HttpResponseMessage();
            string newKey = Common.MFAHandler.GenerateNewKey();

            //get server name for otp caption
            string machineName = Environment.MachineName;

            //generate qr image
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(@"otpauth://totp/nxmBackup@" + machineName + "?secret=" + newKey, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new QRCode(qrCodeData);
            Bitmap qrCodeImage = qrCode.GetGraphic(20);

            //convert image to byte array
            System.IO.MemoryStream imageStream = new System.IO.MemoryStream();
            qrCodeImage.Save(imageStream, System.Drawing.Imaging.ImageFormat.Png);
            byte[] imageBuffer = imageStream.ToArray();
            imageStream.Close();

            //return image
            response.StatusCode = HttpStatusCode.OK;
            response.Content = new ByteArrayContent(imageBuffer);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return response;
        }

        // verify otp
        public HttpResponseMessage Post([FromBody] OTPLoginParams otpParams)
        {
            HttpResponseMessage message = new HttpResponseMessage();

            //check otp
            if (MFAHandler.verifyOTP(otpParams.otp))
            {
                message.StatusCode = HttpStatusCode.OK;
            }
            else
            {
                message.StatusCode= HttpStatusCode.BadRequest;
            }

            return message;
        }

        // delete otp
        public HttpResponseMessage Delete()
        {
            HttpResponseMessage message = new HttpResponseMessage();

            MFAHandler.deleteKey();

            message.StatusCode = HttpStatusCode.OK;
            return message;
        }

        //write temp key to db
        public HttpResponseMessage Put([FromBody] OTPLoginParams otpParams)
        {
            HttpResponseMessage message = new HttpResponseMessage();

            //verify otp first
            if (!MFAHandler.verifyOTP(otpParams.otp))
            {
                message.StatusCode = HttpStatusCode.BadRequest;
                return message;
            }

            //write key to db
            if (!MFAHandler.writeKeyToDB())
            {
                message.StatusCode = HttpStatusCode.NotFound;
                return message;
            }

            //everything successful
            message.StatusCode = HttpStatusCode.OK;
            return message;
        }


    }

    public class OTPLoginParams
    {
        public string otp;
    }
}