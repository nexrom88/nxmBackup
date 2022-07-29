using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Common
{
    public class SHA256Provider
    {
        public static byte[] computeHash(byte[] input)
        {
            using (SHA256 shaProvider = SHA256.Create())
            {
                byte[] hash = shaProvider.ComputeHash(input);
                return hash;
            }
        }
    }
}
