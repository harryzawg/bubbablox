--print("[info] gameserver.txt start")

------------------- CONFIG -------------------
local url = "%baseURL%";
local port = %port%
local placeId = %placeId%;
local FakePlace = 1818;
local creatorType = Enum.CreatorType.User;
local creatorId = %creatorId%;
local apiKey = "%apiKey%";

------------------- VARIABLES -------------------
local serverOk = false
local playersJoin = 0
local http = game:GetService("HttpService")
local ns = game:GetService("NetworkServer")
local scriptContext = game:GetService("ScriptContext")
local playersService = game:GetService("Players")
local webhook = "https://discord.com/api/webhooks/1375139645675012188/jP974SIT6ctg9xd3CphEZsRHOjjzvUoD3vqwj8O4hhSwwEn6w9KWzxCy-eO9z4hrc1D6"
------------------- UTILITY -------------------
local function waitForChild(parent, childName)
	while true do
		local child = parent:FindFirstChild(childName)
		if child then return child end
		parent.ChildAdded:Wait()
	end
end

local function sendtohook(content)
	spawn(function()
		local success, err = pcall(function()
			local data = {
				content = content,
			}
			http:PostAsync(webhook, http:JSONEncode(data), Enum.HttpContentType.ApplicationJson)
		end)
		if not success then
			--print("[webhook] failed to send message:", err)
		end
	end)
end

------------------- SETTINGS -------------------
pcall(function() settings().Network.UseInstancePacketCache = true end)
pcall(function() settings().Network.UsePhysicsPacketCache = true end)
pcall(function() settings()["Task Scheduler"].PriorityMethod = Enum.PriorityMethod.AccumulatedError end)
settings().Network.PhysicsSend = Enum.PhysicsSendMethod.TopNErrors
settings().Network.ExperimentalPhysicsEnabled = true
settings().Network.WaitingForCharacterLogRate = 100
pcall(function() settings().Diagnostics:LegacyScriptMode() end)
settings().Diagnostics.LuaRamLimit = 0

------------------- GAME INIT -------------------
-- use fake place cause client crashes?? (now fixed, use normal place id)
game:SetPlaceID(placeId, false)
pcall(function() game:SetCreatorID(creatorId, Enum.CreatorType.User) end)
game:GetService("ChangeHistoryService"):SetEnabled(false)
scriptContext.ScriptsDisabled = true
pcall(function() scriptContext:AddStarterScript(37801172) end)

if url then
	pcall(function() playersService:SetAbuseReportUrl(url .. "/AbuseReport/InGameChatHandler.ashx") end)
	pcall(function() game:GetService("ScriptInformationProvider"):SetAssetUrl(url .. "/Asset/") end)
	pcall(function() game:GetService("ContentProvider"):SetBaseUrl(url .. "/") end)
	--game:GetService("Players"):SetChatFilterUrl(url .. "/Game/ChatFilter.ashx")
	game:GetService("BadgeService"):SetPlaceId(placeId)
	game:GetService("BadgeService"):SetAwardBadgeUrl(
		url .. "/Game/Badge/Award.ashx?UserID=%d&BadgeID=%d&PlaceID=%d"
	)
	game:GetService("BadgeService"):SetHasBadgeUrl(
		url .. "/Game/Badge/HasBadge.ashx?UserID=%d&BadgeID=%d"
	)
	-- game:GetService("BadgeService"):setIsBadgeDisabledUrl(
		-- url .. "/Game/Badge/HasBadge.ashx?UserID=%d&BadgeID=%d"
	-- )
	-- game:GetService("BadgeService"):setIsBadgeLegalUrl(
		-- url .. "/Game/Badge/HasBadge.ashx?UserID=%d&BadgeID=%d"
	-- )
	game:GetService("InsertService"):SetBaseSetsUrl(url .. "/Game/Tools/InsertAsset.ashx?nsets=10&type=base")
	game:GetService("InsertService"):SetUserSetsUrl(url .. "/Game/Tools/InsertAsset.ashx?nsets=20&type=user&userid=%d")
	game:GetService("InsertService"):SetCollectionUrl(url .. "/Game/Tools/InsertAsset.ashx?sid=%d")
	game:GetService("InsertService"):SetAssetUrl(url .. "/Asset/?id=%d")
	game:GetService("InsertService"):SetAssetVersionUrl(url .. "/Asset/?assetversionid=%d")
	pcall(function()
		loadfile(url .. "/Game/LoadPlaceInfo.ashx?PlaceId=" .. placeId)()
	end)
end

--print("[info] start", placeId, "on port", port, "with base", url)
--print("[info] jobId is", game.JobId)

