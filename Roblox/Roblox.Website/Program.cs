using System.Diagnostics;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.Http.Json;
using Roblox.Rendering;
using Roblox.Website.Middleware;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;
using Roblox;
using Roblox.Libraries.RemoteView;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Website.Hubs;
using Roblox.Website.WebsiteModels;
using Npgsql;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
ServicePointManager.ServerCertificateValidationCallback += 
	(sender, cert, chain, sslPolicyErrors) => true;

var domain = AppDomain.CurrentDomain;
// Set a timeout interval of 5 seconds.
domain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromSeconds(5));

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var builder = WebApplication.CreateBuilder(args);

// DB
Roblox.Services.Database.Configure(configuration.GetSection("Postgres").Value);
Roblox.Services.Cache.Configure(configuration.GetSection("Redis").Value);
builder.Services.AddTransient<NpgsqlConnection>(provider => 
{
    var connection = new NpgsqlConnection(configuration.GetSection("Postgres").Value);
    //connection.Open();
    return connection;
});
#if RELEASE
// Influx DB
//Roblox.Metrics.RobloxInfluxDb.Configure(configuration.GetSection("InfluxDB:Website:BaseUrl").Value, configuration.GetSection("InfluxDB:Website:Authorization").Value);
#endif
// Config
Roblox.Configuration.CdnBaseUrl = configuration.GetSection("CdnBaseUrl").Value;
Roblox.Configuration.AssetDirectory = configuration.GetSection("Directories:Asset").Value;
Roblox.Configuration.StorageDirectory = configuration.GetSection("Directories:Storage").Value;
Roblox.Configuration.ThumbnailsDirectory = configuration.GetSection("Directories:Thumbnails").Value;
Roblox.Configuration.GroupIconsDirectory = configuration.GetSection("Directories:GroupIcons").Value;
Roblox.Configuration.PublicDirectory = configuration.GetSection("Directories:Public").Value;
Roblox.Configuration.XmlTemplatesDirectory = configuration.GetSection("Directories:XmlTemplates").Value;
Roblox.Configuration.JsonDataDirectory = configuration.GetSection("Directories:JsonData").Value;
Roblox.Configuration.AdminBundleDirectory = configuration.GetSection("Directories:AdminBundle").Value;
Roblox.Configuration.EconomyChatBundleDirectory = configuration.GetSection("Directories:EconomyChatBundle").Value;
Roblox.Configuration.RccServicePath = configuration.GetSection("Directories:RccServicePath").Value;
Roblox.Configuration.RccService2015Path = configuration.GetSection("Directories:RccService2015Path").Value;
Roblox.Configuration.RccService2017Path = configuration.GetSection("Directories:RccService2017Path").Value;
Roblox.Configuration.RccService2018Path = configuration.GetSection("Directories:RccService2018Path").Value;
Roblox.Configuration.RccService2020Path = configuration.GetSection("Directories:RccService2020Path").Value;
Roblox.Configuration.LuaScriptPath = configuration.GetSection("Directories:LuaScripts").Value;
Roblox.Configuration.BaseUrl = configuration.GetSection("BaseUrl").Value;
Roblox.Configuration.AssetUrl = configuration.GetSection("AssetUrl").Value;
Roblox.Configuration.GSIPAddress = configuration.GetSection("GSIPAddress").Value;
Roblox.Configuration.Webhook = configuration.GetSection("Webhook").Value;
Roblox.Configuration.SignupWebhook = configuration.GetSection("SignupWebhook").Value;
Roblox.Configuration.DiscordClientID = configuration.GetSection("DiscordClientID").Value;
Roblox.Configuration.DiscordClientSecret = configuration.GetSection("DiscordClientSecret").Value;
Roblox.Configuration.DiscordRedirect = configuration.GetSection("DiscordRedirect").Value;
Roblox.Configuration.DiscordForgotPasswordRedirect = configuration.GetSection("DiscordForgotPasswordRedirect").Value;
Roblox.Configuration.DiscordLoginRedirect = configuration.GetSection("DiscordLoginRedirect").Value;
Roblox.Configuration.DiscordKey = configuration.GetSection("DiscordKey").Value;
Roblox.Configuration.IPSalt = configuration.GetSection("IPSalt").Value;
Roblox.Configuration.IPHubApiKey = configuration.GetSection("IPHubApiKey").Value;
Roblox.Configuration.BubbamonsApiKey = configuration.GetSection("BubbamonsApiKey").Value;
Roblox.Configuration.HCaptchaPublicKey = configuration.GetSection("HCaptcha:Public").Value;
Roblox.Configuration.HCaptchaPrivateKey = configuration.GetSection("HCaptcha:Private").Value;
Roblox.Configuration.AllowedNetworkPorts = configuration.GetSection("GameServer:AllowedNetworkPorts").GetChildren().Select(c => int.Parse(c.Value));
Roblox.Configuration.GameServerAuthorization = configuration.GetSection("GameServerAuthorization").Value;
Roblox.Configuration.RenderAuthorization = configuration.GetSection("Render:Authorization").Value;
Roblox.Configuration.BotAuthorization = configuration.GetSection("BotAuthorization").Value;
// game-server config stuff
IConfiguration gameServerConfig = new ConfigurationBuilder().AddJsonFile("game-servers.json").Build();
Roblox.Configuration.GameServerIpAddresses = gameServerConfig.GetSection("GameServers").Get<IEnumerable<GameServerConfigEntry>>();
Roblox.Configuration.RccAuthorization = configuration.GetSection("RccAuthorization").Value;
Roblox.Configuration.AllowedQuietGetJson = configuration.GetSection("AllowedQuietGetJson").GetChildren().Select(c => c.Value);
Roblox.Configuration.AssetValidationServiceUrl =
    configuration.GetSection("AssetValidation:BaseUrl").Value;
