using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Exceptions;
using Roblox.Logging;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Website.Middleware;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using MVC = Microsoft.AspNetCore.Mvc;
using Roblox.Website.WebsiteModels.Games;
using Roblox.Services.Exceptions;
using Roblox.Dto.Games;
using Roblox.Models.GameServer;
using Roblox.Dto.Assets;
using Roblox.Models.Assets;
using Roblox.Services;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class Game2014Testing : ControllerBase 
    {	
		[HttpGetBypass("/game/host2014")]
		public async Task<MVC.IActionResult> Host2014()
		{
			try
			{
				var port = Request.Query["port"].FirstOrDefault();
				var PlaceID = Request.Query["placeId"].FirstOrDefault();
				
				if (string.IsNullOrEmpty(port))
				{
					return StatusCode(400, "Port is required");
				}
				
				if (string.IsNullOrEmpty(PlaceID) || !long.TryParse(PlaceID, out long placeId))
				{
					return StatusCode(400, "Valid placeId required");
				}
				
/* 				var PlaceDetails = await services.assets.GetAssetCatalogInfo(placeId);
				if (PlaceDetails == null || PlaceDetails.assetType != Roblox.Models.Assets.Type.Place)
				{
					return StatusCode(404, "Place not found");
				} */

				var Script = $@"-- Start Game Script Arguments

------------------- UTILITY FUNCTIONS --------------------------

local cdnSuccess = 0
local cdnFailure = 0

function waitForChild(parent, childName)
	while true do
		local child = parent:findFirstChild(childName)
		if child then
			return child
		end
		parent.ChildAdded:wait()
	end
end

-- returns the player object that killed this humanoid
-- returns nil if the killer is no longer in the game
function getKillerOfHumanoidIfStillInGame(humanoid)

	-- check for kill tag on humanoid - may be more than one - todo: deal with this
	local tag = humanoid:findFirstChild(""creator"")

	-- find player with name on tag
	if tag then
		local killer = tag.Value
		if killer.Parent then -- killer still in game
			return killer
		end
	end

	return nil
end
-----------------------------------END UTILITY FUNCTIONS -------------------------

-----------------------------------""CUSTOM"" SHARED CODE----------------------------------

pcall(function() settings().Network.UseInstancePacketCache = true end)
pcall(function() settings().Network.UsePhysicsPacketCache = true end)
pcall(function() settings()[""Task Scheduler""].PriorityMethod = Enum.PriorityMethod.AccumulatedError end)


settings().Network.PhysicsSend = Enum.PhysicsSendMethod.TopNErrors
settings().Network.ExperimentalPhysicsEnabled = true
settings().Network.WaitingForCharacterLogRate = 100
pcall(function() settings().Diagnostics:LegacyScriptMode() end)

-----------------------------------START GAME SHARED SCRIPT------------------------------

-- establish this peer as the Server
local ns = game:GetService(""NetworkServer"")

local badgeUrlFlagExists, badgeUrlFlagValue = pcall(function () return settings():GetFFlag(""NewBadgeServiceUrlEnabled"") end)
local newBadgeUrlEnabled = badgeUrlFlagExists and badgeUrlFlagValue

local url = ""{Configuration.BaseUrl}""
-- make this not use this in the future very secure
local apiKey = ""AckGU""
local placeId = ""{PlaceID}""
local access = ""apiKey="" .. apiKey

pcall(function() game:GetService(""Players""):SetAbuseReportUrl(url .. ""/AbuseReport/InGameChatHandler.ashx"") end)
pcall(function() game:GetService(""ScriptInformationProvider""):SetAssetUrl(url .. ""/Asset/"") end)
pcall(function() game:GetService(""ContentProvider""):SetBaseUrl(url .. ""/"") end)
pcall(function() game:GetService(""Players""):SetChatFilterUrl(url .. ""/Game/ChatFilter.ashx"") end)

game:GetService(""BadgeService""):SetPlaceId({placeId})
game:SetPlaceId({placeId})

if newBadgeUrlEnabled then
	game:GetService(""BadgeService""):SetAwardBadgeUrl(url .. ""/assets/award-badge?userId=%d&badgeId=%d&placeId=%d&"" .. access)
end

if access~=nil then
	if not newBadgeUrlEnabled then
		game:GetService(""BadgeService""):SetAwardBadgeUrl(url .. ""/Game/Badge/AwardBadge.ashx?UserID=%d&BadgeID=%d&PlaceID=%d&"" .. access)
	end

	game:GetService(""BadgeService""):SetHasBadgeUrl(url .. ""/Game/Badge/HasBadge.ashx?UserID=%d&BadgeID=%d&"" .. access)
	game:GetService(""BadgeService""):SetIsBadgeDisabledUrl(url .. ""/Game/Badge/IsBadgeDisabled.ashx?BadgeID=%d&PlaceID=%d&"" .. access)

	game:GetService(""FriendService""):SetMakeFriendUrl(url .. ""/Game/CreateFriend?firstUserId=%d&secondUserId=%d"")
	game:GetService(""FriendService""):SetBreakFriendUrl(url .. ""/Game/BreakFriend?firstUserId=%d&secondUserId=%d"")
	game:GetService(""FriendService""):SetGetFriendsUrl(url .. ""/Game/AreFriends?userId=%d"")
end

game:GetService(""BadgeService""):SetIsBadgeLegalUrl("""")
game:GetService(""InsertService""):SetBaseSetsUrl(url .. ""/Game/Tools/InsertAsset.ashx?nsets=10&type=base"")
game:GetService(""InsertService""):SetUserSetsUrl(url .. ""/Game/Tools/InsertAsset.ashx?nsets=20&type=user&userid=%d"")
game:GetService(""InsertService""):SetCollectionUrl(url .. ""/Game/Tools/InsertAsset.ashx?sid=%d"")
game:GetService(""InsertService""):SetAssetUrl(url .. ""/Asset/?id=%d"")
game:GetService(""InsertService""):SetAssetVersionUrl(url .. ""/Asset/?assetversionid=%d"")

pcall(function() loadfile(url .. ""/Game/LoadPlaceInfo.ashx?PlaceId="" .. {placeId})() end)
	
pcall(function() 
	if access then
		loadfile(url .. ""/Game/PlaceSpecificScript.ashx?PlaceId="" .. {placeId} .. ""&"" .. access)()
	end
end)

pcall(function() game:GetService(""NetworkServer""):SetIsPlayerAuthenticationRequired(true) end)
settings().Diagnostics.LuaRamLimit = 0

-- listen for the death of a Player
function createDeathMonitor(player)
	-- we don't need to clean up old monitors or connections since the Character will be destroyed soon
	if player.Character then
		local humanoid = waitForChild(player.Character, ""Humanoid"")
		humanoid.Died:connect(
			function ()
				-- Ahh
			end
		)
	end
end

-- listen to all Players' Characters
game:GetService(""Players"").ChildAdded:connect(
	function (player)
		createDeathMonitor(player)
		player.Changed:connect(
			function (property)
				if property==""Character"" then
					createDeathMonitor(player)
				end
			end
		)
	end
)

game:GetService(""Players"").PlayerAdded:connect(function(player)
	print(""Player "" .. player.userId .. "" added"")
	if url and access and {placeId} and player and player.userId then
		game:HttpGet(url .. ""/Game/ClientPresence.ashx?action=connect&"" .. access .. ""&PlaceID="" .. {placeId} .. ""&UserID="" .. player.userId)
		game:HttpPost(url .. ""/Game/PlaceVisit.ashx?UserID="" .. player.userId .. ""&AssociatedPlaceID="" .. {placeId} .. ""&"" .. access, """")
	end
end)

game:GetService(""Players"").PlayerRemoving:connect(function(player)
	print(""Player "" .. player.userId .. "" leaving"")
	if url and access and {placeId} and player and player.userId then
		game:HttpGet(url .. ""/Game/ClientPresence.ashx?action=disconnect&"" .. access .. ""&PlaceID="" .. {placeId} .. ""&UserID="" .. player.userId)
	end
end)

-- Now start the connection
game:Load(url .. ""/Asset/?id="" .. placeId .. ""&apiKey="" .. apiKey)
ns:Start({port}, 1/60)  
pcall(function() game.LocalSaveEnabled = true end)

-- StartGame --
Game:GetService(""RunService""):Run()";

				var RSA = services.rsaSign;
				var signature = RSA.SignScript(Script, false);
				var result = $"{signature}\r\n{Script}";
				
				return Content(result, "text/plain");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"2014 join error: {ex}");
				return StatusCode(500, "Failed to generate game script");
			}
		}
		
		[HttpGetBypass("/game/studio.ashx")]
		public async Task<MVC.IActionResult> StudioAshx()
		{
			try
			{
				var Script = @"print(""hello"")
repeat wait() until game:FindFirstChild(""NetworkServer"")
game:SetMessage(""Hosting!"")";

				var RSA = services.rsaSign;
				var signature = RSA.SignScript(Script, false);
				var result = $"{signature}\r\n{Script}";
				
				return Content(result, "text/plain");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Studio.ashx error: {ex}");
				return StatusCode(500, "Failed to generate studio script");
			}
		}
		
		[HttpGetBypass("/game/join2014")]
		public async Task<MVC.IActionResult> Join2014()
		{
			try
			{
				var port = Request.Query["port"].FirstOrDefault();
				var PlaceID = Request.Query["placeId"].FirstOrDefault();
				
				if (string.IsNullOrEmpty(port))
				{
					return StatusCode(400, "Port is required");
				}
				
				if (string.IsNullOrEmpty(PlaceID) || !long.TryParse(PlaceID, out long placeId))
				{
					return StatusCode(400, "Valid PlaceID required");
				}

				var Script = $@"--This is a joinscript that works in 2013 and back, etc.

-- functions --------------------------
function onPlayerAdded(player)
	-- override
end

pcall(function() game:SetPlaceID(-1, false) end)

local startTime = tick()
local connectResolved = false
local loadResolved = false
local joinResolved = false
local playResolved = true
local playStartTime = 0

local cdnSuccess = 0
local cdnFailure = 0

settings()[""Game Options""].CollisionSoundEnabled = true
pcall(function() settings().Rendering.EnableFRM = true end)
pcall(function() settings().Physics.Is30FpsThrottleEnabled = false end)
pcall(function() settings()[""Task Scheduler""].PriorityMethod = Enum.PriorityMethod.AccumulatedError end)
pcall(function() settings().Physics.PhysicsEnvironmentalThrottle = Enum.EnviromentalPhysicsThrottle.DefaultAuto end)

local threadSleepTime = ...

if threadSleepTime==nil then
	threadSleepTime = 15
end

local test = true

local closeConnection = game.Close:connect(function() 
	if 0 then
		if not connectResolved then
			local duration = tick() - startTime;
		elseif (not loadResolved) or (not joinResolved) then
			local duration = tick() - startTime;
			if not loadResolved then
				loadResolved = true
			end
			if not joinResolved then
				joinResolved = true
			end
		elseif not playResolved then
			playResolved = true
		end
	end
end)

game:GetService(""ChangeHistoryService""):SetEnabled(false)
game:GetService(""ContentProvider""):SetThreadPool(16)
game:GetService(""InsertService""):SetBaseSetsUrl(""{Configuration.BaseUrl}/Game/Tools/InsertAsset.ashx?nsets=10&type=base"")
game:GetService(""InsertService""):SetUserSetsUrl(""{Configuration.BaseUrl}/Game/Tools/InsertAsset.ashx?nsets=20&type=user&userid=%d"")
game:GetService(""InsertService""):SetCollectionUrl(""{Configuration.BaseUrl}/Game/Tools/InsertAsset.ashx?sid=%d"")
game:GetService(""InsertService""):SetAssetUrl(""{Configuration.BaseUrl}/asset/?id=%d"")
game:GetService(""ContentProvider""):SetAssetUrl(""{Configuration.BaseUrl}/"")
game:GetService(""InsertService""):SetAssetVersionUrl(""{Configuration.BaseUrl}/Asset/?assetversionid=%d"")

pcall(function() game:GetService(""SocialService""):SetFriendUrl(""{Configuration.BaseUrl}/Game/LuaWebService/HandleSocialRequest.ashx?method=IsFriendsWith&playerid=%d&userid=%d"") end)
pcall(function() game:GetService(""SocialService""):SetBestFriendUrl(""{Configuration.BaseUrl}/Game/LuaWebService/HandleSocialRequest.ashx?method=IsBestFriendsWith&playerid=%d&userid=%d"") end)
pcall(function() game:GetService(""SocialService""):SetGroupUrl(""{Configuration.BaseUrl}/Game/LuaWebService/HandleSocialRequest.ashx?method=IsInGroup&playerid=%d&groupid=%d"") end)
pcall(function() game:GetService(""SocialService""):SetGroupRankUrl(""{Configuration.BaseUrl}/Game/LuaWebService/HandleSocialRequest.ashx?method=GetGroupRank&playerid=%d&groupid=%d"") end)
pcall(function() game:GetService(""SocialService""):SetGroupRoleUrl(""{Configuration.BaseUrl}/Game/LuaWebService/HandleSocialRequest.ashx?method=GetGroupRole&playerid=%d&groupid=%d"") end)
pcall(function() game:GetService(""GamePassService""):SetPlayerHasPassUrl(""{Configuration.BaseUrl}/Game/GamePass/GamePassHandler.ashx?Action=HasPass&UserID=%d&PassID=%d"") end)
pcall(function() game:GetService(""MarketplaceService""):SetProductInfoUrl(""{Configuration.BaseUrl}/marketplace/productinfo?assetId=%d"") end)
pcall(function() game:GetService(""MarketplaceService""):SetPlayerOwnsAssetUrl(""{Configuration.BaseUrl}/ownership/hasasset?userId=%d&assetId=%d"") end)
pcall(function() game:SetCreatorID(0, Enum.CreatorType.User) end)

pcall(function() game:GetService(""Players""):SetChatStyle(Enum.ChatStyle.ClassicAndBubble) end)
pcall(function() game:GetService(""ScriptContext""):AddCoreScript(1,game:GetService(""ScriptContext""),""StarterScript"") end)

local waitingForCharacter = false

pcall( function()
	if settings().Network.MtuOverride == 0 then
	  settings().Network.MtuOverride = 1400
	end
end)


client = game:GetService(""NetworkClient"")
visit = game:GetService(""Visit"")

function setMessage(message)
	-- todo: animated ""...""
	if not false then
		game:SetMessage(message)
	else
		-- hack, good enought for now
		game:SetMessage(""Teleporting ..."")
	end
end

function showErrorWindow(message, errorType, errorCategory)
	game:SetMessage(message)
end

-- called when the client connection closes
function onDisconnection(peer, lostConnection)
	if lostConnection then
		showErrorWindow(""You have lost connection"", ""LostConnection"", ""LostConnection"")
	else
		showErrorWindow(""This game has been shutdown"", ""Kick"", ""Kick"")
	end
end

function requestCharacter(replicator)
	
	-- prepare code for when the Character appears
	local connection
	connection = player.Changed:connect(function (property)
		if property==""Character"" then
			game:ClearMessage()
			waitingForCharacter = false
			
			connection:disconnect()
		
			if 0 then
				if not joinResolved then
					local duration = tick() - startTime;
					joinResolved = true
					
					playStartTime = tick()
					playResolved = false
				end
			end
		end
	end)
	
	setMessage(""Requesting character"")
	
	local success, err = pcall(function()	
		replicator:RequestCharacter()
		setMessage(""Waiting for character"")
		waitingForCharacter = true
	end)
end

function onConnectionAccepted(url, replicator)
	connectResolved = true

	local waitingForMarker = true
	
	local success, err = pcall(function()	
		if not test then 
		    visit:SetPing("""", 300) 
		end
		
		if not false then
			game:SetMessageBrickCount()
		else
			setMessage(""Teleporting ..."")
		end

		replicator.Disconnection:connect(onDisconnection)
		
		local marker = replicator:SendMarker()
		
		marker.Received:connect(function()
			waitingForMarker = false
			requestCharacter(replicator)
		end)
	end)
	
	if not success then
		return
	end
	
	while waitingForMarker do
		workspace:ZoomToExtents()
		wait(0.5)
	end
end

-- called when the client connection fails
function onConnectionFailed(_, error)
	showErrorWindow(""Failed to connect to the Game. (ID="" .. error .. "")"", ""ID"" .. error, ""Other"")
end

-- called when the client connection is rejected
function onConnectionRejected()
	connectionFailed:disconnect()
	showErrorWindow(""This game is not available. Please try another"", ""WrongVersion"", ""WrongVersion"")
end

pcall(function() settings().Diagnostics:LegacyScriptMode() end)
local success, err = pcall(function()	

	game:SetRemoteBuildMode(true)
	
	setMessage(""Connecting to Server"")
	client.ConnectionAccepted:connect(onConnectionAccepted)
	client.ConnectionRejected:connect(onConnectionRejected)
	connectionFailed = client.ConnectionFailed:connect(onConnectionFailed)
	client.Ticket = """"
	
	playerConnectSucces, player = pcall(function() return client:PlayerConnect({placeId}, ""127.0.0.1"", {port}, 0, threadSleepTime) end)

	player:SetSuperSafeChat(false)
	pcall(function() player:SetUnder13(false) end)
	pcall(function() player:SetMembershipType(Enum.MembershipType.None) end)
	pcall(function() player:SetAccountAge(365) end)
	--player.Idled:connect(onPlayerIdled)
	
	-- Overriden
	onPlayerAdded(player)
	
	player.CharacterAppearance = ""{Configuration.BaseUrl}/Asset/CharacterFetch.ashx?userId=1&placeId=1""	
	if not test then visit:SetUploadUrl("""")end
        player.Name = ""Player""
		
end)

pcall(function() game:SetScreenshotInfo("""") end)";

				var RSA = services.rsaSign;
				var signature = RSA.SignScript(Script, false);
				var result = $"{signature}\r\n{Script}";
				
				return Content(result, "text/plain");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"2014 join error: {ex}");
				return StatusCode(500, "Failed to generate game script");
			}
		}
	}
}	