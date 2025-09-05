local PlaceId = game.PlaceId
local MarketplaceService = game:GetService("MarketplaceService")
local ContentProvider = game:GetService("ContentProvider")
local GuiService = game:GetService("GuiService")
local ContextActionService = game:GetService("ContextActionService")
local StartTime = tick()
local GameAssetInfo = nil
local GameName = nil
local GameCreator = nil
local CurrentLoadingGui = nil
local Colors = {
	Main = {
		[0] = Color3.fromRGB(30, 30, 30)
	},
	Border = Color3.fromRGB(50, 50, 50),
	Background = Color3.fromRGB(15, 15, 15),
	Black = Color3.fromRGB(0, 0, 0)
}

local function GetViewportSize()
	local CurrentCamera = workspace.CurrentCamera
	
	while not CurrentCamera do
		workspace.Changed:wait()
	end

	while CurrentCamera.ViewportSize == Vector2.new(0, 0) do
		CurrentCamera.Changed:wait()
	end

	return CurrentCamera.ViewportSize
end

local Create = function(className, defaultParent)
	return function(propertyList)
		local object = Instance.new(className)

		for index, value in next, propertyList do
			if type(index) == "string" then
				object[index] = value
			else
				if type(value) == "function" then
					value(object)
				elseif type(value) == "userdata" then
					value.Parent = object
				end
			end
		end

		if object.Parent == nil then
			object.Parent = defaultParent
		end

		return object
	end
end

local function GetGameName()
	if GameAssetInfo ~= nil then
		return GameAssetInfo.Name
	else
		return "Game"
	end
end

local function GetCreatorName()
	if GameAssetInfo ~= nil then
		return GameAssetInfo.Creator.Name
	else
		return "Ted Kaczynski"
	end
end

local function LoadAssets()
	spawn(function()
		if PlaceId <= 0 then
			while game.PlaceId <= 0 do
				wait()
			end
			PlaceId = game.PlaceId
		end

		-- load game asset info
		coroutine.resume(coroutine.create(function()
			local success, result = pcall(function()
				GameAssetInfo = MarketplaceService:GetProductInfo(PlaceId)
			end)
			if not success then
				print("LoadingScript->LoadAssets:  ", result)
			end
		end))
	end)
end

local function GenerateMain()
	local LoadingGui = Create "ScreenGui" {
		Name = "RobloxLoadingGui"
	}
	
	--
	-- create descendant frames
	local Backdrop = Create "ImageLabel" {
		Name = "Backdrop",
		BackgroundColor3 = Colors.Black,
		BackgroundTransparency = 0,
		Size = UDim2.new(1, 0, 1, 0),
		Position = UDim2.new(0, 0, 0, 0),
		Active = true,
		Parent = LoadingGui,
		BorderSizePixel = 0,
		ZIndex = 1,
		Image = ""
	}
		local CloseButton =	Create "ImageButton" {
			Name = "CloseButton",
			Image = "rbxasset://textures/loading/cancelButton.png",
			ImageTransparency = 1,
			BackgroundTransparency = 1,
			Position = UDim2.new(1, -37, 0, 5),
			Size = UDim2.new(0, 32, 0, 32),
			Active = false,
			ZIndex = 10,
			Parent = Backdrop
		}
	local LoadingFrame = Create "Frame" {
		Name = "LoadingFrame",
		BackgroundTransparency = 1,
		Size = UDim2.new(0, 700, 0, 479),
		ZIndex = 1,
		Position = UDim2.new(0.5, -350, 0.5, -239),
		Active = true,
		Parent = LoadingGui
	}
		local DescriptionFrame = Create "Frame" {
			Name = "DescriptionFrame",
			BackgroundColor3 = Colors.Main[0],
			BorderSizePixel = 5,
			BorderColor3 = Colors.Border,
			Size = UDim2.new(0, 700, 0, 115),
			ZIndex = 1,
			Position = UDim2.new(0, 0, 0.75, 0),
			Active = true,
			Parent = LoadingFrame
		}
			local GameDescriptionFrame = Create "Frame" {
				Name = "GameDescriptionFrame",
				BackgroundTransparency = 1,
				Size = UDim2.new(0, 330, 0, 85),
				ZIndex = 1,
				Position = UDim2.new(0, 20, 0, 10),
				Active = true,
				Parent = DescriptionFrame
			}
				local CreatorLabel = Create "TextLabel" {
					Name = "CreatorLabel",
					BackgroundTransparency = 1,
					Size = UDim2.new(0, 0, 0, 50),
					Position = UDim2.new(0, 200, 0, 50),
					Active = true,
					Parent = DescriptionFrame,
					Font = Enum.Font.SourceSansLight,
					Text = "by Ted Kaczynski",
					TextColor3 = Color3.new(1, 1, 1),
					TextStrokeTransparency = 1,
					ZIndex = 10,
					FontSize = Enum.FontSize.Size24,
					TextYAlignment = Enum.TextYAlignment.Top,
					TextXAlignment = Enum.TextXAlignment.Left
				}
				local GameLabel = Create "TextLabel" {
					Name = "GameLabel",
					BackgroundTransparency = 1,
					Size = UDim2.new(0, 0, 0, 0),
					Position = UDim2.new(0, 200, 0, 50),
					Active = true,
					Parent = DescriptionFrame,
					Font = Enum.Font.SourceSansBold,
					Text = "NIGGA FART SEX",
					ZIndex = 10,
					TextColor3 = Color3.new(1, 1, 1),
					TextStrokeTransparency = 1,
					FontSize = Enum.FontSize.Size42,
					TextYAlignment = Enum.TextYAlignment.Bottom,
					TextXAlignment = Enum.TextXAlignment.Left
				}
		local ThumbnailFrame = Create "Frame" {
			Name = "ThumbnailFrame",
			BackgroundColor3 = Colors.Main[0],
			BorderSizePixel = 5,
			BorderColor3 = Colors.Border,
			Size = UDim2.new(0, 700, 0, 350),
			Position = UDim2.new(0, 0, 0, 0),
			Active = true,
			ZIndex = 1,
			Parent = LoadingFrame
		}
			local Thumbnail = Create "ImageLabel" {
				Name = "Thumbnail",
				BackgroundColor3 = Colors.Black,
				BackgroundTransparency = 1,
				Size = UDim2.new(0, 700, 0, 350),
				Position = UDim2.new(0, 0, 0, 0),
				Active = true,
				Parent = ThumbnailFrame,
				ZIndex = 10,
				Image = ""
			}
	
	while not game:GetService("CoreGui") do
		wait()
	end
	
	LoadingGui.Parent = game:GetService("CoreGui")
	CurrentLoadingGui = LoadingGui
