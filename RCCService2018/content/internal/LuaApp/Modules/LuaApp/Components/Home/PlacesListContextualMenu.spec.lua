return function()
	local Modules = game:GetService("CoreGui").RobloxGui.Modules
	local Roact = require(Modules.Common.Roact)
	local Rodux = require(Modules.Common.Rodux)
	local PlacesListContextualMenu = require(Modules.LuaApp.Components.Home.PlacesListContextualMenu)
	local User = require(Modules.LuaApp.Models.User)
	local mockServices = require(Modules.LuaApp.TestHelpers.mockServices)
	local AppReducer = require(Modules.LuaApp.AppReducer)
	local FormFactor = require(Modules.LuaApp.Enum.FormFactor)

	local mockTabletStore = Rodux.Store.new(AppReducer, {
		TabBarVisible = true,
		TopBar = {
			topBarHeight = 20,
		},
		FormFactor = FormFactor.TABLET,
		ScreenSize = Vector2.new(300, 200),
	})

	local mockPhoneStore = Rodux.Store.new(AppReducer, {
		TabBarVisible = true,
		TopBar = {
			topBarHeight = 20,
		},
		FormFactor = FormFactor.PHONE,
		ScreenSize = Vector2.new(300, 200),
	})

	local listOfFriends = {
		User.fromData(1, "Hedonism Bot", true),
		User.fromData(2, "Hypno Toad", true),
		User.fromData(4, "Pazuzu", true),
		User.fromData(6, "Lrrr", true),
	}

	it("should create and destroy without errors", function()
		-- Tablet
		local element = mockServices({
			ContextualMenu = Roact.createElement(PlacesListContextualMenu, {
				friends = listOfFriends,
				parentCardSize = Vector2.new(30, 30),
				getParentCardPosition = function() return Vector2.new(0, 0) end,
			}),
		}, {
			includeStoreProvider = true,
			store = mockTabletStore,
		})

		local instance = Roact.mount(element)
		Roact.unmount(instance)

		-- Phone
		element = mockServices({
			ContextualMenu = Roact.createElement(PlacesListContextualMenu, {
				friends = listOfFriends,
				parentCardSize = Vector2.new(30, 30),
				getParentCardPosition = function() return Vector2.new(0, 0) end,
			}),
		}, {
			includeStoreProvider = true,
			store = mockPhoneStore,
		})

		instance = Roact.mount(element)
		Roact.unmount(instance)
	end)
end