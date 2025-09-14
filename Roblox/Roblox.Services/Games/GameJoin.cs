using System.Dynamic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Roblox.Dto.Games;
using Roblox.Dto.Users;
using Roblox.Models.Assets;
using Roblox.Models.Users;
using Roblox.Dto.Assets;
using Roblox.Models.GameServer;

namespace Roblox.Services
{
	public class GameJoinService : ServiceBase
	{
		public AssetsService Assets { get; }
		public UsersService Users { get; }
		public GamesService Games { get; }
		public AvatarService Avatar { get; }
		public GameServerService GameServer { get; }
		public RSASignService RSA { get; }

		public GameJoinService(
			AssetsService AssetsService,
			UsersService UsersService,
			GamesService GamesService,
			AvatarService AvatarService,
			GameServerService GameServerService,
			RSASignService RSASignService)
		{
			Assets = AssetsService;
			Users = UsersService;
			Games = GamesService;
			Avatar = AvatarService;
			GameServer = GameServerService;
			RSA = RSASignService;
		}

        public async Task<dynamic> GenerateJoinScript(long userId, long placeId, string jobId, int serverPort, string Ticket, string Year = "2016")
        {
            var UserInfo = await Users.GetUserById(userId);
            var PlaceDetails = await Assets.GetAssetCatalogInfo(placeId);
            var UniID = await Games.GetUniverseId(placeId);
            
            int AccountAge = (int)(DateTime.UtcNow - UserInfo.created).TotalDays;
            var membership = await Users.GetUserMembership(userId);
            
			string MembershipType = "None";
			if (membership != null)
			{
				MembershipType = membership.membershipType switch
				{
					Roblox.Models.Users.MembershipType.OutrageousBuildersClub => "OutrageousBuildersClub",
					Roblox.Models.Users.MembershipType.TurboBuildersClub => "TurboBuildersClub",
					Roblox.Models.Users.MembershipType.BuildersClub => "BuildersClub",
					Roblox.Models.Users.MembershipType.None => "None",
					_ => "None"
				};
			}
			
            string Creator;
            if (PlaceDetails.creatorType == CreatorType.User)
            {
                var CreatorUsername = await Users.GetUserById(PlaceDetails.creatorTargetId);
                Creator = CreatorUsername?.username ?? "Unknown";
            }
            else
            {
                Creator = PlaceDetails.creatorName ?? "Unknown";
            }

			switch (Year)
			{
				case "2020":
					return await Generate2020JoinScript(UserInfo, PlaceDetails, UniID, jobId, serverPort, MembershipType, AccountAge, Creator, Ticket);
				case "2018":
				case "2017":
					return await Generate2017JoinScript(UserInfo, PlaceDetails, UniID, jobId, serverPort, MembershipType, AccountAge, Creator, Ticket);
				case "2015":
				case "2016":
				default:
					return await Generate2015JoinScript(UserInfo, PlaceDetails, UniID, jobId, serverPort, MembershipType, AccountAge, Creator, Ticket);
			}
        }