Roblox.Configuration.AssetValidationServiceAuthorization =
    configuration.GetSection("AssetValidation:Authorization").Value;
Roblox.Services.GameServerService.Configure(string.Join(Guid.NewGuid().ToString(), new int [16].Select(_ => Guid.NewGuid().ToString()))); // More TODO: If we every load balance, this will break
// Package Clothing
Roblox.Configuration.PackageShirtAssetId = long.Parse(configuration.GetSection("PackageShirtAssetId").Value);
Roblox.Configuration.PackagePantsAssetId = long.Parse(configuration.GetSection("PackagePantsAssetId").Value);
Roblox.Configuration.PackageLeftArmAssetId = long.Parse(configuration.GetSection("PackageLeftArmAssetId").Value);
Roblox.Configuration.PackageRightArmAssetId = long.Parse(configuration.GetSection("PackageRightArmAssetId").Value);
Roblox.Configuration.PackageLeftLegAssetId = long.Parse(configuration.GetSection("PackageLeftLegAssetId").Value);
Roblox.Configuration.PackageRightLegAssetId = long.Parse(configuration.GetSection("PackageRightLegAssetId").Value);
Roblox.Configuration.PackageTorsoAssetId = long.Parse(configuration.GetSection("PackageTorsoAssetId").Value);

