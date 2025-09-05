local CoreGui = game:GetService("CoreGui")
local Players = game:GetService("Players")
local LocalizationService = game:GetService("LocalizationService")
local RunService = game:GetService("RunService")
local Workspace = game:GetService("Workspace")
local UserInputService = game:GetService("UserInputService")
local NotificationService = game:GetService("NotificationService")
local GuiService = game:GetService("GuiService")

local Modules = CoreGui.RobloxGui.Modules

local Roact = require(Modules.Common.Roact)
local Rodux = require(Modules.Common.Rodux)
local RoactRodux = require(Modules.Common.RoactRodux)
local ExternalEventConnection = require(Modules.Common.RoactUtilities.ExternalEventConnection)

local Promise = require(Modules.LuaApp.Promise)
local PromiseUtilities = require(Modules.LuaApp.PromiseUtilities)
local Localization = require(Modules.LuaApp.Localization)
local RoactServices = require(Modules.LuaApp.RoactServices)
local RoactAnalytics = require(Modules.LuaApp.Services.RoactAnalytics)
local RoactLocalization = require(Modules.LuaApp.Services.RoactLocalization)
local RoactNetworking = require(Modules.LuaApp.Services.RoactNetworking)
local AppNotificationService = require(Modules.LuaApp.Services.AppNotificationService)
local AppGuiService = require(Modules.LuaApp.Services.AppGuiService)
local FlagSettings = require(Modules.LuaApp.FlagSettings)
local AppPage = require(Modules.LuaApp.AppPage)

local AppRouter = require(Modules.LuaApp.Components.AppRouter)
local Toast = require(Modules.LuaApp.Components.Toast)
local BottomBar = require(Modules.LuaApp.Components.BottomBar)
local ScreenGuiWrap = require(Modules.LuaApp.Components.ScreenGuiWrap)
local HomePage = require(Modules.LuaApp.Components.Home.HomePage)
local GamesHub = require(Modules.LuaApp.Components.Games.GamesHub)
local GamesList = require(Modules.LuaApp.Components.Games.GamesList)
local SearchPage = require(Modules.LuaApp.Components.Search.SearchPage)
local RoactAvatarEditorWrapper
local AvatarEditorEventReceiver = nil

if (settings():GetFFlag("AvatarEditorRoactRewrite")) then
	RoactAvatarEditorWrapper = require(Modules.LuaApp.Components.Avatar.RoactAvatarEditorWrapperV2)
	AvatarEditorEventReceiver = require(Modules.LuaApp.Components.EventReceivers.AvatarEditorEventReceiver)
else
	RoactAvatarEditorWrapper = require(Modules.LuaApp.Components.Avatar.RoactAvatarEditorWrapper)
end
local RoactChatWrapper = require(Modules.LuaApp.Components.Chat.RoactChatWrapper)
local GameDetailWrapper = require(Modules.LuaApp.Components.GameDetailWrapper)
local RoactDummyPageWrap = require(Modules.LuaApp.Components.RoactDummyPageWrap)
local RoactGameShareWrapper = require(Modules.LuaApp.Components.Chat.RoactGameShareWrapper)
local MorePage = require(Modules.LuaApp.Components.More.MorePage)
local AboutPage = require(Modules.LuaApp.Components.More.AboutPage)
local SettingsPage = require(Modules.LuaApp.Components.More.SettingsPage)
local BadgeEventReceiver = require(Modules.LuaApp.Components.EventReceivers.BadgeEventReceiver)
local FriendshipEventReceiver = require(Modules.LuaApp.Components.EventReceivers.FriendshipEventReceiver)
local NavigationEventReceiver = require(Modules.LuaApp.Components.EventReceivers.NavigationEventReceiver)

local ThemeProvider = require(Modules.LuaApp.ThemeProvider)
local ClassicTheme = require(Modules.LuaApp.Themes.ClassicTheme)

local AppReducer = require(Modules.LuaApp.AppReducer)

local RobloxEventReceiver = require(Modules.LuaApp.RobloxEventReceiver)

