return function()
	local Modules = game:GetService("CoreGui").RobloxGui.Modules
	local Roact = require(Modules.Common.Roact)
	local Rodux = require(Modules.Common.Rodux)
	local mockServices = require(Modules.LuaApp.TestHelpers.mockServices)

	local AppReducer = require(Modules.LuaApp.AppReducer)
	local JoinableFriendEntry = require(Modules.LuaApp.Components.Home.JoinableFriendEntry)
	local User = require(Modules.LuaApp.Models.User)

	local storeWithDummyFriends = Rodux.Store.new(AppReducer)

	it("should create and destroy without errors", function()
		local element = mockServices({
			Entry = Roact.createElement(JoinableFriendEntry, {
				user = User.fromData(1, "Hedonism Bot", true),
				entryHeight = 30,
				entryWidth = 100,
			})
		}, {
			includeStoreProvider = true,
			store = storeWithDummyFriends,
		})

		local instance = Roact.mount(element)
		Roact.unmount(instance)
	end)

end