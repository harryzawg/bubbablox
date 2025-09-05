local CoreGui = game:GetService("CoreGui")
local TweenService = game:GetService("TweenService")
local Modules = CoreGui.RobloxGui.Modules
local DeviceOrientationMode = require(Modules.LuaApp.DeviceOrientationMode)
local AECategories = require(Modules.LuaApp.Components.Avatar.AECategories)
local AEConstants = require(Modules.LuaApp.Components.Avatar.AEConstants)

local AECameraManager = {}
AECameraManager.__index = AECameraManager

local cameraDefaultFOV = 70

local deviceOrientationSpecific = {
	[DeviceOrientationMode.Portrait] =
	{
		cameraCenterScreenPosition = UDim2.new(0, 0, -0.5, 40),
		cameraDefaultPosition = Vector3.new(10.2427, 5.1198, -30.9536),
	},

	[DeviceOrientationMode.Landscape] =
	{
		cameraCenterScreenPosition = UDim2.new(-0.5, 0, 0, 10),
		cameraDefaultPosition = Vector3.new(11.4540, 4.4313, -24.0810),
	},
}

-- List of body parts the camera can focus on
local avatarTypeBodyPartCameraFocus = {
	[AEConstants.AvatarType.R15] = {
		legsFocus = {'RightUpperLeg', 'LeftUpperLeg'},
		faceFocus = {'Head'},
		armsFocus = {'UpperTorso'},
		headWideFocus = {'Head'},
		neckFocus = {'Head', 'UpperTorso'},
		shoulderFocus = {'Head', 'RightUpperArm', 'LeftUpperArm'},
		waistFocus = {'LowerTorso', 'RightUpperLeg', 'LeftUpperLeg'}
	},
	[AEConstants.AvatarType.R6] = {
		legsFocus = {'Right Leg', 'Left Leg'},
		faceFocus = {'Head'},
		armsFocus = {'Torso'},
		headWideFocus = {'Head'},
		neckFocus = {'Head', 'Torso'},
		shoulderFocus = {'Head', 'Right Arm', 'Left Arm'},
		waistFocus = {'Torso', 'Right Leg', 'Left Leg'}
	}
}

local fullViewCameraCFrame = CFrame.new(
	13.2618074,   4.74155569,  -22.701086,
	-0.94241035,  0.0557777137, -0.329775006,
	 0.000000000, 0.98599577,    0.166770056,
	 0.334458828, 0.157165825,  -0.92921263)

local fullViewCameraFieldOfView = 70

function AECameraManager.new(store)
	local self = {}
	self.store = store
	self.connections = {}
	setmetatable(self, AECameraManager)

	local camera = game.Workspace.CurrentCamera
	self.camera = camera

	self.tweenInfo = TweenInfo.new(0.5, Enum.EasingStyle.Quad, Enum.EasingDirection.InOut, 0, false, 0)
	local fullViewCameraGoals = {
		CFrame = fullViewCameraCFrame;
		FieldOfView = fullViewCameraFieldOfView;
	}
	self.tweenFullView = TweenService:Create(self.camera, self.tweenInfo, fullViewCameraGoals)

	return self
end

function AECameraManager:start()
	self.camera.CameraType = Enum.CameraType.Scriptable
	local storeChangedConnection = self.store.Changed:connect(function(state, oldState)
		self:update(state, oldState)
	end)
	table.insert(self.connections, storeChangedConnection)

	local state = self.store:getState()
	local categoryIndex = state.AEAppReducer.AECategory.AECategoryIndex
	local tabIndex = state.AEAppReducer.AECategory.AETabsInfo[categoryIndex]
	local page = AECategories.categories[categoryIndex].pages[tabIndex]
	self:handleCameraChange(page, state, true)
end

function AECameraManager:stop()
	for _, connection in ipairs(self.connections) do
		connection:disconnect()
	end
	self.connections = {}
end

