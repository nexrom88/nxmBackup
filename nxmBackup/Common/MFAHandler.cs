using OtpNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class MFAHandler
    {
        private static string tempNewKey;

        //generates a new secret key
        public static string GenerateNewKey()
        {
            byte[] secret = KeyGeneration.GenerateRandomKey(20);
            string base32Secret = Base32Encoding.ToString(secret);

            //buffer key to wait for confirmation
            tempNewKey = base32Secret;

            return base32Secret;
        }


        //writes the temp key to db
        public static bool writeKeyToDB()
        {
            if (tempNewKey != "")
            {
                Dictionary<string, string> setting = new Dictionary<string, string>();
                setting.Add("otpkey", tempNewKey);
                DBQueries.writeGlobalSettings(setting);
                tempNewKey = "";
                return true;
            }
            else
            {
                //no key to write -> error
                return false;
            }
        }

        //verifies a given otp
        public static bool verifyOTP(string otp)
        {
            //read key from db
            string otpKey = DBQueries.readGlobalSetting("otpkey");

            if (otpKey == null || otpKey == "")
            {
                return false;
            }

            Totp totpHandler = new Totp(Base32Encoding.ToBytes(otpKey), 300);
            long timeStepMatched;

            return totpHandler.VerifyTotp(otp, out timeStepMatched);
        }
    }
}