        private Task<dynamic> Generate2020JoinScript(UserInfo UserInfo, Roblox.Dto.Assets.MultiGetEntry PlaceDetails, long UniID, string jobId, int serverPort, string MembershipType, int accountAge, string Creator, string Ticket)
        {
            var currentDateTime = DateTime.Now;
            
            return Task.FromResult<dynamic>(new
            {
                ClientPort = 0,
                MachineAddress = "games.zawg.ca",
                ServerPort = serverPort,
                DirectServerReturn = true,
                PingUrl = "",
                PingInterval = 0,
                UserName = UserInfo.username,
                DisplayName = UserInfo.username,
                SeleniumTestMode = false,
                UserId = UserInfo.userId,
                RobloxLocale = "en_us",
                GameLocale = "en_us#RobloxTranslateAbTest2",
                SuperSafeChat = false,
                CharacterAppearance = $"{Configuration.BaseUrl}/v1.1/avatar-fetch/?placeId={PlaceDetails.id}&userId={UserInfo.userId}",
                ClientTicket = RSA.GenerateClientTicket2020(UserInfo, MembershipType, accountAge, jobId, PlaceDetails.id),
                NewClientTicket = RSA.GenerateClientTicket2020(UserInfo, MembershipType, accountAge, jobId, PlaceDetails.id),
                GameChatType = "AllUsers",
                GameId = jobId,
                PlaceId = PlaceDetails.id,
                WaitingForCharacterGuid = "a3cc25b0-099b-4066-be70-e915de21e3d3",
                BaseUrl = Configuration.BaseUrl + "/",
                ChatStyle = "ClassicAndBubble",
                VendorId = "0",
                ScreenShotInfo = "",
				VideoInfo = "<?xml version=\"1.0\"?><entry xmlns=\"http://www.w3.org/2005/Atom\" xmlns:media=\"http://search.yahoo.com/mrss/\" xmlns:yt=\"http://gdata.youtube.com/schemas/2007\"><media:group><media:title type=\"plain\"><![CDATA[ROBLOX Place]]></media:title><media:description type=\"plain\"><![CDATA[ For more games visit http://www.roblox.com]]></media:description><media:category scheme=\"http://gdata.youtube.com/schemas/2007/categories.cat\">Games</media:category><media:keywords>ROBLOX, video, free game, online virtual world</media:keywords></media:group></entry>",
                CreatorId = PlaceDetails.creatorTargetId,
                CreatorTypeEnum = PlaceDetails.creatorType.ToString(),
                MembershipType = MembershipType,
                AccountAge = accountAge,
                CookieStoreFirstTimePlayKey = "rbx_evt_ftp",
                CookieStoreFiveMinutePlayKey = "rbx_evt_fmp",
                CookieStoreEnabled = true,
                IsRobloxPlace = PlaceDetails.creatorTargetId == 1,
                IsUnknownOrUnder13 = false,
                SessionId = $"a3cc25b0-099b-4066-be70-e915de21e3d3|{jobId}|0|127.0.0.1|8|{currentDateTime:MM/dd/yyyy HH:mm:ss}|0|null|{Ticket}|null|null|null",
                DataCenterId = 0,
                UniID = UniID,
                BrowserTrackerId = 0,
                UsePortraitMode = false,
                FollowUserId = 0,
                characterAppearanceId = UserInfo.userId,
                CountryCode = "US"
            });
        }

        private Task<dynamic> Generate2017JoinScript(UserInfo UserInfo, Roblox.Dto.Assets.MultiGetEntry PlaceDetails, long UniID, string jobId, int serverPort, string MembershipType, int accountAge, string Creator, string Ticket)
        {
            return Task.FromResult<dynamic>(new
            {
                ClientPort = 0,
                MachineAddress = "games.zawg.ca",
                ServerPort = serverPort,
                PingUrl = "",
                PingInterval = 0,
                UserName = UserInfo.username,
                SeleniumTestMode = false,
                UserId = UserInfo.userId,
                SuperSafeChat = false,
                CharacterAppearance = $"{Configuration.BaseUrl}/v1.1/avatar-fetch?placeId={PlaceDetails.id}&userId={UserInfo.userId}",
                PlaceId = PlaceDetails.id,
                MeasurementUrl = "",
                WaitingForCharacterGuid = "26eb3e21-aa80-475b-a777-b43c3ea5f7d2",
                BaseUrl = Configuration.BaseUrl + "/",
                ChatStyle = "ClassicAndBubble",
                VendorId = 0,
                ScreenShotInfo = "",
                VideoInfo = "<?xml version=\"1.0\"?><entry xmlns=\"http://www.w3.org/2005/Atom\" xmlns:media=\"http://search.yahoo.com/mrss/\" xmlns:yt=\"http://gdata.youtube.com/schemas/2007\"><media:group><media:title type=\"plain\"><![CDATA[ROBLOX Place]]></media:title><media:description type=\"plain\"><![CDATA[ For more games visit http://www.roblox.com]]></media:description><media:category scheme=\"http://gdata.youtube.com/schemas/2007/categories.cat\">Games</media:category><media:keywords>ROBLOX, video, free game, online virtual world</media:keywords></media:group></entry>",
                CreatorId = PlaceDetails.creatorTargetId,
                CreatorTypeEnum = PlaceDetails.creatorType.ToString(),
                MembershipType = MembershipType,
                AccountAge = accountAge,
                CookieStoreFirstTimePlayKey = "rbx_evt_ftp",
                CookieStoreFiveMinutePlayKey = "rbx_evt_fmp",
                CookieStoreEnabled = false,
                IsRobloxPlace = PlaceDetails.creatorTargetId == 1,
                GenerateTeleportJoin = false,
                IsUnknownOrUnder13 = false,
                SessionId = $"a3cc25b0-099b-4066-be70-e915de21e3d3|{jobId}|{UserInfo.userId}|127.0.0.1|8|{DateTime.Now:ddMMyy}|0|null|{Ticket}|null|null|null",
                DataCenterId = 0,
                FollowUserId = 0,
                CharacterAppearanceId = 4,
                UniID = UniID
            });
        }

