-- Simple Animation Thumbnail Generator (2018 style)
-- Loads R15 rig from local asset, plays animation from structured asset, captures thumbnail

-- Arguments passed in by job
baseUrl, fileExtension, x, y, animationUrl, jobId, assetId = ...

-- Services
local HttpService = game:GetService("HttpService")
local HttpRbxApiService = game:GetService("HttpRbxApiService")
local ScriptContext = game:GetService("ScriptContext")
local ContentProvider = game:GetService("ContentProvider")
local Players = game:GetService("Players")
local ThumbnailGenerator = game:GetService("ThumbnailGenerator")
local InsertService = game:GetService("InsertService")

-- Configure environment
pcall(function() ContentProvider:SetBaseUrl(baseUrl) end)
ScriptContext.ScriptsDisabled = true
HttpService.HttpEnabled = true
game:GetService("StarterGui"):SetCoreGuiEnabled(Enum.CoreGuiType.All, false)
ThumbnailGenerator.GraphicsMode = 2

-- Debug print helper
local function debugLog(msg)
    print("[DEBUG] " .. tostring(msg))
end

debugLog("Starting thumbnail generation process")

----------------------------------------------------------
-- Step 1: Load R15 rig
----------------------------------------------------------
debugLog("Loading R15 character from local asset...")
local player = Players:CreateLocalPlayer(0)

local r15Character = InsertService:LoadLocalAsset("rbxasset://avatar/characterR15.rbxm")
if r15Character then
    if player.Character then
        player.Character:Destroy()
    end
    r15Character.Parent = workspace
    player.Character = r15Character
    wait(1)
    debugLog("R15 character assigned to player")
else
    debugLog("ERROR: Could not load characterR15.rbxm, falling back to default")
    player:LoadCharacterBlocking()
end

local humanoid = player.Character and player.Character:FindFirstChildOfClass("Humanoid")
if humanoid then
    debugLog("Humanoid rig type: " .. tostring(humanoid.RigType))
else
    debugLog("ERROR: No humanoid found!")
end

----------------------------------------------------------
-- Step 2: Extract actual AnimationId from structured asset
----------------------------------------------------------
local function getActualAnimationId(assetUrl)
    debugLog("Processing structured animation asset: " .. tostring(assetUrl))

    -- Extract numeric ID
    local id = assetUrl:match("id=(%d+)") or assetUrl:match("%D(%d+)$")
    if not id then
        debugLog("ERROR: Could not extract ID from url")
        return nil
    end

    local success, asset = pcall(function()
        return InsertService:LoadAsset(tonumber(id))
    end)
    if not success or not asset then
        debugLog("ERROR: Failed to load asset " .. tostring(id))
        return nil
    end

    local r15Anim = asset:FindFirstChild("R15Anim")
    if not r15Anim then
        debugLog("ERROR: No R15Anim folder found")
        asset:Destroy()
        return nil
    end

    local jumpVal = r15Anim:FindFirstChild("jump")
    if not jumpVal then
        debugLog("ERROR: No 'jump' StringValue found")
        asset:Destroy()
        return nil
    end

    local animObj = jumpVal:FindFirstChildOfClass("Animation")
    if not animObj then
        debugLog("ERROR: No Animation inside 'jump'")
        asset:Destroy()
        return nil
    end

    local animId = animObj.AnimationId
    debugLog("Found actual animationId: " .. tostring(animId))
    asset:Destroy()
    return animId
end

----------------------------------------------------------
-- Step 3: Play the animation
----------------------------------------------------------
local actualAnimId = getActualAnimationId(animationUrl)
if not actualAnimId then
    debugLog("Falling back to default animation")
    actualAnimId = "rbxassetid://507771019" -- dance emote
end

debugLog("Using animationId: " .. tostring(actualAnimId))

if humanoid then
    local anim = Instance.new("Animation")
    anim.AnimationId = "rbxassetid://10921137402"  -- temporary animation

    local track = humanoid:LoadAnimation(anim)
    track.Priority = Enum.AnimationPriority.Action
    track:Play()

    wait(1)  -- wait so the animation visibly plays
    track.TimePosition = track.Length * 0.4  -- jump ahead 40% into animation
end
----------------------------------------------------------
-- Step 4: Capture thumbnail
----------------------------------------------------------
debugLog("Capturing thumbnail...")
local thumbnailData = ThumbnailGenerator:Click(fileExtension, x, y, true, true)
debugLog("Thumbnail captured successfully")

-- Post back
local payload = {
    type = "Asset",
    assetId = assetId,
    thumbnail = thumbnailData,
    jobId = jobId
}
local json = HttpService:JSONEncode(payload)

local ok, result = pcall(function()
    return HttpRbxApiService:PostAsync("/api/thumbnail", json)
end)

if ok then
    debugLog("Post success to /api/thumbnail: " .. tostring(result))
else
    debugLog("Post failed: " .. tostring(result))
end

debugLog("Thumbnail generation completed")
print(thumbnailData)
