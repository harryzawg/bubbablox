local Modules = game:GetService("CoreGui").RobloxGui.Modules

local Common = Modules.Common
local LuaApp = Modules.LuaApp
local LuaChat = Modules.LuaChat

local ApiFetchGamesDataByPlaceIds = require(LuaApp.Thunks.ApiFetchGamesDataByPlaceIds)
local AppGuiService = require(LuaApp.Services.AppGuiService)
local Constants = require(LuaApp.Constants)
local FitChildren = require(LuaApp.FitChildren)
local formatInteger = require(Modules.LuaChat.Utils.formatInteger)
local FormFactor = require(LuaApp.Enum.FormFactor)
local FitImageTextButton = require(LuaApp.Components.FitImageTextButton)
local GameJoin = require(LuaChat.Utils.joinGame)

local NotificationType = require(LuaApp.Enum.NotificationType)
local PlayabilityStatus = require(LuaApp.Enum.PlayabilityStatus)

local Roact = require(Common.Roact)
local RoactRodux = require(Modules.Common.RoactRodux)
local RoactServices = require(LuaApp.RoactServices)
local RoactLocalization = require(LuaApp.Services.RoactLocalization)
local RoactNetworking = require(Modules.LuaApp.Services.RoactNetworking)

local Text = require(Modules.Common.Text)

local GAME_BUTTON_SIZE = 94
local GAME_ICON_SIZE = 90
local PHONE_PADDING = 15
local TABLET_PADDING = 12
local GAME_NAME_LABEL_HEIGHT = 40
local SEPARATOR_HEIGHT = 1
local SEPARATOR_COLOR = Constants.Color.GRAY4

local GAME_TITLE_TOP_PADDING = 12
local GAME_TITLE_BOTTOM_PADDING = 24
local GAME_TITLE_COLOR = Constants.Color.GRAY1

local DEFAULT_BACKGROUND_COLOR = Constants.Color.WHITE
local DEFAULT_BUTTON_COLOR = Constants.Color.GREEN_PRIMARY
local DEFAULT_BUTTON_TEXT_COLOR = Constants.Color.WHITE
local DEFAULT_BUTTON_HEIGHT = 32
local DEFAULT_BUTTON_WIDTH = 90
local DEFAULT_TEXT_FONT = Enum.Font.SourceSans
local DEFAULT_TEXT_SIZE = 20

local VIEW_GAME_DETAILS_FROM_ICON = "gameIcon"
local VIEW_GAME_DETAILS_FROM_TITLE = "gameTitle"
local VIEW_GAME_DETAILS_FROM_BUTTON = "viewDetailButton"

local ROBUX_ICON = "rbxasset://textures/ui/LuaApp/icons/ic-ROBUX.png"
local ROUNDED_BUTTON = "rbxasset://textures/ui/LuaChat/9-slice/input-default.png"

local TextMeasureTemporaryPatch = settings():GetFFlag("TextMeasureTemporaryPatch")

local UserActiveGame = Roact.PureComponent:extend("UserActiveGame")

local function createSeparator(layoutOrder)
	return Roact.createElement("Frame", {
		BackgroundColor3 = SEPARATOR_COLOR,
		BackgroundTransparency = 0,
		BorderSizePixel = 0,
		LayoutOrder = layoutOrder,
		Size = UDim2.new(1, 0, 0, SEPARATOR_HEIGHT),
	})
end

function UserActiveGame:init()
	self.game = {}
	self.interactiveButtonProps = {}

	self.openGameDetails = function(fromWhere)
		if self.game.placeId then
			self.props.analytics.reportViewProfileFromPeopleList(
				self.game.friendId,
				self.props.position,
				self.game.rootPlaceId,
				fromWhere
			)

			self.props.dismissContextualMenu()
			self.props.guiService:BroadcastNotification(
				self.game.placeId,
				NotificationType.VIEW_GAME_DETAILS_ANIMATED
			)
		end
	end

	self.joinGameByUser = function()
		self.props.dismissContextualMenu()
		self.props.analytics.reportPeopleListJoinGame(
				self.game.friendId,
				self.props.position,
				self.game.rootPlaceId,
				self.game.gameInstanceId
		)

		GameJoin:ByUser(self.props.friend)
	end

	self.getGameTitleHeight = function(text, font, textSize, maxWidth)
		local gameTitleHeight = Text.GetTextHeight(text, font, textSize, maxWidth)

		-- TODO(CLIPLAYEREX-1633): We can remove this padding patch after fixing TextService:GetTextSize sizing bug
		-- When the flag TextMeasureTemporaryPatch is on, Text.GetTextHeight() would add 2px to the total height
		-- For getting the correct height, 2px need to subtracting from here.
		if TextMeasureTemporaryPatch then
			gameTitleHeight = gameTitleHeight - 2
		end

		return math.min(GAME_NAME_LABEL_HEIGHT, gameTitleHeight)
	end
