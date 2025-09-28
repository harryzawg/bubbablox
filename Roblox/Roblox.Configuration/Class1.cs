// ReSharper disable InconsistentNaming
#pragma warning disable CS8618
namespace Roblox;

public class GameServerConfigEntry
{
    public string ip { get; set; }
    public string domain { get; set; }
    public int maxServerCount { get; set; }
}

public static class Configuration
{
    public static string CdnBaseUrl { get; set; }
    public static string StorageDirectory { get; set; }
    public static string AssetDirectory { get; set; }
    public static string PublicDirectory { get; set; }
    public static string ThumbnailsDirectory { get; set; }
    public static string GroupIconsDirectory { get; set; }
    public static string XmlTemplatesDirectory { get; set; }
    public static string JsonDataDirectory { get; set; }
    public static string AdminBundleDirectory { get; set; }
    public static string EconomyChatBundleDirectory { get; set; }
    public static string BaseUrl { get; set; }
    public static string AssetUrl { get; set; }   
	public static string GSIPAddress { get; set; }
	public static string Webhook { get; set; }
	public static string SignupWebhook { get; set; }
	public static string DiscordClientID { get; set; }
	public static string DiscordClientSecret { get; set; }
	public static string DiscordRedirect { get; set; }
	public static string DiscordForgotPasswordRedirect { get; set; }
	public static string DiscordLoginRedirect { get; set; }	
	public static string DiscordKey { get; set; }
	public static string IPSalt { get; set; }
	public static string IPHubApiKey { get; set; }
	public static string BubbamonsApiKey { get; set; }
    public static string HCaptchaPublicKey { get; set; }
    public static string HCaptchaPrivateKey { get; set; }
    public static IEnumerable<GameServerConfigEntry> GameServerIpAddresses { get; set; }
	public static IEnumerable<int> AllowedNetworkPorts { get; set; } = Array.Empty<int>();
    public static string GameServerAuthorization { get; set; }
    public static string RobloxAppPrefix { get; set; } = "bbclient://";
    public static string AssetValidationServiceUrl { get; set; }
    public static string AssetValidationServiceAuthorization { get; set; }
    public static string BotAuthorization { get; set; }
	public static string RenderAuthorization { get; set; }
    public static string RccAuthorization { get; set; }
	public static string RccServicePath { get; set; }
	public static string RccService2015Path { get; set; }
	public static string RccService2017Path { get; set; }
	public static string RccService2018Path { get; set; }
	public static string RccService2020Path { get; set; }
	public static string LuaScriptPath { get; set; }
	public static IEnumerable<string> AllowedQuietGetJson { get; set; } = Array.Empty<string>();
    public const string UserAgentBypassSecret = "503534DA-F2F8-4681-9B37-15EE9EAE88DC4D0FAE23-F672-4BC6-8D5F-E35A2939680DB1980985-AF9C-4B2E-B19E-67005FBAD27B";
    public static long PackageShirtAssetId { get; set; }
    public static long PackagePantsAssetId { get; set; }
    public static long PackageLeftArmAssetId { get; set; }
    public static long PackageRightArmAssetId { get; set; }
    public static long PackageLeftLegAssetId { get; set; }
    public static long PackageRightLegAssetId { get; set; }
    public static long PackageTorsoAssetId { get; set; }
    private static IEnumerable<long>? _SignupAssetIdsMan { get; set; }
    private static IEnumerable<long>? _SignupAssetIdsFemale { get; set; }
    private static IEnumerable<long>? _SignupAvatarAssetIdsMan { get; set; }
    private static IEnumerable<long>? _SignupAvatarAssetIdsFemale { get; set; }

    public static IEnumerable<long> SignupAssetIdsMan
    {
        get => _SignupAssetIdsMan ?? ArraySegment<long>.Empty;
        set
        {
            if (_SignupAssetIdsMan != null)
                throw new Exception("Cannot set startup asset ids - they are not null.");
            _SignupAssetIdsMan = value;
        }
    }

    public static IEnumerable<long> SignupAssetIdsFemale
    {
        get => _SignupAssetIdsFemale ?? ArraySegment<long>.Empty;
        set
        {
            if (_SignupAssetIdsFemale != null)
                throw new Exception("Cannot set startup asset ids - they are not null.");
            _SignupAssetIdsFemale = value;
        }
    }

    public static IEnumerable<long> SignupAvatarAssetIdsMan
    {
        get => _SignupAvatarAssetIdsMan ?? ArraySegment<long>.Empty;
        set
        {
            if (_SignupAvatarAssetIdsMan != null)
                throw new Exception("Cannot set signup avatar asset ids, they are not null");
            _SignupAvatarAssetIdsMan = value;
        }
    }

    public static IEnumerable<long> SignupAvatarAssetIdsFemale
    {
        get => _SignupAvatarAssetIdsFemale ?? ArraySegment<long>.Empty;
        set
        {
            if (_SignupAvatarAssetIdsFemale != null)
                throw new Exception("Cannot set signup avatar asset ids, they are not null");
            _SignupAvatarAssetIdsFemale = value;
        }
    }

    public static string GameServerDomain => "bt.zawg.ca"; // set to your game server's domain
}