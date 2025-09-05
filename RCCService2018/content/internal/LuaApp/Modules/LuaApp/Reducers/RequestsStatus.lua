local Modules = game:GetService("CoreGui").RobloxGui.Modules

local GameSortTokenFetchingStatus = require(Modules.LuaApp.Reducers.GameSortTokenFetchingStatus)
local GameSortsStatus = require(Modules.LuaApp.Reducers.GameSortsStatus)
local SearchesInGamesStatus = require(Modules.LuaApp.Reducers.SearchesInGamesStatus)
local PlayabilityFetchingStatus = require(Modules.LuaApp.Reducers.PlayabilityFetchingStatus)

return function(state, action)
	state = state or {}

	return {
		GameSortTokenFetchingStatus = GameSortTokenFetchingStatus(state.GameSortTokenFetchingStatus, action),
		GameSortsStatus = GameSortsStatus(state.GameSortsStatus, action),
		SearchesInGamesStatus = SearchesInGamesStatus(state.SearchesInGamesStatus, action),
		PlayabilityFetchingStatus = PlayabilityFetchingStatus(state.PlayabilityFetchingStatus, action),
	}
end