--[[
	Wraps around Rodux and applies a compatibility patch to deal with API
	breakages since the last Rodux upgrade.

	Things that have changed:
	* API is now camelCase instead of PascalCase to match standards
	* Thunks are no longer enabled by default
]]

local CorePackages = game:GetService("CorePackages")

local Rodux = require(CorePackages.Rodux)

local function getWarningMessage(funcNameCamelCase)
	return string.format(
		"Store:%s() has been deprecated, use %s()\n%s]",
		funcNameCamelCase:sub(1, 1):upper() .. funcNameCamelCase:sub(2),
		funcNameCamelCase,
		debug.traceback()
	)
end

local oldStoreNew = Rodux.Store.new
Rodux.Store.new = function(reducer, initialState)
	-- Thunks are no longer enabled by default, so enable them!
	local store = oldStoreNew(reducer, initialState, { Rodux.thunkMiddleware })

	-- 'changed' is created for every store
	store.Changed = store.changed

	-- Middleware work by overwriting the 'dispatch' method, so we have to set
	-- it here.
	store.Dispatch = function(...)
		warn(getWarningMessage("dispatch"))
		return store.dispatch(...)
	end

	return store
end

-- Create PascalCase versions of regular Store methods
Rodux.Store.GetState = function(...)
	warn(getWarningMessage("getState"))
	return Rodux.Store.getState(...)
end
Rodux.Store.Destruct = function(...)
	warn(getWarningMessage("destruct"))
	return Rodux.Store.destruct(...)
end
Rodux.Store.Flush = function(...)
	warn(getWarningMessage("flush"))
	return Rodux.Store.flush(...)
end

-- Rodux has a Signal implementation that's exposed via Store.changed.
local Signal = require(CorePackages.RoduxImpl.Signal)

local oldSignalConnect = Signal.connect
Signal.connect = function(...)
	local connection = oldSignalConnect(...)

	-- 'disconnect' is created for every connection.
	connection.Disconnect = function(...)
		warn(string.format(
			"Connection:Disconnect() has been deprecated, use Connection:disconnect()\n%s]",
			debug.traceback()
		))
		return connection.disconnect(...)
	end

	return connection
end

-- Create PascalCase versions of regular Signal methods
Signal.Connect = function(...)
	warn(string.format(
		"Signal:Connect() has been deprecated, use Signal:connect()\n%s]",
		debug.traceback()
	))
	return Signal.connect(...)
end
Signal.Fire = function(...)
	warn(string.format(
		"Signal:Fire() has been deprecated, use Signal:fire()\n%s]",
		debug.traceback()
	))
	return Signal.fire(...)
end

return Rodux