local RetrievalStatus = require(Modules.LuaApp.Enum.RetrievalStatus)
local Constants = require(Modules.LuaApp.Constants)
local SetFormFactor = require(Modules.LuaApp.Actions.SetFormFactor)
local SetPlatform = require(Modules.LuaApp.Actions.SetPlatform)
local DeviceOrientationMode = require(Modules.LuaApp.DeviceOrientationMode)
local SetDeviceOrientation = require(Modules.LuaApp.Actions.SetDeviceOrientation)
local GetLocalUser = require(Modules.LuaApp.Thunks.GetLocalUser)
local SetHomePageDataStatus = require(Modules.LuaApp.Actions.SetHomePageDataStatus)
local SetGamesPageDataStatus = require(Modules.LuaApp.Actions.SetGamesPageDataStatus)
local SetScreenSize = require(Modules.LuaApp.Actions.SetScreenSize)
local SetUserMembershipType = require(Modules.LuaApp.Actions.SetUserMembershipType)
local ApiFetchUsersThumbnail = require(Modules.LuaApp.Thunks.ApiFetchUsersThumbnail)
local Analytics = require(Modules.Common.Analytics)
local request = require(Modules.LuaApp.Http.request)
local ApiFetchSortTokens = require(Modules.LuaApp.Thunks.ApiFetchSortTokens)
local ApiFetchGamesData = require(Modules.LuaApp.Thunks.ApiFetchGamesData)
local ApiFetchUsersFriends = require(Modules.LuaApp.Thunks.ApiFetchUsersFriends)
local FormFactor = require(Modules.LuaApp.Enum.FormFactor)
local ApiFetchUnreadNotificationCount = require(Modules.LuaApp.Thunks.ApiFetchUnreadNotificationCount)
local FetchGamesPageData = require(Modules.LuaApp.Thunks.FetchGamesPageData)
local FetchChatData = require(Modules.LuaApp.Thunks.FetchChatData)
local ApiFetchSiteMessage = require(Modules.LuaApp.Thunks.ApiFetchSiteMessage)

local ChatMaster = require(Modules.ChatMaster)

local analyticsReleasePeriod = tonumber(settings():GetFVariable("LuaAnalyticsReleasePeriod"))

-- flag dependencies
local luaAppLegacyInputDisabledGlobally = settings():GetFFlag('LuaAppLegacyInputDisabledGlobally2')
local LuaHomePageShowFriendAvatarFace = settings():GetFFlag("LuaHomePageShowFriendAvatarFace150By150")
local LuaHomePageEnablePlacesListV1 = settings():GetFFlag("LuaHomePageEnablePlacesListV1")
local EnableLuaGamesListSortsFix = settings():GetFFlag("EnableLuaGamesListSortsFix")
local siteMessageBannerEnabled = settings():GetFFlag('LuaAppSiteMessageBannerEnabled')

local diagCounterHomePageLoadTimes = settings():GetFVariable("LuaAppsDiagPageLoadTimeHome")

local function getDevicePlatform()
	if _G.__TESTEZ_RUNNING_TEST__ then
		return Enum.Platform.None
	end

	return UserInputService:GetPlatform()
end

local function sumNumberOfFailures(batchPromiseResults)
	local failureCount = 0
	for _, result in pairs(batchPromiseResults) do
		local success, _ = result:unwrap()
		if not success then
			failureCount = failureCount + 1
		end
	end

	return failureCount
end

local App = Roact.Component:extend("App")