end

function UserActiveGame:createGameNameButton(height, gameName, textXAlignment, textYAlignment)
	return Roact.createElement("TextButton", {
		BackgroundTransparency = 1,
		BorderSizePixel = 0,
		Font = DEFAULT_TEXT_FONT,
		LayoutOrder = 1,
		Size = UDim2.new(1, 0, 0, height),
		Text = gameName,
		TextColor3 = GAME_TITLE_COLOR,
		TextSize = DEFAULT_TEXT_SIZE,
		TextTruncate = Enum.TextTruncate.AtEnd,
		TextWrapped = true,
		TextXAlignment = textXAlignment,
		TextYAlignment = textYAlignment,

		[Roact.Event.Activated] = function()
			self.openGameDetails(VIEW_GAME_DETAILS_FROM_TITLE)
		end,
	})
end

function UserActiveGame:createInteractiveButton(buyToPlay, maxWidth, gamePrice)
	local localization = self.props.localization

	local interactiveButtonBackgroundColor = self.interactiveButtonProps.backgroundColor
	local interactiveButtonOnActivated = self.interactiveButtonProps.onActivated
	local interactiveButtonTextKey = self.interactiveButtonProps.textKey
	local interactiveButtonTextColor = self.interactiveButtonProps.textColor

	return buyToPlay and Roact.createElement(FitImageTextButton, {
		backgroundImage = ROUNDED_BUTTON,
		layoutOrder = 3,
		leftIcon = ROBUX_ICON,
		maxWidth = maxWidth,
		minWidth = DEFAULT_BUTTON_WIDTH,
		text = formatInteger(gamePrice),

		onActivated = function()
			self.openGameDetails(VIEW_GAME_DETAILS_FROM_BUTTON)
		end,
	}) or Roact.createElement(FitImageTextButton, {
		backgroundColor = interactiveButtonBackgroundColor,
		backgroundImage = ROUNDED_BUTTON,
		layoutOrder = 3,
		leftIconEnabled = false,
		maxWidth = maxWidth,
		minWidth = DEFAULT_BUTTON_WIDTH,
		text = localization:Format(interactiveButtonTextKey),
		textColor = interactiveButtonTextColor,
		onActivated = interactiveButtonOnActivated,
	})
end

