local Modules = game:GetService("CoreGui").RobloxGui.Modules
local Common = Modules.Common
local LuaApp = Modules.LuaApp

local Roact = require(Common.Roact)
local RoactRodux = require(Modules.Common.RoactRodux)

local Constants = require(LuaApp.Constants)
local FitChildren = require(LuaApp.FitChildren)

local LocalizedSectionHeaderWithSeeAll = require(LuaApp.Components.LocalizedSectionHeaderWithSeeAll)

local NotificationType = require(LuaApp.Enum.NotificationType)
local Url = require(LuaApp.Http.Url)

local UserCarouselEntry = require(LuaApp.Components.Home.UserCarouselEntry)

local CAROUSEL_PADDING_DIM = UDim.new(0, Constants.USER_CAROUSEL_PADDING)

local FRIEND_SECTION_MARGIN = 15 - UserCarouselEntry.horizontalPadding()

local LuaHomePageShowFriendAvatarFace = settings():GetFFlag("LuaHomePageShowFriendAvatarFace150By150")

local UserCarousel = Roact.PureComponent:extend("UserCarousel")

function UserCarousel:init()
	local guiService = self.props.guiService

	self.state = {
		cardWindowStart = 1,
		cardsInWindow = 0,
		cardWidth = 0,
	}

	self.onSeeAllFriends = function()
		local url = string.format("%susers/friends", Url.BASE_URL)
		guiService:BroadcastNotification(url, NotificationType.VIEW_PROFILE)
	end

	self.scrollingFrameRefCallback = function(rbx)
		self.scrollingFrameRef = rbx
	end

	self.updateCardWindowBounds = function()
		if not self.scrollingFrameRef then
			return
		end

		local formFactor = self.props.formFactor
		local friends = self.state.friends
		local screenSize = self.props.screenSize

		local containerWidth = screenSize.X - FRIEND_SECTION_MARGIN
		local windowOffset = self.scrollingFrameRef.CanvasPosition.X

		local userCardSizeX = UserCarouselEntry.getCardWidth(formFactor)

		local cardWindowStart = math.max(1, math.floor(windowOffset / (userCardSizeX)) + 1)
		local cardsInWindow = math.ceil(containerWidth/userCardSizeX) + 1

		local maxOffset = userCardSizeX * #friends - self.scrollingFrameRef.AbsoluteSize.X
		local inScrollingBounds = windowOffset >= 0 and windowOffset <= maxOffset

		local shouldUpdate = inScrollingBounds and (cardWindowStart ~= self.state.cardWindowStart
			or cardsInWindow ~= self.state.cardsInWindow
			or userCardSizeX ~= self.state.cardWidth)

		if shouldUpdate then
			self:setState({
				cardWindowStart = cardWindowStart,
				cardsInWindow = cardsInWindow,
				cardWidth = userCardSizeX
			})
		end
	end
end

function UserCarousel:render()
	local formFactor = self.props.formFactor
	local friends = self.state.friends
	local friendCount = self.props.friendCount
	local layoutOrder = self.props.LayoutOrder
	local setPeopleListFrozen = self.props.setPeopleListFrozen

	local cardStart = self.state.cardWindowStart
	local numCards = self.state.cardsInWindow
	local cardWidth = self.state.cardWidth

	local friendSectionHeight = UserCarouselEntry.height(formFactor)

	local canvasSizeX = #friends * cardWidth + FRIEND_SECTION_MARGIN

	local function createUserEntry(user, count)
		local avatarThumbnailType

		if LuaHomePageShowFriendAvatarFace then
			avatarThumbnailType = Constants.AvatarThumbnailTypes.HeadShot
		else
			avatarThumbnailType = Constants.AvatarThumbnailTypes.AvatarThumbnail
		end

		return Roact.createElement(UserCarouselEntry, {
			user = user,
			formFactor = formFactor,
			count = count,
			highlightColor = Constants.Color.WHITE,
			setPeopleListFrozen = setPeopleListFrozen,
			thumbnailType = avatarThumbnailType,
		})
	end

	local leftPadding = math.max(0, (cardStart - 1) * cardWidth) + FRIEND_SECTION_MARGIN

	local userCards = {}

	for i = math.max(1, cardStart), math.min(#friends,cardStart + numCards) do
		table.insert(userCards, createUserEntry(friends[i], #userCards + 1))
	end

	userCards.Layout = Roact.createElement("UIListLayout", {
		SortOrder = Enum.SortOrder.LayoutOrder,
		FillDirection = Enum.FillDirection.Horizontal,
		HorizontalAlignment = Enum.HorizontalAlignment.Left,
	})

	userCards.Padding = Roact.createElement("Frame", {
		BackgroundTransparency = 1,
		LayoutOrder = 0,
		Size = UDim2.new(0, leftPadding, 1, 0),
	})

	local userCarousel = Roact.createElement("ScrollingFrame", {
		Size = UDim2.new(1, 0, 0, friendSectionHeight),
		ScrollBarThickness = 0,
		BackgroundTransparency = 1,
		CanvasSize = UDim2.new(0, canvasSizeX, 1, 0),
		ScrollingDirection = Enum.ScrollingDirection.X,

		[Roact.Change.CanvasPosition] = self.updateCardWindowBounds,
		[Roact.Ref] = self.scrollingFrameRefCallback,
	}, userCards)

	return Roact.createElement(FitChildren.FitFrame, {
			Size = UDim2.new(1, 0, 0, 0),
			fitAxis = FitChildren.FitAxis.Height,
			BackgroundTransparency = 1,
			LayoutOrder = layoutOrder,
		},
		{
			Layout = Roact.createElement("UIListLayout", {
				SortOrder = Enum.SortOrder.LayoutOrder,
			}),
			Container = Roact.createElement(FitChildren.FitFrame, {
				Size = UDim2.new(1, 0, 0, 0),
				BackgroundTransparency = 1,
				fitFields = {
					Size = FitChildren.FitAxis.Height,
				},
			},
			{
				SidePadding = Roact.createElement("UIPadding", {
					PaddingLeft = CAROUSEL_PADDING_DIM,
					PaddingRight = CAROUSEL_PADDING_DIM,
				}),
				Header = Roact.createElement(LocalizedSectionHeaderWithSeeAll, {
					text = {
						"Feature.Home.HeadingFriends",
						friendCount = friendCount,
					},
					LayoutOrder = 1,
					onSelected = self.onSeeAllFriends
				}),
			}),
			CarouselFrame = Roact.createElement("Frame", {
				Size = UDim2.new(1, 0, 0, friendSectionHeight),
				BackgroundColor3 = Constants.Color.WHITE,
				LayoutOrder = 2,
				BorderSizePixel = 0,
			},
			{
				Carousel = userCarousel
			}),
		}
	)
end

function UserCarousel.getDerivedStateFromProps(props)
	if not props.peopleListFrozen then
		return {
			friends = props.friends,
		}
	end
end

function UserCarousel:didMount()
	self.updateCardWindowBounds()
end

function UserCarousel:didUpdate(prevProps)
	if self.props.screenSize ~= prevProps.screenSize or self.props.formFactor ~= prevProps.formFactor then
		self.updateCardWindowBounds()
	end
end

UserCarousel = RoactRodux.UNSTABLE_connect2(
	function(state, props)
		return {
			screenSize = state.ScreenSize,
		}
	end
)(UserCarousel)

return UserCarousel