local CoreGui = game:GetService("CoreGui")

local Modules = CoreGui.RobloxGui.Modules

local Roact = require(Modules.Common.Roact)
local RoactRodux = require(Modules.Common.RoactRodux)
local memoize = require(Modules.Common.memoize)
local User = require(Modules.LuaApp.Models.User)
local ChatFlagSettings = require(Modules.LuaChat.FlagSettings)

local LuaChatPlayTogetherUseRootPresence = ChatFlagSettings.LuaChatPlayTogetherUseRootPresence()
local FriendIcon = require(Modules.LuaApp.Components.FriendIcon)

local PRESENCE_WEIGHTS = {
	[User.PresenceType.IN_GAME] = 3,
	[User.PresenceType.ONLINE] = 2,
	[User.PresenceType.IN_STUDIO] = 1,
	[User.PresenceType.OFFLINE] = 0,
}

local ICON_PADDING = 3

local NUMBERED_CIRCLE = "rbxasset://textures/ui/LuaChat/graphic/gr-numbers.png"
local NUMBERED_ICON_FONT = Enum.Font.SourceSans
local NUMBERED_ICON_FONT_COLOR = Color3.fromRGB(192, 192, 192)

local function NumberedIcon(props)
	local width = props.size
	local height = props.size
	local layoutOrder = props.layoutOrder
	local count = props.count

	return Roact.createElement("ImageLabel", {
		Size = UDim2.new(0, width, 0, height),
		LayoutOrder = layoutOrder,
		Image = NUMBERED_CIRCLE,
		BackgroundTransparency = 1,
	}, {
		Count = Roact.createElement("TextLabel", {
			Size = UDim2.new(0.5, 0, 0.5, 0),
			AnchorPoint = Vector2.new(0.5, 0.5),
			Position = UDim2.new(0.5, 0, 0.5, 0),
			Font = NUMBERED_ICON_FONT,
			TextSize = 12,
			Text = "+" .. count,
			TextColor3 = NUMBERED_ICON_FONT_COLOR,
			BackgroundTransparency = 1,
		}),
	})
end

local FriendFooter = Roact.PureComponent:extend("FriendFooter")

function FriendFooter:render()
	local width = self.props.width
	local height = self.props.height
	local layoutOrder = self.props.layoutOrder
	local friends = self.props.friends
	local topPadding = self.props.topPadding

	local presenceIndicatorSize = 8
	local numberedIconValue = 0

	if #friends <= 0 then
		return nil
	end

	local friendIconSize = height - topPadding

	local maxNumberOfIcons = math.floor((width + ICON_PADDING) / (friendIconSize + ICON_PADDING))

	if #friends > maxNumberOfIcons then
		numberedIconValue = #friends - (maxNumberOfIcons - 1)
	end

	local listOfIcons = {}
	listOfIcons.ListLayout = Roact.createElement("UIListLayout", {
		FillDirection = Enum.FillDirection.Horizontal,
		HorizontalAlignment = Enum.HorizontalAlignment.Center,
		VerticalAlignment = Enum.VerticalAlignment.Center,
		SortOrder = Enum.SortOrder.LayoutOrder,
		Padding = UDim.new(0, ICON_PADDING),
	})

	listOfIcons.Padding = Roact.createElement("UIPadding", {
		PaddingTop = UDim.new(0, topPadding),
	})

	local maxFriendIndex = numberedIconValue > 0 and maxNumberOfIcons - 1
							or math.min(maxNumberOfIcons, #friends)
	for index = 1, maxFriendIndex do
		local user = friends[index]
		listOfIcons["FriendIcon"..index] = Roact.createElement(FriendIcon, {
			user = user,
			dotSize = presenceIndicatorSize,
			itemSize = friendIconSize,
			layoutOrder = index,
		})
	end

	listOfIcons.NumberedIcon = numberedIconValue > 0 and Roact.createElement(NumberedIcon, {
		size = friendIconSize,
		layoutOrder = maxNumberOfIcons,
		count = numberedIconValue,
	})

	return Roact.createElement("Frame", {
		Size = UDim2.new(0, width, 0, height),
		LayoutOrder = layoutOrder,
		BackgroundTransparency = 1,
	}, listOfIcons)
end

local getSortedFriends = memoize(function(users, listOfUserIds)
	if not users or not listOfUserIds then
		return {}
	end

	local allFriends = {}
	local function friendPreference(friend1, friend2)
		local friend1Weight = PRESENCE_WEIGHTS[friend1.presence]
		local friend2Weight = PRESENCE_WEIGHTS[friend2.presence]

		if friend1Weight == friend2Weight then
			if LuaChatPlayTogetherUseRootPresence then
				if friend1.presence == User.PresenceType.IN_GAME then
					return friend1.lastOnline > friend2.lastOnline
				else
					return friend1.name < friend2.name
				end
			else
				return friend1.name < friend2.name
			end
		else
			return friend1Weight > friend2Weight
		end
	end

	for _, userId in pairs(listOfUserIds) do
		local user = users[userId]
		if user and user.isFriend then
			allFriends[#allFriends + 1] = user
		end
	end

	table.sort(allFriends, friendPreference)
	return allFriends
end)

FriendFooter = RoactRodux.UNSTABLE_connect2(
	function(state, props)
		return {
			friends = getSortedFriends(
				state.Users,
				state.InGameUsersByGame[props.universeId]
			),
		}
	end
)(FriendFooter)

return FriendFooter