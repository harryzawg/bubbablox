local Modules = game:GetService("CoreGui").RobloxGui.Modules
local Common = Modules.Common

local Roact = require(Common.Roact)

local ThemeProvider = Roact.Component:extend("ThemeProvider")

function ThemeProvider:init(props)
    local theme = props.theme
    self._context.AppTheme = theme
end

function ThemeProvider:render()
    return Roact.oneChild(self.props[Roact.Children])
end

return ThemeProvider