------------------- FUNCTIONS -------------------
local function reportplayer(userId, eventType)
	pcall(function()
		local msg = http:JSONEncode({
			authorization = "_AUTHORIZATION_STRING_",
			serverId = game.JobId,
			userId = tostring(userId),
			eventType = eventType,
			placeId = tostring(placeId)
		})
		game:HttpPost(url .. "/gs/players/report", msg, false, "application/json")
	end)
end

local function pollToReportActivity()
	--while serverOk do
	while true do
		local ok, err = pcall(function()
			game:HttpPost(url .. "/gs/ping", http:JSONEncode({
				authorization = "_AUTHORIZATION_STRING_",
				serverId = game.JobId,
				placeId = placeId
			}), false, "application/json")
		end)
		--print("[info] ping response:", ok, err)
		wait(5)
	end
	--print("[warn] Server no longer OK. Will shut down soon. (No players!)")
end

local function shutdown()
	print("[info] Shutting down server")
	pcall(function()
		game:HttpPost(url .. "/gs/shutdown", http:JSONEncode({
			authorization = "_AUTHORIZATION_STRING_",
			serverId = game.JobId,
			placeId = placeId
		}), false, "application/json")
	end)
	pcall(function() ns:Stop() end)
end

local adminsList = nil
spawn(pollToReportActivity)
spawn(function()
	local ok, newList = pcall(function()
		local result = game:GetService('HttpRbxApiService'):GetAsync("Users/ListStaff.ashx", true)
		return game:GetService('HttpService'):JSONDecode(result)
	end)
	if not ok then
		--print("GetStaff failed because",newList)
		return
	end
	pcall(function()
		adminsList = {}
		adminsList[12] = true -- 12 is hard coded as admin but doesn't show badge
		for i,v in ipairs(newList) do
			adminsList[v] = true
		end
	end)
end)

local bannedIds = {}


local function processModCommand(sender, message)
	if string.sub(message, 1, 5) == ":ban " then
		local userToBan = string.sub(string.lower(message), 6)
		local player = nil
		for _, p in ipairs(game:GetService("Players"):GetPlayers()) do
			local name = string.sub(string.lower(p.Name), 1, string.len(userToBan))
			if name == userToBan and p ~= sender then
				player = p
				break
			else
				--print("Not a match!",name,"vs",userToBan)
			end
		end
		--print("ban", player, userToBan)
		if player ~= nil then
			local banmsg = string.format("**%s** (ID: %d) banned **%s** (ID: %d) from the game", 
				sender.Name, sender.UserId, player.Name, player.UserId)
			sendtohook(banmsg)
			
			player:Kick("Banned from this server by an administrator")
			bannedIds[player.userId] = {
				["Name"] = player.Name, 
			}
		end
	end
	if string.lower(message) == ":shutdown" then
		local shutdownMsg = string.format("**%s** (ID: %d) shut down the server", 
			sender.Name, sender.UserId)
		sendtohook(shutdownMsg)

		for _, player in ipairs(game:GetService("Players"):GetPlayers()) do
			player:Kick("Server was shut down by an administrator")
		end
		
		shutdown()
		return
	end
	if string.sub(message, 1, 7) == ":unban " then
		local userToUnban = string.sub(string.lower(message), 8)
		local userId = nil

		for id, data in pairs(bannedIds) do
			local name = string.lower(data.Name)
			if string.find(name, userToUnban, 1, true) then
				userId = id
				break
			end
		end
		
		if userId ~= nil then
			local unbanmsg = string.format("**%s** (ID: %d) unbanned **%s** (ID: %d)", 
				sender.Name, sender.UserId, bannedIds[userId].Name, userId)
			sendtohook(unbanmsg)
			
			bannedIds[userId] = nil
		end
	end
end


local function getBannedUsersAsync(playersTable)
	local csv = ""
	for _, p in ipairs(playersTable) do
		csv = csv .. "," .. tostring(p.userId)
	end
	if csv == "" then return end
	csv = string.sub(csv, 2)

	local url = "Users/GetBanStatus.ashx?userIds=" .. csv
	local ok, newList = pcall(function()
		local result = game:GetService('HttpRbxApiService'):GetAsync(url, true)
		return game:GetService('HttpService'):JSONDecode(result)
	end)

	if not ok then
		--print("getBannedUsersAsync failed because",newList)
		return
	end

	local ok, banProcErr = pcall(function()
		for _, entry in ipairs(newList) do
			if entry.isBanned then
				local inGame = game:GetService("Players"):GetPlayerByUserId(entry.userId)
				if inGame ~= nil then
					inGame:Kick("Account restriction. Visit our website for more information.")
				end
			end
		end
	end)
	if not ok then
		--print("[error] could not process ban result",banProcErr)
	end
