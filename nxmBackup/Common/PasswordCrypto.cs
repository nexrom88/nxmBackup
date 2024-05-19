using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class PasswordCrypto
    {
        private static byte[] aesStaticKey = { 0x34, 0x2, 0xe3, 0xaa, 0x88, 0xf7, 0xbb, 0x9a, 0x71, 0x4b, 0x28, 0xa1, 0xc5, 0x04, 0xa7, 0xe1 };

        //decrypts a given decrypted passwort string to plain text
        public static string decryptPassword(string encryptedPassword)
        {
            //when encrypted password is an empty string, return an empty string
            if (encryptedPassword == null || encryptedPassword == "")
            {
                return "";
            }

            //decode base64 string
            byte[] encodedBytes = Convert.FromBase64String(encryptedPassword);

            //get iv length
            UInt32 ivLength = BitConverter.ToUInt32(encodedBytes, 0);
            byte[] iv = new byte[ivLength];

            //get iv
            Array.Copy(encodedBytes, 4, iv, 0, ivLength);

            //init buffer for encrypted password
            byte[] encryptedPasswordBytes = new byte[encodedBytes.Length - (4 + ivLength)];
            Array.Copy(encodedBytes, 4 + ivLength, encryptedPasswordBytes, 0, encryptedPasswordBytes.Length);

            //init crypto system
            AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider();
            aesProvider.Key = aesStaticKey;
            aesProvider.IV = iv;
            ICryptoTransform decryptor = aesProvider.CreateDecryptor(aesProvider.Key, aesProvider.IV);
            MemoryStream memStream = new MemoryStream(encryptedPasswordBytes);

            //start crypto stream
            CryptoStream cryptoStream = new CryptoStream(memStream, decryptor, CryptoStreamMode.Read);

            //decrypt password
            MemoryStream decryptedStream = new MemoryStream();
            cryptoStream.CopyTo(decryptedStream);

            byte[] decryptedBytes = decryptedStream.ToArray();
            cryptoStream.Close();
            decryptedStream.Close();

            //decode bytes to utf8 and return string 
            return Encoding.UTF8.GetString(decryptedBytes);
        }


        //encrypts a given plain text password and returns its base64 string
        public static string encrpytPassword(string password)
        {
            AesCryptoServiceProvider aesProvider = new AesCryptoServiceProvider();
            aesProvider.Key = aesStaticKey;
            aesProvider.GenerateIV();

            //init crypto system
            ICryptoTransform encryptor = aesProvider.CreateEncryptor(aesProvider.Key, aesProvider.IV);
            MemoryStream memStream = new MemoryStream();

            //write iv length to mem stream
            memStream.Write(BitConverter.GetBytes(aesProvider.IV.Length), 0, 4);

            //write iv to mem stream
            memStream.Write(aesProvider.IV, 0, aesProvider.IV.Length);

            //start crypto stream
            CryptoStream cryptoStream = new CryptoStream(memStream, encryptor, CryptoStreamMode.Write);

            //encrypt pw
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            cryptoStream.Write(passwordBytes, 0, passwordBytes.Length);
            cryptoStream.FlushFinalBlock();

            //cleanup
            string retVal = Convert.ToBase64String(memStream.ToArray());
            cryptoStream.Close();
            memStream.Close();

            return retVal;

        }
    }
}
