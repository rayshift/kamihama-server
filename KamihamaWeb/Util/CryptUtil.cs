using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace KamihamaWeb.Util
{
    public class CryptUtil
    {
        public static string CalculateMd5File(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filename);

            StringBuilder sb = new StringBuilder();
            foreach (byte b in md5.ComputeHash(stream))
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        public static string CalculateSha256(string text)
        {
            using var sha = SHA256.Create();

            StringBuilder sb = new StringBuilder();
            foreach (byte b in sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        public static byte[] ReadFully(Stream input)
        {
            using MemoryStream ms = new MemoryStream();
            input.CopyTo(ms);
            return ms.ToArray();
        }

        public static object CalculateMd5Bytes(byte[] storeContents)
        {
            using var md5 = MD5.Create();
            using var stream = new MemoryStream(storeContents);

            StringBuilder sb = new StringBuilder();
            foreach (byte b in md5.ComputeHash(stream))
            {
                sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}