function UserActiveGame:renderPhone()
	local layoutOrder = self.props.layoutOrder
	local width = self.props.width

	local buyToPlay = self.game.buyToPlay
	local gameIcon = self.game.icon
	local gameName = self.game.name
	local gamePrice = self.game.price

	local maxWidth = width - 2 * PHONE_PADDING
	local gameNameHeight = self.getGameTitleHeight(gameName, DEFAULT_TEXT_FONT, DEFAULT_TEXT_SIZE, maxWidth)
	local iconPadding = (GAME_BUTTON_SIZE - GAME_ICON_SIZE) / 2

	return Roact.createElement(FitChildren.FitFrame, {
		BackgroundTransparency = 1,
		fitAxis = FitChildren.FitAxis.Height,
		LayoutOrder = layoutOrder,
		Size = UDim2.new(1, 0, 0, 0),
	}, {
		GameContent = Roact.createElement(FitChildren.FitFrame, {
			BackgroundTransparency = 1,
			fitAxis = FitChildren.FitAxis.Height,
			Size = UDim2.new(1, 0, 0, 0),
		}, {
			Layout = Roact.createElement("UIListLayout", {
				FillDirection = Enum.FillDirection.Vertical,
				HorizontalAlignment = Enum.HorizontalAlignment.Center,
				SortOrder = Enum.SortOrder.LayoutOrder,
			}),

			GameIcon = Roact.createElement("Frame", {
				BackgroundColor3 = DEFAULT_BACKGROUND_COLOR,
				BackgroundTransparency = 0,
				BorderSizePixel = 0,
				LayoutOrder = 1,
				Size = UDim2.new(0, GAME_BUTTON_SIZE, 0, GAME_BUTTON_SIZE),
			}, {
				Icon = Roact.createElement("ImageButton", {
					BackgroundTransparency = 1,
					BorderSizePixel = 0,
					Image = gameIcon,
					Position = UDim2.new(0, iconPadding, 0, iconPadding),
					Size = UDim2.new(0, GAME_ICON_SIZE, 0, GAME_ICON_SIZE),

					[Roact.Event.Activated] = function()
						self.openGameDetails(VIEW_GAME_DETAILS_FROM_ICON)
					end,
				})
			}),

			GameName = Roact.createElement(FitChildren.FitFrame, {
				BackgroundTransparency = 1,
				fitAxis = FitChildren.FitAxis.Height,
				LayoutOrder = 2,
				Size = UDim2.new(1, -2 * PHONE_PADDING, 0, 0),
			},{
				Layout = Roact.createElement("UIListLayout", {
					HorizontalAlignment = Enum.HorizontalAlignment.Center,
				}),

				NameButton =  self:createGameNameButton(
					gameNameHeight,
					gameName,
					Enum.TextXAlignment.Center,
					Enum.TextYAlignment.Center
				),

				Padding = Roact.createElement("UIPadding", {
					PaddingTop = UDim.new(0, GAME_TITLE_TOP_PADDING),
					PaddingBottom = UDim.new(0, GAME_TITLE_BOTTOM_PADDING),
				}),
			}),

			InteractiveButton = self:createInteractiveButton(buyToPlay, maxWidth, gamePrice),

			Separator = Roact.createElement(FitChildren.FitFrame, {
				BackgroundTransparency = 1,
				Size = UDim2.new(1, 0, 0, 0),
				LayoutOrder = 5,
				fitAxis = FitChildren.FitAxis.Height,
			}, {
				Layout = Roact.createElement("UIListLayout", {
					FillDirection = Enum.FillDirection.Vertical,
					SortOrder = Enum.SortOrder.LayoutOrder,
				}),

				Line = createSeparator(2),

				Padding = Roact.createElement("UIPadding", {
					PaddingTop = UDim.new(0, GAME_TITLE_BOTTOM_PADDING - SEPARATOR_HEIGHT),
				})
			}),
		}),

		Background = Roact.createElement("Frame", {
			BackgroundColor3 = DEFAULT_BACKGROUND_COLOR,
			BorderSizePixel = 0,
			Size = UDim2.new(1, 0, 1, -GAME_BUTTON_SIZE / 2),
			Position = UDim2.new(0, 0, 0, GAME_BUTTON_SIZE / 2),
		}),
	})
end

function UserActiveGame:renderTablet()
	local layoutOrder = self.props.layoutOrder
	local width = self.props.width

	local buyToPlay = self.game.buyToPlay
	local gameIcon = self.game.icon
	local gameName = self.game.name
	local gamePrice = self.game.price

	local maxWidth = width - 3 * TABLET_PADDING - GAME_ICON_SIZE
	local buttonTopPadding = GAME_ICON_SIZE - GAME_NAME_LABEL_HEIGHT - DEFAULT_BUTTON_HEIGHT

	return Roact.createElement(FitChildren.FitFrame, {
		BackgroundColor3 = DEFAULT_BACKGROUND_COLOR,
		BorderSizePixel = 0,
		fitAxis = FitChildren.FitAxis.Height,
		LayoutOrder = layoutOrder,
		Size = UDim2.new(0, width, 0, 0),
	}, {
		Layout = Roact.createElement("UIListLayout", {
			FillDirection = Enum.FillDirection.Vertical,
			SortOrder = Enum.SortOrder.LayoutOrder,
		}),

		GameContent = Roact.createElement(FitChildren.FitFrame, {
			BackgroundTransparency = 1,
			BorderSizePixel = 0,
			BackgroundColor3 = Constants.Color.WHITE,
			fitAxis = FitChildren.FitAxis.Height,
			LayoutOrder = 1,
			Size = UDim2.new(1, 0, 0, 0),
		},{
			Padding = Roact.createElement("UIPadding", {
				PaddingTop = UDim.new(0, TABLET_PADDING),
				PaddingLeft = UDim.new(0, TABLET_PADDING),
				PaddingRight = UDim.new(0, TABLET_PADDING),
				PaddingBottom = UDim.new(0, TABLET_PADDING - 1),
			}),

			Layout = Roact.createElement("UIListLayout", {
				FillDirection = Enum.FillDirection.Horizontal,
				SortOrder = Enum.SortOrder.LayoutOrder,
				Padding = UDim.new(0, PHONE_PADDING),
			}),

			GameIcon = Roact.createElement("ImageButton", {
				BackgroundTransparency = 1,
				BorderSizePixel = 0,
				LayoutOrder = 1,
				Image = gameIcon,
				Size = UDim2.new(0, GAME_ICON_SIZE, 0, GAME_ICON_SIZE),

				[Roact.Event.Activated] = function()
					self.openGameDetails(VIEW_GAME_DETAILS_FROM_ICON)
				end,
			}, {
				Padding = Roact.createElement("UIPadding", {
					PaddingLeft = UDim.new(0, TABLET_PADDING),
				}),
			}),

			GameInfo = Roact.createElement(FitChildren.FitFrame, {
				BackgroundTransparency = 1,
				fitAxis = FitChildren.FitAxis.Height,
				LayoutOrder = 2,
				Size = UDim2.new(1, -PHONE_PADDING - GAME_ICON_SIZE, 0, 0),
			}, {
				Layout = Roact.createElement("UIListLayout", {
					FillDirection = Enum.FillDirection.Vertical,
					SortOrder = Enum.SortOrder.LayoutOrder,
				}),

				NameButton = self:createGameNameButton(
					GAME_NAME_LABEL_HEIGHT,
					gameName,
					Enum.TextXAlignment.Left,
					Enum.TextYAlignment.Top
				),

				InteractiveButton = Roact.createElement(FitChildren.FitFrame, {
					BackgroundTransparency = 1,
					Size = UDim2.new(1, 0, 0, 0),
					LayoutOrder = 2,
					fitAxis = FitChildren.FitAxis.Height,
				}, {
					Layout = Roact.createElement("UIListLayout", {
						HorizontalAlignment = Enum.HorizontalAlignment.Right,
					}),

					Button = self:createInteractiveButton(buyToPlay, maxWidth, gamePrice),

					Padding = Roact.createElement("UIPadding", {
						PaddingTop = UDim.new(0, buttonTopPadding),
					})
				}),
			}),
		}),

		Separator = createSeparator(2),
	})
