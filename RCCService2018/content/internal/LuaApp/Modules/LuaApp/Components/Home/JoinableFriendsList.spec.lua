return function()
	local Modules = game:GetService("CoreGui").RobloxGui.Modules
	local Roact = require(Modules.Common.Roact)
	local Rodux = require(Modules.Common.Rodux)
	local RoactRodux = require(Modules.Common.RoactRodux)
	local mockServices = require(Modules.LuaApp.TestHelpers.mockServices)
	local MockId = require(Modules.LuaApp.MockId)

	local AppReducer = require(Modules.LuaApp.AppReducer)
	local JoinableFriendsList = require(Modules.LuaApp.Components.Home.JoinableFriendsList)
	local User = require(Modules.LuaApp.Models.User)
	local AddUser = require(Modules.LuaApp.Actions.AddUser)

	local listOfFriends = {
		User.fromData(1, "Hedonism Bot", true),
		User.fromData(2, "Hypno Toad", true),
		User.fromData(3, "Pazuzu", true),
		User.fromData(4, "Lrrr", true),
	}

	local function GetMockStore()
		return Rodux.Store.new(AppReducer, {
			Users = {
				["1"] = User.fromData(1, "Hedonism Bot", true),
				["2"] = User.fromData(2, "Hypno Toad", true),
				["3"] = User.fromData(3, "Pazuzu", true),
				["4"] = User.fromData(4, "Lrrr", true),
			},
		})
	end
	it("should create and destroy without errors", function()
		local element = mockServices({
			List = Roact.createElement(JoinableFriendsList, {
				maxHeight = 300,
				friends = listOfFriends,
				width = 100,
			})
		}, {
			includeStoreProvider = true,
			store = GetMockStore(),
		})

		local instance = Roact.mount(element)
		Roact.unmount(instance)
	end)

	it("should not modify the list of friends once it is created", function()
		local DummyParent = Roact.PureComponent:extend("DummyParent")

		function DummyParent:render()
			local users = self.props.users

			return Roact.createElement(JoinableFriendsList, {
				maxHeight = 300,
				friends = users,
				width = 100,
			})
		end

		DummyParent = RoactRodux.UNSTABLE_connect2(
			function(state, props)
				local allFriends = {}
				for _, user in pairs(state.Users) do
					if user.isFriend then
						allFriends[#allFriends + 1] = user
					end
				end
				return {
					users = allFriends,
				}
			end
		)(DummyParent)

		local mockStore = GetMockStore()

		local element = mockServices({
			DummyParent = Roact.createElement(DummyParent)
		}, {
			includeStoreProvider = true,
			store = mockStore,
		})

		local container = Instance.new("Folder")
		local instance = Roact.mount(element, container, "Test")

		local numberOfFriendsOnList = 0
		local friends = container.Test:GetChildren()

		for _, child in pairs(friends) do
			if string.find(child.Name, "Entry_") then
				numberOfFriendsOnList = numberOfFriendsOnList + 1
			end
		end
		expect(numberOfFriendsOnList).to.equal(4)

		mockStore:dispatch(AddUser(User.fromData(MockId(), "User Ignore", true)))

		numberOfFriendsOnList = 0
		friends = container.Test:GetChildren()

		for _, child in pairs(friends) do
			if string.find(child.Name, "Entry_") then
				numberOfFriendsOnList = numberOfFriendsOnList + 1
			end
		end
		expect(numberOfFriendsOnList).to.equal(4)

		Roact.unmount(instance)
	end)

end