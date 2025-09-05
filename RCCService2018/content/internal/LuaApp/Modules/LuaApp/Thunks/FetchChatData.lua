local CoreGui = game:GetService("CoreGui")
local Modules = CoreGui.RobloxGui.Modules

local LuaChat = Modules.LuaChat

local ConversationActions = require(LuaChat.Actions.ConversationActions)
local FetchChatEnabled = require(LuaChat.Actions.FetchChatEnabled)
local GetFriendCount = require(LuaChat.Actions.GetFriendCount)
local SetAppLoaded = require(LuaChat.Actions.SetAppLoaded)

local Constants = require(LuaChat.Constants)

local LuaChatCheckIsChatEnabled = settings():GetFFlag("LuaChatCheckIsChatEnabled")

return function(onEnabled)
	if LuaChatCheckIsChatEnabled then
		return function(store)
			store:dispatch(FetchChatEnabled(function(chatEnabled)
				if chatEnabled then
					store:dispatch(ConversationActions.GetUnreadConversationCountAsync())
					store:dispatch(GetFriendCount())
					store:dispatch(
						ConversationActions.GetLocalUserConversationsAsync(1, Constants.PageSize.GET_CONVERSATIONS)
					):andThen(function()
						store:dispatch(SetAppLoaded(true))
					end)
				end

				if onEnabled then
					onEnabled(chatEnabled)
				end
			end))
		end
	else
		return function(store)
			store:dispatch(FetchChatEnabled())
			store:dispatch(ConversationActions.GetUnreadConversationCountAsync())
			store:dispatch(
				ConversationActions.GetLocalUserConversationsAsync(1, Constants.PageSize.GET_CONVERSATIONS)
			):andThen(function()
				store:dispatch(SetAppLoaded(true))
			end)
		end
	end
end