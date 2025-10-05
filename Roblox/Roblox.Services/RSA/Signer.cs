using System.Security.Cryptography;
using System.Text;
using Roblox.Dto.Users;

namespace Roblox.Services
{
    public class RSASignService : ServiceBase, IService
    {
		public string SignScript(string script, bool is2048 = false)
        {
            var signature = SignData("\r\n" + script, is2048);
            return is2048 ? $"--rbxsig2%{signature}%" : $"--rbxsig%{signature}%";
        }
		
        public string SignData(string data, bool is2048 = false)
        {
            try
            {
                string Key = is2048 ? 
                    Path.Combine("RSA", "PrivateKey2020.pem") : 
                    Path.Combine("RSA", "PrivateKey.pem");
                
                if (!File.Exists(Key))
                {
                    throw new FileNotFoundException($"Private key not found!");
                }

                string PrivateKey = File.ReadAllText(Key);
                using RSA rsa = RSA.Create();
                rsa.ImportFromPem(PrivateKey.ToCharArray());

                byte[] DataBytes = Encoding.UTF8.GetBytes(data);
                byte[] SigBytes = rsa.SignData(DataBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
                
                return Convert.ToBase64String(SigBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RSA signing error: {ex.Message}");
                throw;
            }
        }

        public string GenerateClientTicket2020(UserInfo user, string membershipType, int accountAgeDays, string jobId, long placeId)
        {
            var DateTimeStr = DateTime.Now.ToString("M/d/yyyy h:mm:ss tt");
            var CharApp = $"{Configuration.BaseUrl}/v1.1/avatar-fetch?userId={user.userId}&placeId={placeId}";

            var FirstUnsigned = $"{user.userId}\n{user.username}\n{CharApp}\n{jobId}\n{DateTimeStr}";
            var FirstSigned = SignData(FirstUnsigned, true);

            var countryCode = "US";
            
            var SecondUnsigned = $"{DateTimeStr}\n" +
                $"{jobId}\n" +
                $"{user.userId}\n" +
                $"{user.userId}\n" +
                $"0\n" +
                $"{accountAgeDays}\n" +
                $"f\n" +
                $"{user.username.Length}\n" +
                $"{user.username}\n" +
                $"{membershipType.Length}\n" +
                $"{membershipType}\n" +
                $"{countryCode.Length}\n" +
                $"{countryCode}\n" +
                $"0\n\n" +
                $"{user.username.Length}\n" +
                $"{user.username}";

            var SecondSigned = SignData(SecondUnsigned, true);

            return $"{DateTimeStr};{FirstSigned};{SecondSigned};4";
        }
		public bool IsThreadSafe() => true;
        public bool IsReusable() => false;
    }
}