function App:init()
	self.state = {
		store = Rodux.Store.new(AppReducer)
	}

	self._analytics = Analytics.new()
	self._networkRequest = request
	self._localization = Localization.new(LocalizationService.RobloxLocaleId)
	self._robloxEventReceiver = RobloxEventReceiver.new(NotificationService)

	self.updateLocalization = function(newLocale)
		self._localization:SetLocale(newLocale)
	end

	self._chatMaster = ChatMaster.new(self.state.store)

	local function wrapPageInScreenGui(component, pageType, visible, props)
		return Roact.createElement(ScreenGuiWrap, {
			component = component,
			pageType = pageType,
			isVisible = visible,
			props = props,
		})
	end

	self.pageConstructors = {
		[AppPage.None] = function()
			return nil
		end,
		[AppPage.Home] = function(visible)
			if FlagSettings.IsLuaHomePageEnabled(getDevicePlatform()) then
				return wrapPageInScreenGui(HomePage, AppPage.Home, visible)
			end
			return nil
		end,
		[AppPage.Games] = function(visible)
			if FlagSettings.IsLuaGamesPageEnabled(getDevicePlatform()) then
				return wrapPageInScreenGui(GamesHub, AppPage.Games, visible)
			end
			return nil
		end,
		[AppPage.GamesList] = function(visible, detail)
			return wrapPageInScreenGui(GamesList, AppPage.GamesList, visible, { sortName = detail })
		end,
		[AppPage.SearchPage] = function(visible, detail)
			local parameters = detail and { searchUuid = detail } or nil
			return wrapPageInScreenGui(SearchPage, AppPage.SearchPage, visible, parameters)
		end,
		[AppPage.GameDetail] = function(visible, detail)
			return Roact.createElement(GameDetailWrapper, {
				isVisible = visible,
				placeId = detail,
			})
		end,
		[AppPage.AvatarEditor] = function(visible)
			return Roact.createElement(RoactAvatarEditorWrapper, {
				isVisible = visible,
			})
		end,
		[AppPage.Chat] = function(visible, detail)
			return Roact.createElement(RoactChatWrapper, {
				chatMaster = self._chatMaster,
				isVisible = visible,
				pageType = AppPage.Chat,
				parameters = detail and { conversationId = detail } or nil
			})
		end,
		[AppPage.ShareGameToChat] = function(visible, detail)
			local parameters = {
				chatMaster = self._chatMaster,
			}
			if detail then
				parameters.placeId = detail
			end
			return wrapPageInScreenGui(RoactGameShareWrapper, AppPage.ShareGameToChat, visible, parameters)
		end,
		[AppPage.Catalog] = function(visible)
			return Roact.createElement(RoactDummyPageWrap, {
				isVisible = visible,
				pageType = "Catalog",
			})
		end,
		[AppPage.Friends] = function(visible)
			return Roact.createElement(RoactDummyPageWrap, {
				isVisible = visible,
				pageType = "Friends",
			})
		end,
		[AppPage.More] = function(visible)
			return wrapPageInScreenGui(MorePage, AppPage.More, visible)
		end,
		[AppPage.About] = function(visible)
			return wrapPageInScreenGui(AboutPage, AppPage.About, visible)
		end,
		[AppPage.Settings] = function(visible)
			return wrapPageInScreenGui(SettingsPage, AppPage.Settings, visible)
		end,
	}

	self.alwaysRenderedPages = {
		{ name = AppPage.Home },
		{ name = AppPage.Games },
		{ name = AppPage.AvatarEditor },
		{ name = AppPage.Chat },
	}

	self.updateDeviceOrientation = function(viewportSize)
		local deviceOrientation = viewportSize.x > viewportSize.y and
			DeviceOrientationMode.Landscape or DeviceOrientationMode.Portrait

		if self._deviceOrientation ~= deviceOrientation then
			self._deviceOrientation = deviceOrientation
			self.state.store:dispatch(SetDeviceOrientation(self._deviceOrientation))
		end
	end

	self.updateDeviceFormFactor = function(viewportSize)
		local formFactor = FormFactor.TABLET

		if viewportSize.Y > viewportSize.X then
			formFactor = FormFactor.PHONE
		end

		self.state.store:dispatch(SetFormFactor(formFactor))
	end

	self.updateViewport = function()
		local viewportSize = Workspace.CurrentCamera.ViewportSize

		-- Hacky code awaits underlying mechanism fix.
		-- Viewport will get a 0,0,1,1 rect before it is properly set.
		if viewportSize.X > 1 and viewportSize.Y > 1 then
			self.state.store:dispatch(SetScreenSize(viewportSize))
			self.updateDeviceOrientation(viewportSize)

			if FlagSettings.IsLuaAppDeterminingFormFactorAndPlatform() then
				self.updateDeviceFormFactor(viewportSize)
			end
		end
	end

	self.updateLocalPlayerMembership = function()
		local localPlayer = Players.LocalPlayer
		local userId = tostring(localPlayer.UserId)

		self.state.store:dispatch(SetUserMembershipType(userId, localPlayer.MembershipType))
	end

	self.updateDevicePlatform = function()
		-- Have to filter this to handle studio testing plugin which runs in a
		-- downgraded security context
		local platform = getDevicePlatform()
		self.state.store:dispatch(SetPlatform(platform))
	end

	self.releaseStream = function()
		-- there is currently a bug with the EventStream, where the stream is not released
		-- by the game engine. This call is a temporary work around until a new api is available.
		self._analytics.EventStream:releaseRBXEventStream()
	end

	-- Make sure that analytics are reported
	self.releaseEvents = true
	spawn(function()
		wait(analyticsReleasePeriod)
		while self.releaseEvents do
			self.releaseStream()
			wait(analyticsReleasePeriod)
		end
	end)

	-- the BindToClose function does not play nicely with Studio.
	if not RunService:IsStudio() then
		game:BindToClose(function()
			self.releaseEvents = false
			self.releaseStream()
		end)
	end

	if FlagSettings.IsLuaAppDeterminingFormFactorAndPlatform() then
		self.updateDevicePlatform()
	end