function AECameraManager:update(state, oldState)
	local currentCategoryIndex = state.AEAppReducer.AECategory.AECategoryIndex
	local oldCategoryIndex = oldState.AEAppReducer.AECategory.AECategoryIndex
	local currentTab = state.AEAppReducer.AECategory.AETabsInfo[currentCategoryIndex]
	local oldTab = oldState.AEAppReducer.AECategory.AETabsInfo[oldCategoryIndex]
	local currentFullView = state.AEAppReducer.AEFullView
	local oldFullView = oldState.AEAppReducer.AEFullView
	local instantChange = false

	if state.DeviceOrientation ~= oldState.DeviceOrientation then
		instantChange = true
	end
	-- Tween the camera on tab change to focus on the relevant character part
	if currentCategoryIndex ~= oldCategoryIndex or currentTab ~= oldTab or instantChange then
		self:handleCameraChange(AECategories.categories[currentCategoryIndex].pages[currentTab], state, instantChange)
	end

	if currentFullView ~= oldFullView then
		if currentFullView then
			self.tweenFullView:Play()
		else
			self:handleCameraChange(AECategories.categories[currentCategoryIndex].pages[currentTab], state)
		end
	elseif currentFullView and state.DeviceOrientation ~= oldState.DeviceOrientation then
		self.camera.CFrame = fullViewCameraCFrame
		self.camera.FieldOfView = fullViewCameraFieldOfView
	end
end

function AECameraManager:handleCameraChange(page, state, instant)
	local position = deviceOrientationSpecific[state.DeviceOrientation].cameraDefaultPosition
	local avatarType = self.store:getState().AEAppReducer.AECharacter.AEAvatarType
	local parts = avatarTypeBodyPartCameraFocus[avatarType][page.CameraFocus] or {'HumanoidRootPart'}
	local focusPoint = self:getFocusPoint(parts)

	if page.CameraZoomRadius then
		local toCamera = (position - focusPoint)
		toCamera = Vector3.new(toCamera.x, 0, toCamera.z).unit
		position = focusPoint + page.CameraZoomRadius * toCamera
	end

	self:tweenCameraIntoPlace(position, focusPoint, cameraDefaultFOV, instant)
end

function AECameraManager:getFocusPoint(partNames)
	local numParts = #partNames

	-- Focus on the torso if there is nothing specific to focus on.
	if numParts == 0 then
		local humanoid = self.store:getState().AEAppReducer.AECharacter.AECurrentCharacter.Humanoid
		return humanoid.Torso.CFrame.p
	end

	local sumOfPartPositions = Vector3.new()

	for _, partName in next, partNames do
		sumOfPartPositions = sumOfPartPositions + self:getPartPosition(partName).p
	end

	return sumOfPartPositions / numParts
end

function AECameraManager:getPartPosition(partName)
	local character = self.store:getState().AEAppReducer.AECharacter.AECurrentCharacter

	if character and character[partName] then
		return character[partName].cFrame
	else
		return CFrame.new()
	end
end

function AECameraManager:tweenCameraIntoPlace(position, focusPoint, targetFOV, instant)
	local cameraCenterScreenPosition =
		deviceOrientationSpecific[self.store:getState().DeviceOrientation].cameraCenterScreenPosition
	local screenSize = self.camera.ViewportSize
	local screenWidth = screenSize.X
	local screenHeight = screenSize.Y

	local fy = 0.5 * targetFOV * math.pi / 180.0 -- half vertical field of view (in radians)
	local fx = math.atan( math.tan(fy) * screenWidth / screenHeight ) -- half horizontal field of view (in radians)

	local anglesX = math.atan( math.tan(fx)
		* (cameraCenterScreenPosition.X.Scale + 2.0 * cameraCenterScreenPosition.X.Offset / screenWidth))
	local anglesY = math.atan( math.tan(fy)
		* (cameraCenterScreenPosition.Y.Scale + 2.0 * cameraCenterScreenPosition.Y.Offset / screenHeight))

	local targetCFrame
		= CFrame.new(position)
		* CFrame.new(Vector3.new(), focusPoint-position)
		* CFrame.Angles(anglesY,anglesX,0)

	if instant then
		self.camera.FieldOfView = targetFOV
		self.camera.CFrame = targetCFrame
	else
		local cameraGoals = {
			CFrame = targetCFrame;
			FieldOfView = targetFOV;
		}
		TweenService:Create(self.camera, self.tweenInfo, cameraGoals):Play()
	end
end

return AECameraManager