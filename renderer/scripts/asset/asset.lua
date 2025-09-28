-- ignore the clothing handling being here, when i first setup the site it was using this script so i made it work cause i was stupid
local assetId = {1234}
local jobId = "InsertJobIdHere"
local mode = "R6"
local baseURL = "http://localhost"
local uploadURL = "UPLOAD_URL_HERE"
local ScriptContext = game:GetService("ScriptContext")
local Lighting = game:GetService("Lighting")
local RunService = game:GetService("RunService")
local ContentProvider = game:GetService("ContentProvider")
local HttpService = game:GetService("HttpService")
local ThumbnailGenerator = game:GetService("ThumbnailGenerator")
local Players = game:GetService("Players")
local InsertService = game:GetService("InsertService")

print("asset was called")

game:GetService("StarterGui"):SetCoreGuiEnabled(Enum.CoreGuiType.All, false)
game:GetService("ThumbnailGenerator").GraphicsMode = 2
HttpService.HttpEnabled = true
ScriptContext.ScriptsDisabled = true
Lighting.Outlines = false
ContentProvider:SetBaseUrl("http://localhost")

local function makechar()
    print("[DEBUG] creating R6 char")
    local player = Players:CreateLocalPlayer(1)
	-- is this necessary?
    player.CharacterAppearance = "http://localhost/asset/?id=27111419"
    player:LoadCharacter()
    local character = player.Character or player.CharacterAdded:Wait()
    character.Parent = workspace
    return character
end

local function clothing(character)
    print("[DEBUG] applying clothing")

    local hasClothing = false

    for _, id in pairs(assetId) do
        local success, asset = pcall(function()
            return InsertService:LoadAsset(id)
        end)

        if success and asset then
            for _, item in pairs(asset:GetChildren()) do
                if item:IsA("Shirt") or item:IsA("Pants") or item:IsA("Shirt Graphic") or item:IsA("ShirtGraphic") then
                    print("[DEBUG] adding clothing:", item.Name)
                    item.Parent = character
                    hasClothing = true
                elseif item:IsA("CharacterMesh") then
                    print("[DEBUG] adding CharacterMesh:", item.Name)
                    item.Parent = character
                    hasClothing = true
                end
            end
        else
            warn("[DEBUG] failed to load:", id)
        end
    end

    -- if it has clothing because we're rendering the character make the body white so you can see the clothing better
    if hasClothing then
        print("[DEBUG] clothing detected, setting body colors to white")

        local bodyColors = character:FindFirstChild("Body Colors")
        if bodyColors then
            bodyColors.HeadColor = BrickColor.new("White")
            bodyColors.TorsoColor = BrickColor.new("White")
            bodyColors.LeftArmColor = BrickColor.new("White")
            bodyColors.RightArmColor = BrickColor.new("White")
            bodyColors.LeftLegColor = BrickColor.new("White")
            bodyColors.RightLegColor = BrickColor.new("White")
        else
            warn("[DEBUG] no body colours found, creating them")

            local newBodyColors = Instance.new("BodyColors")
            newBodyColors.HeadColor = BrickColor.new("White")
            newBodyColors.TorsoColor = BrickColor.new("White")
            newBodyColors.LeftArmColor = BrickColor.new("White")
            newBodyColors.RightArmColor = BrickColor.new("White")
            newBodyColors.LeftLegColor = BrickColor.new("White")
            newBodyColors.RightLegColor = BrickColor.new("White")
            newBodyColors.Parent = character
        end
    end
end

local function sendthumb(character)
    print("[DEBUG] getting char thumb")
	
	local camera = Instance.new("Camera")
	workspace.CurrentCamera = camera
    
    -- render twice, fixes some issues have no clue why
    ThumbnailGenerator:Click('png', 1, 1, true, false)
    
    local encoded = ThumbnailGenerator:Click("png", 512, 512, true, true)
    
    print("[DEBUG] sending thumb")
    local ok, data = pcall(function()
        return HttpService:PostAsync(uploadURL, HttpService:JSONEncode({
            ['type'] = 'Character',
            ['assetId'] = assetId,
            ['thumbnail'] = encoded,
            ['accessKey'] = "AccessKey",
            ['jobId'] = jobId,
        }), Enum.HttpContentType.TextPlain)
    end)
    
    if ok then
        print("[DEBUG] thumb success:", data)
    else
        warn("[DEBUG] thumb failed:", data)
    end
end

local function containsClothing()
    for _, id in pairs(assetId) do
        local success, asset = pcall(function()
            return InsertService:LoadAsset(id)
        end)

        if success and asset then
            for _, item in pairs(asset:GetChildren()) do
                if item:IsA("Shirt") or item:IsA("Pants") or item:IsA("ShirtGraphic") or 
                   item:IsA("Shirt Graphic") or item:IsA("CharacterMesh") then
                    print("[DEBUG] found character item:", item.Name)
                    return true
                end
            end
        else
            warn("[DEBUG] Failed to load asset:", id)
        end
    end
    return false
end

local function render()
    print("[DEBUG] rendering thumb")
    local model = Instance.new("Model")
    
    for _, id in pairs(assetId) do
        local success, asset = pcall(function()
            return InsertService:LoadAsset(id)
        end)
        
        if success and asset then
            for _, item in pairs(asset:GetChildren()) do
                print("[DEBUG] parenting:", item.Name)
                item.Parent = model
            end
        else
            warn("[DEBUG] failed to load:", id)
        end
    end
    
    model.Parent = workspace
	local thumbcamera = model:FindFirstChild("ThumbnailCamera", true)

	local camera = Instance.new("Camera")
	if thumbcamera and thumbcamera:IsA("Camera") then
		print("[DEBUG] got ThumbnailCamera, copying properties")
		
		camera.CFrame = thumbcamera.CFrame
		camera.CoordinateFrame = thumbcamera.CoordinateFrame
		camera.Focus = thumbcamera.Focus
		camera.FieldOfView = thumbcamera.FieldOfView
		camera.CameraType = thumbcamera.CameraType
	else
		print("[DEBUG] no ThumbnailCamera found, using default position")
	end
	workspace.CurrentCamera = camera
	camera.Parent = model
    
    local encoded = ThumbnailGenerator:Click("png", 512, 512, true, true)
    print("[DEBUG] sending thumbnail")
    
    local ok, data = pcall(function()
        return HttpService:PostAsync(uploadURL, HttpService:JSONEncode({
            ['type'] = 'Asset',
            ['assetId'] = assetId,
            ['thumbnail'] = encoded,
            ['accessKey'] = "AccessKey",
            ['jobId'] = jobId,
        }), Enum.HttpContentType.TextPlain)
    end)
     
    if ok then
        print("[DEBUG] thumb success:", data)
    else
        warn("[DEBUG] thumb failed:", data)
    end
end

if not containsClothing() then
    print("[debug] no character items found, rendering normally")
    render()
else
    print("[debug] character items detected, rendering with char")
    local character = makechar()
    clothing(character)
    sendthumb(character)
end

print("[DEBUG] asset.lua finished")