// Sign up asset ids
Roblox.Configuration.SignupAssetIdsMan = configuration.GetSection("SignupAssetIdsMan").GetChildren().Select(c => long.Parse(c.Value));
Roblox.Configuration.SignupAssetIdsFemale = configuration.GetSection("SignupAssetIdsFemale").GetChildren().Select(c => long.Parse(c.Value));
Roblox.Configuration.SignupAvatarAssetIdsMan = configuration.GetSection("SignupAvatarAssetIdsMan").GetChildren().Select(c => long.Parse(c.Value));
Roblox.Configuration.SignupAvatarAssetIdsFemale = configuration.GetSection("SignupAvatarAssetIdsFemale").GetChildren().Select(c => long.Parse(c.Value));
#if DEBUG
Roblox.Configuration.RobloxAppPrefix = "bbclient://";
#endif
FeatureFlags.StartUpdateFlagTask();
Roblox.Website.Filters.StaffFilter.Configure(long.Parse(configuration.GetSection("OwnerUserId").Value));
// Roblox.Website.Controllers.ThumbnailsControllerV1.StartThumbnailFixLoop();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddScoped<AssetsService>();
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.PropertyNamingPolicy = null;
});
builder.Services.AddSignalR();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SchemaGeneratorOptions.SchemaIdSelector = type => type.ToString();
    c.OperationFilter<SwaggerFileOperationFilter>();
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 1000;
    options.Limits.MaxConcurrentUpgradedConnections = 1000;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHttpClient("FrontendProxy", client =>
{
    client.BaseAddress = new Uri("http://localhost:3000");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.ConnectionClose = false;
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false,
    MaxConnectionsPerServer = 100,
    UseProxy = false,
    UseCookies = false
})
.SetHandlerLifetime(Timeout.InfiniteTimeSpan);
builder.Services.AddMemoryCache();
builder.Logging.ClearProviders();
builder.Logging.AddConsole(); 
builder.Logging.AddDebug();
var app = builder.Build();

// god this sucks
app.Use(async (ctx, next) =>
{
    var Original = ctx.Request.Path.Value ?? "";
    
    var Fixed = System.Text.RegularExpressions.Regex.Replace(
        Original, 
        @"/{2,}", 
        "/"
    );
    
    if (Fixed != Original)
    {
        //Console.WriteLine($"fixing double slashes in path: {Original} -> {Fixed}");
        ctx.Request.Path = Fixed;
    }
    
    await next();
});

app.UseRouting();

var prepareResponseForCache = (StaticFileResponseContext ctx) =>
{
    const int durationInSeconds = 86400 * 365;
    ctx.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=" + durationInSeconds;
    ctx.Context.Response.Headers.Remove(HeaderNames.LastModified);
};

var datafolder = new PhysicalFileProvider(Roblox.Configuration.PublicDirectory + "Data");

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Roblox.Configuration.PublicDirectory + "UnsecuredContent"),
    RequestPath = "/UnsecuredContent",
    OnPrepareResponse = prepareResponseForCache,
});

app.Use(async (context, next) =>
{
    var path = (context.Request.Path.Value ?? "").Trim();
    var userAgent = context.Request.Headers["User-Agent"].ToString();
    /* Console.WriteLine($"Path: {path}, User-Agent: {userAgent}, HasCookie: {context.Request.Cookies.ContainsKey(".ROBLOSECURITY")}"); */

    if (!path.StartsWith("/"))
    {
        path = "/" + path;
    }

    path = path.TrimEnd('/');

    if (string.IsNullOrEmpty(path) || path == "/")
    {
        if (context.Request.Cookies.TryGetValue(".ROBLOSECURITY", out var Cookie) && 
            !string.IsNullOrEmpty(Cookie))
        {
            context.Response.Redirect("/home", permanent: false);
            return;
        }
    }

    await next();
});

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = datafolder,
    RequestPath = "",
    DefaultFileNames = new List<string> { "index.html" }
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = datafolder,
    RequestPath = "",
    OnPrepareResponse = prepareResponseForCache
});

System.Net.ServicePointManager.DefaultConnectionLimit = 100;

