local Modules = game:GetService("CoreGui").RobloxGui.Modules
local AESetAvatarSettings = require(Modules.LuaApp.Actions.AEActions.AESetAvatarSettings)
local AEConstants = require(Modules.LuaApp.Components.Avatar.AEConstants)
local Immutable = require(Modules.Common.Immutable)

return function(state, action)
	state = state or {
		[AEConstants.AvatarSettings.proportionsAndBodyTypeEnabledForUser] = false,
		[AEConstants.AvatarSettings.minDeltaBodyColorDifference] = 0
	}

	if action.type == AESetAvatarSettings.name then
		state[AEConstants.AvatarSettings.proportionsAndBodyTypeEnabledForUser] = action.proportionsAndBodyTypeEnabled
		state = Immutable.Set(state,
			AEConstants.AvatarSettings.minDeltaBodyColorDifference, action.minimumDeltaEBodyColorDifference)
		return state
	end

	return state
end