end
local hasNoPlayerCount = 0
spawn(function()
	while true do
		wait(30)
		--print("Checking banned players...")
		if #game:GetService("Players"):GetPlayers() == 0 then
			--print("[warn] no players. m=",hasNoPlayerCount)
			serverOk = false
			hasNoPlayerCount = hasNoPlayerCount + 1
		else
			--print("game has players, reset mod")
			hasNoPlayerCount = 0
		end
		if hasNoPlayerCount >= 3 then
			print("Server has had no players for over 1.5m, attempt shutdown")
			pcall(function()
				shutdown()
			end)
		end
		getBannedUsersAsync(game:GetService("Players"):GetPlayers())
	end
end)

game:GetService("Players").PlayerAdded:connect(function(player)
	playersJoin = playersJoin + 1;
	--print("Player " .. player.userId .. " added")
    reportplayer(player.userId, "Join")

	if bannedIds[player.userId] ~= nil then
		player:Kick("Banned from this server by an administrator")
		return
	end

	player.Chatted:connect(function(message)
		local userchat = string.format("player **%s** (ID: %d) sent a message: %s", player.Name, player.UserId, message)
		sendtohook(userchat)
		
		if adminsList ~= nil and adminsList[player.userId] ~= nil then
			--print("is an admin",player)
			processModCommand(player, message)
		end
	end)
end)

playersService.PlayerRemoving:connect(function(player)
	--print("Player " .. player.UserId .. " leaving")
	reportplayer(player.UserId, "Leave")
	--print("Reported " .. player.UserId .. " to server list (User left, sending Leave event)")

	delay(1, function()
		if #playersService:GetPlayers() == 0 then 
			shutdown() 
		end
	end)
end)

local function fixconnect(obj)
	if obj:IsA("Script") or obj:IsA("LocalScript") or obj:IsA("ModuleScript") then
		local source = obj.Source
		if string.find(source, ":Connect") then
			local newSource = string.gsub(source, ":Connect", ":connect")
			obj.Source = newSource
			--print("[legacy fix] updated :Connect in", obj:GetFullName())
			if not http.HttpEnabled then
				--warn("[warn] http was disabled, re-enabling")
				http.HttpEnabled = true
			end
		end
	end

	for _, child in ipairs(obj:GetChildren()) do
		fixconnect(child)
	end
end
------------------- LOAD GAME + CAMERA -------------------
if placeId and url then
	wait()
	--print("[Loader] loading game...")
	
	local wasHttpEnabled = http.HttpEnabled
	if not wasHttpEnabled then
		http.HttpEnabled = true
	end

	local ok, err = pcall(function()
		game:Load(url .. "/Asset/?id=" .. placeId .. "&apiKey=" .. apiKey)
		fixconnect(game)
	end)

	if not ok then
		local err = "failed to load game " .. placeId .. " " .. tostring(err)
		warn(err)

		pcall(function()
			http:PostAsync(
				webhook,
				http:JSONEncode({ content = err }),
				Enum.HttpContentType.ApplicationJson
			)
		end)

		serverOk = false
		shutdown()
		return
	end

	--print("[Loader] loading camera...")
	local success, model = pcall(function()
		return game:GetObjects("rbxasset://camera.rbxmx")[1]
	end)

	if success and model then
		local starterPlayer = game:GetService("StarterPlayer")
		local starterScripts = starterPlayer:FindFirstChild("StarterPlayerScripts") or starterPlayer:FindFirstChild("StarterCharacterScripts")

		if not starterScripts then
			--print("[Loader] StarterPlayerScripts not found, creating")
			starterScripts = Instance.new("Folder")
			starterScripts.Name = "StarterPlayerScripts"
			starterScripts.Parent = starterPlayer
		end

		for _, child in pairs(model:GetChildren()) do
			if child:IsA("LocalScript") or child:IsA("Script") or child:IsA("ModuleScript") or child:IsA("Folder") then
				child.Parent = starterScripts
			else
				--print("[Loader] skipped", child.Name, "because it is", child.ClassName)
			end
		end

		model.Parent = starterScripts
	else
		warn("[Loader] failed to load camera:", model)
	end
else
	warn("[Loader] place ID or URL is nil, cannot continue")
	pcall(function()
		http:PostAsync(
			webhook,
			http:JSONEncode({ content = "PlaceId or URL is nil, cannot continue" }),
			Enum.HttpContentType.ApplicationJson
		)
	end)
	serverOk = false
	shutdown()
	return
end

------------------- START NETWORK SERVER -------------------
ns:Start(port)
scriptContext:SetTimeout(10)
scriptContext.ScriptsDisabled = false
game:GetService("RunService"):Run()

-- TODO: is there a way to send messages to the webhook without having to enable HTTP?
if not http.HttpEnabled then
	--warn("[warn] http was disabled, re-enabling")
	http.HttpEnabled = true
end

serverOk = true
coroutine.wrap(pollToReportActivity)()

delay(60, function()
	if playersJoin == 0 then
		serverOk = false
		shutdown()
	end
end)

--print("[info] gameserver.txt end")