// CdnBaseUrl is empty on dev servers
if (string.IsNullOrWhiteSpace(Roblox.Configuration.CdnBaseUrl))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Roblox.Configuration.ThumbnailsDirectory),
        RequestPath = "/images/thumbnails",
        OnPrepareResponse = prepareResponseForCache,
    });
	
	app.MapGet("/images/{id}.png", async (string id, HttpContext context) => 
	{
		var filePath = Path.Combine(Roblox.Configuration.AssetDirectory, id);
		
		if (!File.Exists(filePath))
		{
			context.Response.StatusCode = 404;
			return;
		}

		bool isimage;
		try
		{
			await using var fileStream = File.OpenRead(filePath);
			using var reader = new BinaryReader(fileStream);
			
			var header = reader.ReadBytes(8);
			
			// not really sure fully if it only uses PNG, but just in case add the other bytes
			// i know this is a really retarded way to do this, but i could not fix the thumbnail texture generation for the life of me and it works better imo so
			isimage = header.Length >= 8 && (
				// PNG
				(header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 && 
				 header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A) ||
				
				// JPEG
				(header[0] == 0xFF && header[1] == 0xD8) ||
				
				// GIF
				(header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && 
				 header[3] == 0x38 && header[4] == 0x39 && header[5] == 0x61) ||
				
				// GIF
				(header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && 
				 header[3] == 0x38 && header[4] == 0x37 && header[5] == 0x61) ||
				
				// WEBP
				(header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
				 reader.ReadBytes(4) is { Length: 4 } webpHeader &&
				 webpHeader[0] == 0x57 && webpHeader[1] == 0x45 && webpHeader[2] == 0x42 && webpHeader[3] == 0x50)
			);
		}
		catch
		{
			isimage = false;
		}

		if (!isimage)
		{
			context.Response.StatusCode = 400;
			// fake image message to throw people off
			await context.Response.WriteAsync("Could not find image");
			return;
		}
		
		context.Response.Headers.CacheControl = "public,max-age=31536000";
		context.Response.Headers.Remove(HeaderNames.LastModified);
		context.Response.ContentType = "image/png";
		
		await context.Response.SendFileAsync(filePath);
	});

	app.Use(async (context, next) =>
	{
		if (context.Request.Path.StartsWithSegments("/images") && 
			!context.Request.Path.Value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
		{
			context.Response.StatusCode = 400;
			await context.Response.WriteAsync("Could not find image");
			return;
		}
		await next();
	});

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Roblox.Configuration.GroupIconsDirectory),
        RequestPath = "/images/groups",
        OnPrepareResponse = prepareResponseForCache,
    });
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Roblox.Configuration.PublicDirectory + "img/"),
    RequestPath = "/img",
    OnPrepareResponse = prepareResponseForCache,
});

#if FALSE
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Roblox.Configuration.EconomyChatBundleDirectory),
    RequestPath = "/chat",
    ServeUnknownFileTypes = false,
    OnPrepareResponse = prepareResponseForCache,
});
#endif

app.UseRobloxSessionMiddleware();
app.UseRobloxPlayerCorsMiddleware(); // cors varies depending on authentication status, so it must be after session middleware

app.UseRobloxCsrfMiddleware();
app.UseApplicationGuardMiddleware();
Roblox.Website.Middleware.ApplicationGuardMiddleware.Configure(configuration.GetSection("Authorization").Value);
Roblox.Website.Middleware.CsrfMiddleware.Configure(Guid.NewGuid().ToString() + Guid.NewGuid().ToString() + Guid.NewGuid().ToString()); // TODO: This would break if we ever load balance

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<FrontendProxyMiddleware>();
app.UseRobloxLoggingMiddleware();

app.UseExceptionHandler("/error");
// await CommandHandler.Configure("ws://localhost:3189", "hello world of deving 1234");
CommandHandler.Configure(configuration.GetSection("Render:BaseUrl").Value, configuration.GetSection("Render:Authorization").Value);

SessionMiddleware.Configure(configuration.GetSection("Jwt:Sessions").Value);
app.UseTimerMiddleware(); // Must always be last

Task.Run(async () =>
{
    using var db = new NpgsqlConnection(configuration.GetSection("Postgres").Value);
    await db.OpenAsync();
    await using var cmd = new NpgsqlCommand("DELETE FROM asset_server_player;", db);
    await cmd.ExecuteNonQueryAsync();
	await using var cmd2 = new NpgsqlCommand("DELETE FROM asset_server;", db);
    await cmd2.ExecuteNonQueryAsync();
    
    await Task.Delay(TimeSpan.FromSeconds(5));
    using var assets = Roblox.Services.ServiceProvider.GetOrCreate<AssetsService>();
    await assets.FixAssetImagesWithoutMetadata();
});

app.UseEndpoints(e =>
{
    e.MapHub<ChatHub>("/chat");
    e.MapControllers();
    e.MapRazorPages();
});

app.Run();
