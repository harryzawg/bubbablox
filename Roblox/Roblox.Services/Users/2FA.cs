using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Dapper;
using OtpNet;

namespace Roblox.Services
{
    public class TwoFactorService : ServiceBase, IService
    {
		// sorry about using user_email, will fix later 
        private const int TwoFactorStatusCode = 1;
		private async Task<(long Id, string Secret)?> GetTwoFactorSecret(long userId)
		{
			var record = await db.QuerySingleOrDefaultAsync<(long id, string secret)?>(
				"SELECT id, email AS secret FROM user_email WHERE user_id = :user_id",
				new { user_id = userId });
			return record;
		}

		// when setup fetched, get existing code if existing already
		public async Task<string> Setup(long userId)
		{
			var existing = await GetTwoFactorSecret(userId);
			if (existing.HasValue)
			{
				return existing.Value.Secret;
			}

			var key = KeyGeneration.GenerateRandomKey(20);
			var B32 = Base32Encoding.ToString(key);
			
			var updated = await db.ExecuteAsync(
				"UPDATE user_email SET email = :secret, updated_at = NOW() WHERE user_id = :user_id",
				new { user_id = userId, secret = B32 });

			if (updated == 0)
			{
				await db.ExecuteAsync(
					"INSERT INTO user_email (user_id, email, status, created_at, updated_at) VALUES (:user_id, :secret, 0, NOW(), NOW())",
					new { user_id = userId, secret = B32 });
			}

			return B32;
		}

        public async Task<bool> IsEnabled(long userId)
        {
            var exists = await db.ExecuteScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM user_email WHERE user_id = :user_id AND status = :status)",
                new { user_id = userId, status = TwoFactorStatusCode });
            return exists;
        }

		public async Task MarkEnabled(long userId)
		{
			await db.ExecuteAsync(
				"UPDATE user_email SET status = :status, updated_at = NOW() WHERE user_id = :user_id",
				new { user_id = userId, status = TwoFactorStatusCode });
		}

        public async Task Disable2FA(long userId)
        {
            await db.ExecuteAsync(
                "DELETE FROM user_email WHERE user_id = :user_id AND status = :status",
                new { user_id = userId, status = TwoFactorStatusCode });
        }

		// This ufkcing SUCKS
		public async Task<bool> VerifyCode(long userId, string code)
		{
			var SecretDB = await GetTwoFactorSecret(userId);
			if (!SecretDB.HasValue)
			{
				Console.WriteLine($"no 2FA TOTP found for {userId}");
				return false;
			}

			var secret = SecretDB.Value.Secret;
			secret = secret.ToUpper().Replace(" ", "").Trim();

			try
			{
				var Bytes = Base32Encoding.ToBytes(secret);
				var totp = new Totp(Bytes);

				DateTime verTime = GetAccurateTime();
				var verWindow = new VerificationWindow(1, 1);
				bool result = totp.VerifyTotp(verTime.ToUniversalTime(), code, out long matchedtimestep, verWindow);
				Console.WriteLine($"2fa result: {result}, matched: {matchedtimestep}");

				return result;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"error in TOTP verification for {userId}: {ex}");
				return false;
			}
		}

		// goddamn ntp clients FUCK.
		private static TimeSpan? NtpOffset = null;
        private static DateTime LastNtp;

        private static DateTime GetAccurateTime()
        {
            if (NtpOffset == null || (DateTime.UtcNow - LastNtp).TotalMinutes > 15)
            {
                try
                {
                    var networkTime = NtpClient.GetNetworkTime();
                    NtpOffset = networkTime.ToUniversalTime() - DateTime.UtcNow;
                    LastNtp = DateTime.UtcNow;
                }
                catch
                {
                    NtpOffset = TimeSpan.Zero;
                }
            }

            return DateTime.UtcNow + (NtpOffset ?? TimeSpan.Zero);
        }

        private static class NtpClient
        {
            private const string NtpServer = "pool.ntp.org";
            private const int NtpPort = 123;

            public static DateTime GetNetworkTime()
            {
                var ntpData = new byte[48];
                ntpData[0] = 0x1B;

                var addresses = Dns.GetHostEntry(NtpServer).AddressList;
                var IP = new IPEndPoint(addresses[0], NtpPort);

                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect(IP);
                    socket.Send(ntpData);
                    socket.ReceiveTimeout = 3000;
                    socket.Receive(ntpData);
                    socket.Close();
                }

                const byte serverReplyTime = 40;

                ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
                ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

                intPart = SwapEndianness(intPart);
                fractPart = SwapEndianness(fractPart);

                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

                var networkDateTime = new DateTime(1900, 1, 1).AddMilliseconds((long)milliseconds);

                return networkDateTime.ToLocalTime();
            }

            private static uint SwapEndianness(ulong x)
            {
                return (uint)(((x & 0x000000ff) << 24) +
                              ((x & 0x0000ff00) << 8) +
                              ((x & 0x00ff0000) >> 8) +
                              ((x & 0xff000000) >> 24));
            }
        }
        public bool IsThreadSafe() => true;
        public bool IsReusable() => false;
    }
}