end

function App:didMount()
	local platform = self.state.store:getState().Platform

	RunService:setThrottleFramerateEnabled(true)
	UserInputService.LegacyInputEventsEnabled = (not luaAppLegacyInputDisabledGlobally)

	-- Set the device orientation for the 1st time
	-- TODO: this should be put in a seperate file.
	self.updateViewport()

	-- Add the local player info to the store for the 1st time
	local localPlayer = Players.LocalPlayer
	local userId = tostring(localPlayer.UserId)

	self.state.store:dispatch(GetLocalUser())
	self.state.store:dispatch(ApiFetchUsersThumbnail(
		self._networkRequest, {userId}, Constants.AvatarThumbnailRequests.HOME_HEADER_USER
	))
	self.state.store:dispatch(ApiFetchUnreadNotificationCount(self._networkRequest))

	-- Preload critical pages
	self:startFirstClassPagePreloadOperations(platform, userId):andThen(function()
		return self:startSecondClassPagePreloadOperations(platform)
	end)
end

function App:render()
	-- TODO: MOBLUAPP-693
	-- Choose which theme to use depending on the local device setting.
	-- The web theme setting might not match local device theme setting and this is a problem when a web view is opened.
	-- So send requests to server to send us pages with theme that matches the current local device theme setting.

	return Roact.createElement(ThemeProvider, {
		theme = ClassicTheme,
	}, {
		StoreProvider = Roact.createElement(RoactRodux.StoreProvider, {
			store = self.state.store,
		}, {
			services = Roact.createElement(RoactServices.ServiceProvider, {
				services = {
					[RoactAnalytics] = self._analytics,
					[RoactLocalization] = self._localization,
					[RoactNetworking] = self._networkRequest,
					[AppNotificationService] = NotificationService,
					[AppGuiService] = GuiService,
				}
			}, {
				PageWrapper = Roact.createElement("Folder", {}, {
					NavigationEventReceiver = Roact.createElement(NavigationEventReceiver,{
						RobloxEventReceiver = self._robloxEventReceiver,
					}),
					BadgeEventReceiver = Roact.createElement(BadgeEventReceiver, {
						RobloxEventReceiver = self._robloxEventReceiver,
					}),
					AvatarEditorEventReceiver = AvatarEditorEventReceiver and Roact.createElement(AvatarEditorEventReceiver,{
						RobloxEventReceiver = self._robloxEventReceiver,
					}),
					FriendshipEventReceiver = Roact.createElement(FriendshipEventReceiver, {
						RobloxEventReceiver = self._robloxEventReceiver,
					}),
					Toast = Roact.createElement(Toast, {
						displayOrder = Constants.DisplayOrder.Toast,
					}),
					BottomBar = Roact.createElement(BottomBar, {
						displayOrder = Constants.DisplayOrder.BottomBar,
					}),
					AppRouter = Roact.createElement(AppRouter, {
						pageConstructors = self.pageConstructors,
						alwaysRenderedPages = self.alwaysRenderedPages,
					}),
					localizationListener = Roact.createElement(ExternalEventConnection, {
						event = LocalizationService:GetPropertyChangedSignal("RobloxLocaleId"),
						callback = self.updateLocalization,
					}),
					viewportSizeListener = Roact.createElement(ExternalEventConnection, {
						event = Workspace.CurrentCamera:GetPropertyChangedSignal("ViewportSize"),
						callback = self.updateViewport,
					}),
					playerMembershipListener = Roact.createElement(ExternalEventConnection, {
						event = Players.LocalPlayer:GetPropertyChangedSignal("MembershipType"),
						callback = self.updateLocalPlayerMembership,
					}),
				})
			}),
		})
	})

end

function App:willUnmount()
	RunService:setThrottleFramerateEnabled(false)
	self._chatNotificationBroadcaster:Destruct()
end

