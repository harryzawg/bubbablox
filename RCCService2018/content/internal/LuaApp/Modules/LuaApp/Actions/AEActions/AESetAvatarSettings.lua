local Modules = game:GetService("CoreGui"):FindFirstChild("RobloxGui").Modules
local Action = require(Modules.Common.Action)

return Action(script.Name, function(proportionsAndBodyTypeEnabled, minimumDeltaEBodyColorDifference)
	return {
		proportionsAndBodyTypeEnabled = proportionsAndBodyTypeEnabled,
		minimumDeltaEBodyColorDifference = minimumDeltaEBodyColorDifference
	}
end)