end

local function round(num, idp)
	local mult = 10 ^ (idp or 0)
	return math.floor(num * mult + 0.5) / mult
end

LoadAssets()
GenerateMain()

local RemovedLoadingScreen = false
local SetVerb = true
local LastRenderTime = nil
local FadeCycleTime = 1.7
local BrickCountChange = nil
local LastBrickCount = 0
local RunService = game:GetService("RunService")
local ReplicatedFirst = game:GetService("ReplicatedFirst")
local DestroyingBackground = false
local RenderSteppedConnection = nil
local CurrentGuiTransparency = 0
local DestroyedLoadingGui = false
local HasReplicatedFirstElements

local function OnRenderStepped()
	if not CurrentLoadingGui then return end
	if not CurrentLoadingGui:FindFirstChild("LoadingFrame") then return end
	
	if SetVerb then
		CurrentLoadingGui.LoadingFrame.CloseButton:SetVerb("Exit")
		SetVerb = false
	end
	
	local DescFrame = CurrentLoadingGui:FindFirstChild("DescriptionFrame")
	local GameDescFrame = DescFrame:FindFirstChild("GameDescriptionFrame")
	
	if GameDescFrame then
		local GameLabel = GameDescFrame:FindFirstChild("GameLabel")
		if GameLabel and GameLabel.Text == "" then
			GameLabel.Text = GetGameName()
		end
		
		local CreatorLabel = GameDescFrame:FindFirstChild("CreatorLabel")
		if CreatorLabel and CreatorLabel.Text == "" then
			CreatorLabel.Text = "by " .. GetCreatorName()
		end
	end
	
	if not LastRenderTime then
		LastRenderTime = tick()
		return
	end
	
	local CurrentTime = tick()
	local FadeAmount = (CurrentTime - LastRenderTime) * FadeCycleTime
	
	if CurrentTime - StartTime > 5 and CurrentLoadingGui.BlackFrame.CloseButton.ImageTransparency > 0 then
		CurrentLoadingGui.Backdrop.CloseButton.ImageTransparency = CurrentLoadingGui.Backdrop.CloseButton.ImageTransparency - FadeAmount
		if currScreenGui.BlackFrame.CloseButton.ImageTransparency <= 0 then
			currScreenGui.BlackFrame.CloseButton.Active = true
		end
	end
end
RenderSteppedConnection = RunService.RenderStepped:Connect(OnRenderStepped)

local function StopListeningToRenderingStep()
	if RenderSteppedConnection then
		RenderSteppedConnection = nil
	end
end

local function DestroyLoadingGui()
	spawn(function()
		CurrentLoadingGui:Destroy()
		StopListeningToRenderingStep()
	end)
end

local function HandleFinishedReplication()
	HasReplicatedFirstElements = (#game:GetService("ReplicatedFirst"):GetChildren() > 0)
	
	if not HasReplicatedFirstElements then
		if game:IsLoaded() then
			DestroyLoadingGui()
		end
	else
		wait(5)
		DestroyLoadingGui()
	end
end

ReplicatedFirst.FinishedReplicating:Connect(HandleFinishedReplication)
ReplicatedFirst.RemoveDefaultLoadingGuiSignal:Connect(DestroyLoadingGui)
if ReplicatedFirst:IsFinishedReplicating() then
	HandleFinishedReplicating()
elseif ReplicatedFirst:IsDefaultLoadingGuiRemoved() then
	DestroyedLoadingGui()
end