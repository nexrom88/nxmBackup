using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class SHA1Provider
    {
        public static byte[] computeHash(byte[] input)
        {
            using (System.Security.Cryptography.SHA1 md5 = System.Security.Cryptography.SHA1.Create())
            {
                return md5.ComputeHash(input);
            }
        }
    }
}
