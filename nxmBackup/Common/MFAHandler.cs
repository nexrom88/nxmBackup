using OtpNet;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class MFAHandler
    {
        //generates a new secret key
        public static string GenerateNewKey()
        {
            byte[] secret = KeyGeneration.GenerateRandomKey(20);
            string base32Secret = Base32Encoding.ToString(secret);

            return base32Secret;
        }

    }
}