end

function UserActiveGame:render()
	local formFactor = self.props.formFactor
	local friend = self.props.friend
	local gameThumbnail = self.props.gameThumbnail
	local universePlaceInfo = self.props.universePlaceInfo

	local buyToPlay = universePlaceInfo.reasonProhibited == PlayabilityStatus.PurchaseRequired

	self.game = {
		buyToPlay = buyToPlay,
		friendId = friend.id,
		icon = gameThumbnail,
		gameInstanceId = friend.gameInstanceId,
		name = universePlaceInfo.name,
		price = universePlaceInfo.price,
		placeId = universePlaceInfo.placeId,
		rootPlaceId = universePlaceInfo.universeRootPlaceId,
	}

	self.interactiveButtonProps = {
		backgroundColor = DEFAULT_BUTTON_COLOR,
		textColor = DEFAULT_BUTTON_TEXT_COLOR,
	}

	if universePlaceInfo.isPlayable then
		self.interactiveButtonProps.onActivated = self.joinGameByUser
		self.interactiveButtonProps.textKey = "Feature.Chat.Drawer.Join"
	elseif buyToPlay then
		self.interactiveButtonProps.onActivated = function()
			self.openGameDetails(VIEW_GAME_DETAILS_FROM_TITLE)
		end
		self.interactiveButtonProps.textKey = "Feature.Home.PeopleList.BuyToPlay"
	else
		self.interactiveButtonProps = {
			onActivated = function()
				self.openGameDetails(VIEW_GAME_DETAILS_FROM_TITLE)
			end,
			backgroundColor = Constants.Color.WHITE,
			textColor = Constants.Color.GRAY1,
			textKey = "Feature.Chat.Drawer.ViewDetails",
		}
	end

	if formFactor == FormFactor.PHONE then
		return self:renderPhone()
	else
		return self:renderTablet()
	end
end

function UserActiveGame:didMount()
	-- Use what store has to render the UI first [Non-Blocking UI]
	-- Only update the game data asynchronously when initializing UserActiveGame each time.[Present the latest data]
	self.props.refreshGameData(self.props.networking, self.props.friend.placeId)
end


UserActiveGame = RoactRodux.UNSTABLE_connect2(
	function(state, props)
		return {
			gameThumbnail = state.GameThumbnails[tostring(props.universeId)],
			universePlaceInfo = state.UniversePlaceInfos[props.universeId],
		}
	end,

	function(dispatch)
		return {
			refreshGameData = function(networking, placeId)
				return dispatch(ApiFetchGamesDataByPlaceIds(networking, {placeId}))
			end,
		}
	end
)(UserActiveGame)

UserActiveGame = RoactServices.connect({
	guiService = AppGuiService,
	localization = RoactLocalization,
	networking = RoactNetworking,
})(UserActiveGame)

return UserActiveGame