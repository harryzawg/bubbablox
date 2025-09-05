local CoreGui = game:GetService("CoreGui")
local Modules = CoreGui.RobloxGui.Modules

local Roact = require(Modules.Common.Roact)
local RoactRodux = require(Modules.Common.RoactRodux)

local Constants = require(Modules.LuaApp.Constants)
local JoinableFriendsList = require(Modules.LuaApp.Components.Home.JoinableFriendsList)
local FormFactor = require(Modules.LuaApp.Enum.FormFactor)
local FitChildren = require(Modules.LuaApp.FitChildren)
local SetTabBarVisible = require(Modules.LuaApp.Actions.SetTabBarVisible)

local FramePopOut = require(Modules.LuaApp.Components.FramePopOut)
local FramePopup = require(Modules.LuaApp.Components.FramePopup)

local PlacesListContextualMenu = Roact.PureComponent:extend("PlacesListContextualMenu")

local TABLET_MENU_VERTICAL_PADDING = 60

local MENU_WIDTH_ON_TABLET = Constants.DEFAULT_TABLET_CONTEXTUAL_MENU__WIDTH
local CANCEL_HEIGHT = Constants.DEFAULT_CONTEXTUAL_MENU_CANCEL_HEIGHT
local BOTTOM_BAR_SIZE = Constants.BOTTOM_BAR_SIZE

function PlacesListContextualMenu:init()
	local formFactor = self.props.formFactor
	local setTabBarVisible = self.props.setTabBarVisible
	local tabBarVisible = self.props.tabBarVisible

	local tabBarVisibilityChanged = false

	if formFactor == FormFactor.PHONE then
		setTabBarVisible(false)
		tabBarVisibilityChanged = true
	end

	self.state = {
		originalTabBarVisibility = tabBarVisible,
		tabBarVisibilityChanged = tabBarVisibilityChanged,
	}
end

function PlacesListContextualMenu:render()
	local friends = self.props.friends
	local closeCallback = self.props.closeCallback
	local screenSize = self.props.screenSize
	local formFactor = self.props.formFactor
	local topBarHeight = self.props.topBarHeight

	local parentCardSize = self.props.parentCardSize
	local getParentCardPosition = self.props.getParentCardPosition
	local parentCardPosition = getParentCardPosition()

	local isTablet = formFactor == FormFactor.TABLET
	local itemWidth = isTablet and MENU_WIDTH_ON_TABLET or screenSize.X
	local modalComponent = isTablet and FramePopOut or FramePopup

	local listMaxHeight = isTablet and screenSize.Y - TABLET_MENU_VERTICAL_PADDING - BOTTOM_BAR_SIZE - topBarHeight
									or screenSize.Y * .5 - CANCEL_HEIGHT

	return Roact.createElement(Roact.Portal, {
		target = CoreGui,
	}, {
		PortalUI = Roact.createElement("ScreenGui", {
			ZIndexBehavior = Enum.ZIndexBehavior.Sibling,
			DisplayOrder = 3
		}, {
			BackgroundScreen = Roact.createElement(modalComponent, {
				onCancel = closeCallback,
				itemWidth = itemWidth,
				parentShape = {
					x = parentCardPosition.X,
					y = parentCardPosition.Y,
					width = parentCardSize.X,
					height = parentCardSize.Y,
					parentWidth = screenSize.X,
					parentHeight = screenSize.Y,
				},
			}, {
				Roact.createElement(FitChildren.FitFrame, {
					BackgroundTransparency = 1,
					fitAxis = FitChildren.FitAxis.Height,
					Size = UDim2.new(0, itemWidth, 0, 0),
				}, {
					Layout = Roact.createElement("UIListLayout", {
						FillDirection = Enum.FillDirection.Vertical,
						HorizontalAlignment = Enum.HorizontalAlignment.Center,
						SortOrder = Enum.SortOrder.LayoutOrder,
						VerticalAlignment = Enum.VerticalAlignment.Bottom,
					}),
					Divider = Roact.createElement("Frame", {
						LayoutOrder = 2,
						Size = UDim2.new(1, 0, 0, 1),
						BackgroundColor3 = Constants.Color.GRAY4,
						BorderSizePixel = 0,
					}),
					JoinableFriendsList = Roact.createElement(JoinableFriendsList, {
						LayoutOrder = 3,
						maxHeight = listMaxHeight,
						friends = friends,
						width = itemWidth,
					}),
				}),
			}),
        }),
    })
end

function PlacesListContextualMenu:didUpdate(prevProps, prevState)
	local closeCallback = self.props.closeCallback
	local newRouteHistory = self.props.routeHistory
	local newRoute = newRouteHistory[#newRouteHistory]
	local newPage = newRoute[#newRoute]

	local oldRouteHistory = prevProps.routeHistory
	local oldRoute = oldRouteHistory[#oldRouteHistory]
	local oldPage = oldRoute[#oldRoute]

	if newPage.name ~= oldPage.name then
		closeCallback()
	end
end

function PlacesListContextualMenu:willUnmount()
	local setTabBarVisible = self.props.setTabBarVisible
	local tabBarVisibilityChanged = self.state.tabBarVisibilityChanged
	local originalTabBarVisibility = self.state.originalTabBarVisibility

	if tabBarVisibilityChanged then
		setTabBarVisible(originalTabBarVisibility)
	end
end

PlacesListContextualMenu = RoactRodux.UNSTABLE_connect2(
	function(state, props)
		return {
			tabBarVisible = state.TabBarVisible,
			topBarHeight = state.TopBar.topBarHeight,
			formFactor = state.FormFactor,
			screenSize = state.ScreenSize,
			routeHistory = state.Navigation.history,
		}
	end,
	function(dispatch)
		return {
			setTabBarVisible = function(visible)
				return dispatch(SetTabBarVisible(visible))
			end,
		}
	end
)(PlacesListContextualMenu)

return PlacesListContextualMenu