function App:startFirstClassPagePreloadOperations(platform, userId)
	local promises = {}

	-- Preload home page data
	if FlagSettings.IsLuaHomePageEnabled(platform) and
		self.state.store:getState().Startup.HomePageDataStatus == RetrievalStatus.NotStarted then
		local startTime = tick()
		self.state.store:dispatch(SetHomePageDataStatus(RetrievalStatus.Fetching))
		local avatarThumbnailType = LuaHomePageShowFriendAvatarFace and
			Constants.AvatarThumbnailRequests.USER_CAROUSEL_HEAD_SHOT
			or Constants.AvatarThumbnailRequests.USER_CAROUSEL

		local homePageDataToRequest = {
			self.state.store:dispatch(ApiFetchUsersFriends(
				self._networkRequest, userId, avatarThumbnailType
			)),
		}
		if not EnableLuaGamesListSortsFix or not LuaHomePageEnablePlacesListV1 then
			table.insert(
				homePageDataToRequest,
				self.state.store:dispatch(ApiFetchSortTokens(self._networkRequest, Constants.GameSortGroups.HomeGames))
			)
		end
		if LuaHomePageEnablePlacesListV1 then
			table.insert(
				homePageDataToRequest,
				self.state.store:dispatch(ApiFetchSortTokens(self._networkRequest, Constants.GameSortGroups.UnifiedHomeSorts))
			)
		end

		local homePagePromise = PromiseUtilities.Batch(homePageDataToRequest):andThen(function(results)
			local isFullySuccess = sumNumberOfFailures(results) == 0
			self.state.store:dispatch(SetHomePageDataStatus(isFullySuccess and RetrievalStatus.Done or RetrievalStatus.Failed))

			local homePageFetchGamesPromises = {}
			if not EnableLuaGamesListSortsFix or not LuaHomePageEnablePlacesListV1 then
				table.insert(
					homePageFetchGamesPromises,
					self.state.store:dispatch(ApiFetchGamesData(self._networkRequest, Constants.GameSortGroups.HomeGames))
				)
			end
			if LuaHomePageEnablePlacesListV1 then
				table.insert(
					homePageFetchGamesPromises,
					self.state.store:dispatch(ApiFetchGamesData(
						self._networkRequest,
						Constants.GameSortGroups.UnifiedHomeSorts,
						nil,
						{ maxRows = Constants.UNIFIED_HOME_GAMES_FETCH_COUNT }
					))
				)
			end
			return Promise.all(homePageFetchGamesPromises)
		end):andThen(function(results)
			-- Report loading time for all home page data
			local deltaMs = (tick() - startTime) * 1000
			self._analytics.Diag:reportStats(diagCounterHomePageLoadTimes, deltaMs)
		end)

		table.insert(promises, homePagePromise)
	end

	return PromiseUtilities.Batch(promises):andThen(function(results)
		local failureCount = sumNumberOfFailures(results)
		if failureCount ~= 0 then
			warn(string.format("%d of %d first-class preloading operations failed!", failureCount, #promises))
		end
	end)
end

function App:startSecondClassPagePreloadOperations(platform)
	local promises = {}

	-- Preload games page data
	if FlagSettings.IsLuaGamesPagePreloadingEnabled(platform) and
		self.state.store:getState().Startup.GamesPageDataStatus == RetrievalStatus.NotStarted then
		self.state.store:dispatch(SetGamesPageDataStatus(RetrievalStatus.Fetching))

		local gamesDataPromise = self.state.store:dispatch(
			FetchGamesPageData(self._networkRequest, self._analytics))
		table.insert(promises, gamesDataPromise)
	end

	-- Preload chat data
	if NotificationService.IsLuaChatEnabled then
		-- TODO: Implement promise chain support in Lua chat preloading operations so that they can
		-- be included in preload operation tracking. See MOBLUAPP-787.
		self.state.store:dispatch(FetchChatData())
	end

	-- Fetch site message banner
	if siteMessageBannerEnabled then
		table.insert(promises, self.state.store:dispatch(ApiFetchSiteMessage(self._networkRequest)))
	end

	return PromiseUtilities.Batch(promises):andThen(function(results)
		local failureCount = sumNumberOfFailures(results)
		if failureCount ~= 0 then
			warn(string.format("%d of %d second-class preloading operations failed!", failureCount, #promises))
		end
	end)
end

return App