        private Task<dynamic> Generate2015JoinScript(UserInfo UserInfo, Roblox.Dto.Assets.MultiGetEntry PlaceDetails, long UniID, string jobId, int serverPort, string MembershipType, int accountAge, string Creator, string Ticket)
        {
            return Task.FromResult<dynamic>(new
            {
                ClientPort = 0,
                MachineAddress = "games.zawg.ca",
                ServerPort = serverPort,
                PingUrl = "",
                PingInterval = 0,
                UserName = UserInfo.username,
                SeleniumTestMode = false,
                UserId = UserInfo.userId,
                SuperSafeChat = false,
                CharacterAppearance = $"{Configuration.BaseUrl}/Asset/CharacterFetch.ashx?userId={UserInfo.userId}&placeId={PlaceDetails.id}",
                ClientTicket = "",
                GameId = PlaceDetails.id,
                PlaceId = PlaceDetails.id,
                MeasurementUrl = "",
                WaitingForCharacterGuid = "26eb3e21-aa80-475b-a777-b43c3ea5f7d2",
                BaseUrl = Configuration.BaseUrl + "/",
                ChatStyle = "ClassicAndBubble",
                VendorId = "0",
                ScreenShotInfo = "",
				VideoInfo = "<?xml version=\"1.0\"?><entry xmlns=\"http://www.w3.org/2005/Atom\" xmlns:media=\"http://search.yahoo.com/mrss/\" xmlns:yt=\"http://gdata.youtube.com/schemas/2007\"><media:group><media:title type=\"plain\"><![CDATA[ROBLOX Place]]></media:title><media:description type=\"plain\"><![CDATA[ For more games visit http://www.roblox.com]]></media:description><media:category scheme=\"http://gdata.youtube.com/schemas/2007/categories.cat\">Games</media:category><media:keywords>ROBLOX, video, free game, online virtual world</media:keywords></media:group></entry>",
                CreatorId = PlaceDetails.creatorTargetId,
                CreatorTypeEnum = PlaceDetails.creatorType.ToString(),
                MembershipType = MembershipType,
                AccountAge = accountAge,
                CookieStoreFirstTimePlayKey = "rbx_evt_ftp",
                CookieStoreFiveMinutePlayKey = "rbx_evt_fmp",
                CookieStoreEnabled = true,
                IsRobloxPlace = PlaceDetails.creatorTargetId == 1,
                GenerateTeleportJoin = false,
                IsUnknownOrUnder13 = false,
                SessionId = $"a3cc25b0-099b-4066-be70-e915de21e3d3|{jobId}|0|127.0.0.1|8|{DateTime.Now:ddMMyy}|0|null|{Ticket}|null|null|null",
                DataCenterId = jobId,
                UniID = UniID,
                BrowserTrackerId = 0,
                UsePortraitMode = false,
                FollowUserId = 0,
                characterAppearanceId = 1
            });
        }
    }
}