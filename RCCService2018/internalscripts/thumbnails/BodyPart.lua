-- BodyPart v1.0.5
-- See http://wiki.roblox.com/index.php?title=R15_Compatibility_Guide#Package_Parts for details on how body parts work with R15

local assetUrl, baseUrl, fileExtension, x, y, R6RigUrl, customUrl = ...

pcall(function() game:GetService('ContentProvider'):SetBaseUrl(baseUrl) end) 
game:GetService('ScriptContext').ScriptsDisabled = true

local objects = game:GetObjects(assetUrl)
local useR15 = false
local useR15NewNames = false

for _, object in pairs(objects) do	
	if object:IsA("Folder") and (object.Name == "R15" or object.Name == "R15ArtistIntent") then
		useR15 = true
		break
	end
end

local R15RigUrl = "http://www.roblox.com/asset/?id=516159357" 
for _, object in pairs(objects) do
	if object:IsA("Folder") and object.Name == "R15ArtistIntent" then
		useR15NewNames = true
		R15RigUrl = "http://www.roblox.com/asset/?id=1664543044"
		break
	end
end

local mannequin = nil
if useR15 then
	mannequin = game:GetObjects(R15RigUrl)[1]
else
	mannequin = game:GetObjects(R6RigUrl)[1]	
end

mannequin.Humanoid.DisplayDistanceType = Enum.HumanoidDisplayDistanceType.None
mannequin.Parent = workspace

game:GetObjects(customUrl)[1].Parent = mannequin

function addFolderChildren(folder)
	for _, child in pairs(folder:GetChildren()) do
		local existingBodyPart = mannequin:FindFirstChild(child.Name)
		if existingBodyPart then
			existingBodyPart:Destroy()
		end
		child.Parent = mannequin	
	end
end

local r15FolderName = "R15"
if (useR15 and useR15NewNames) then
	r15FolderName = "R15ArtistIntent"
end

for _, object in pairs(objects) do
	if useR15 and object:IsA("Folder") and object.Name == r15FolderName then
		addFolderChildren(object)
	elseif not useR15 and object:IsA("Folder") and object.Name == "R6" then
		addFolderChildren(object)
	elseif not (object:IsA("Folder") and  string.find(object.Name, "R15")) then  -- There will now be MULTIPLE R15 Folders. Ignore the ones we didn't search for.
		object.Parent = mannequin
	end
end

local function buildJoint(parentAttachment, partForJointAttachment)
    local jointName = parentAttachment.Name:gsub("RigAttachment", "")
    local motor = partForJointAttachment.Parent:FindFirstChild(jointName)
    if not motor then
        motor = Instance.new("Motor6D")
    end
	motor.Name = jointName
 
    motor.Part0 = parentAttachment.Parent
    motor.Part1 = partForJointAttachment.Parent
 
	motor.C0 = parentAttachment.CFrame
    motor.C1 = partForJointAttachment.CFrame
 
    motor.Parent = partForJointAttachment.Parent
end
 
-- Builds an R15 rig from the attachments in the parts
function buildRigFromAttachments(currentPart, lastPart)
	local validSiblings = {}
	for _, sibling in pairs(currentPart.Parent:GetChildren()) do
		-- Don't find matching attachment in the current part being processed.
		-- Don't visit the last part visited again, this would cause an infinite loop.
		if sibling:IsA("BasePart") and sibling ~= currentPart and sibling ~= lastPart then
			table.insert(validSiblings, sibling)
		end
	end

	local function processRigAttachment(attachment)
		for _, sibling in pairs(validSiblings) do
			local matchingAttachment = sibling:FindFirstChild(attachment.Name)
			if matchingAttachment then
				buildJoint(attachment, matchingAttachment)
				buildRigFromAttachments(matchingAttachment.Parent, currentPart)
			end
		end
	end

	for _, object in pairs(currentPart:GetChildren()) do
		if object:IsA("Attachment") and string.find(object.Name, "RigAttachment") then
			processRigAttachment(object)
		end
	end
end

if useR15 then
	-- Build R15 rig
	local humanoidRootPart = mannequin:WaitForChild("HumanoidRootPart")
	humanoidRootPart.CFrame = CFrame.new(Vector3.new(0, 5, 0)) * CFrame.Angles(0, math.pi, 0)
	humanoidRootPart.Anchored = true
	buildRigFromAttachments(humanoidRootPart)
end

return game:GetService("ThumbnailGenerator"):Click(fileExtension, x, y, --[[hideSky = ]] true)