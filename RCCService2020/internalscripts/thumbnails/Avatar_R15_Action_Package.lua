-- Avatar_R15_Action v doi doi doi
-- Generates thumbnails for packages and body part

local baseUrl, characterAppearanceUrl, fileExtension, x, y, assetsParam = ...

local ThumbnailGenerator = game:GetService("ThumbnailGenerator")
ThumbnailGenerator:AddProfilingCheckpoint("ThumbnailScriptStarted")

pcall(function() game:GetService("ContentProvider"):SetBaseUrl(baseUrl) end)
game:GetService("ScriptContext").ScriptsDisabled = true
game:GetService("UserInputService").MouseIconEnabled = false

local player = game:GetService("Players"):CreateLocalPlayer(0)
player.CharacterAppearance = characterAppearanceUrl
player:LoadCharacterBlocking()

ThumbnailGenerator:AddProfilingCheckpoint("PlayerCharacterLoaded")

local poseAnimationId = "http://bbblox.org/asset/?id=532421348"
local function getJointBetween(part0, part1)
    for _, obj in pairs(part1:GetChildren()) do
        if obj:IsA("Motor6D") and obj.Part0 == part0 then
            return obj
        end
    end
end

local function applyKeyframe(character, poseKeyframe)
    local function recurApplyPoses(parentPose, poseObject)
        if poseObject:IsA("Pose") then
            if parentPose then
                local parentPart = character:FindFirstChild(parentPose.Name)
                local childPart = character:FindFirstChild(poseObject.Name)

                if parentPart and childPart and parentPart:IsA("BasePart") and childPart:IsA("BasePart") then
                    local joint = getJointBetween(parentPart, childPart)
                    if joint and poseObject.Weight ~= 0 then
                        joint.C1 = poseObject.CFrame:inverse() + joint.C1.p
                    end
                end
            end

            for _, subPose in pairs(poseObject:GetSubPoses()) do
                recurApplyPoses(poseObject, subPose)
            end
        end
    end

    for _, poseObj in pairs(poseKeyframe:GetPoses()) do
        if poseObj:IsA("Pose") then
            recurApplyPoses(nil, poseObj)
        end
    end
end

local function applyR15Pose(character)
    local poseKeyframeSequence = game:GetService("KeyframeSequenceProvider"):GetKeyframeSequence(poseAnimationId)
    local poseKeyframe = poseKeyframeSequence:GetKeyframes()[1]
    applyKeyframe(character, poseKeyframe)
end

local function findAttachmentsRecur(parent, resultTable, returnDictionary)
    for _, obj in pairs(parent:GetChildren()) do
        if obj:IsA("Attachment") then
            if returnDictionary then
                resultTable[obj.Name] = obj
            else
                resultTable[#resultTable + 1] = obj
            end
        elseif not obj:IsA("Tool") and not obj:IsA("Accoutrement") then
            findAttachmentsRecur(obj, resultTable, returnDictionary)
        end
    end
end

local function findAttachmentsInTool(tool)
    local attachments = {}
    findAttachmentsRecur(tool, attachments, false)
    return attachments
end

local function findAttachmentsInCharacter(character)
    local attachments = {}
    findAttachmentsRecur(character, attachments, true)
    return attachments
end

local function weldAttachments(attach1, attach2)
    local weld = Instance.new("Weld")
    weld.Part0 = attach1.Parent
    weld.Part1 = attach2.Parent
    weld.C0 = attach1.CFrame
    weld.C1 = attach2.CFrame
    weld.Parent = attach1.Parent
    return weld
end

local function findFirstMatchingAttachment(model, name)
    for _, child in pairs(model:GetChildren()) do
        if child:IsA("Attachment") and child.Name == name then
            return child
        elseif not child:IsA("Accoutrement") and not child:IsA("Tool") then
            local foundAttachment = findFirstMatchingAttachment(child, name)
            if foundAttachment then
                return foundAttachment
            end
        end
    end
end

local function doR15ToolPose(character, humanoid, tool)
    local characterAttachments = findAttachmentsInCharacter(character)
    local toolAttachments = findAttachmentsInTool(tool)
    local foundAttachments = false
    for _, attachment in pairs(toolAttachments) do
        local matchingAttachment = characterAttachments[attachment.Name]
        if matchingAttachment then
            foundAttachments = true
            weldAttachments(matchingAttachment, attachment)
        end
    end

    if foundAttachments then
        tool.Parent = character
        applyR15Pose(character)

        local toolPose = tool:FindFirstChild("ThumbnailPose")
        if toolPose and toolPose:IsA("Keyframe") then
            applyKeyframe(character, toolPose)
        end
    else
        tool.Parent = nil
        local rightShoulderJoint = getJointBetween(character.UpperTorso, character.RightUpperArm)
        if rightShoulderJoint then
            rightShoulderJoint.C1 = rightShoulderJoint.C1 *  CFrame.new(0, 0, 0, 1, 0, 0, 0, 0, -1, 0, 1, 0):inverse()
        end
        if tool:FindFirstChild("Handle") then
            local attachment = findFirstMatchingAttachment(character, "RightGripAttachment")
            if attachment then
                tool.Handle.CFrame = attachment.Parent.CFrame * attachment.CFrame * tool.Grip:inverse()
            end
        end
        humanoid:EquipTool(tool)
    end
end

local function applyAssetsToCharacter(character, humanoid, assetIds)
    local InsertService = game:GetService("InsertService")

    for _, assetId in ipairs(assetIds) do
        if type(assetId) == "number" and assetId > 0 then
            local success, assetModel = pcall(function()
                return InsertService:LoadAsset(assetId)
            end)
            
            if success and assetModel then
                for _, acc in pairs(assetModel:GetChildren()) do
                    if acc:IsA("Accessory") then
                        acc:Clone().Parent = character
                    elseif acc:IsA("Decal") then
                        local head = character:FindFirstChild("Head")
                        if head then
                            for _, child in pairs(head:GetChildren()) do
                                if child:IsA("Decal") then
                                    child:Destroy()
                                end
                            end
                            acc:Clone().Parent = head
                        end
                    end
                end

                local tool = assetModel:FindFirstChildOfClass("Tool")
                if tool then
                    tool.Parent = character
                    if humanoid.RigType == Enum.HumanoidRigType.R15 then
                        doR15ToolPose(character, humanoid, tool)
                    else
                        humanoid:EquipTool(tool)
                    end
                end

                local function parentPartsRecursively(parent)
                    for _, obj in pairs(parent:GetChildren()) do
                        if obj:IsA("BasePart") then
                            obj:Clone().Parent = character
                        elseif obj:IsA("Model") then
                            parentPartsRecursively(obj)
                        end
                    end
                end
                parentPartsRecursively(assetModel)

				local r6Folder = assetModel:FindFirstChild("R6")
				if r6Folder then
					for _, child in ipairs(r6Folder:GetChildren()) do
						if child:IsA("CharacterMesh") then
							child:Clone().Parent = character
						end
					end
				end
            else
                warn("[DEBUG] Failed to load asset: " .. tostring(assetId))
            end
        end
    end
end

local character = player.Character
if character then
    local humanoid = character:FindFirstChildOfClass("Humanoid")
    if humanoid then
        local validAssets = {}
        for _, id in ipairs(assetsParam) do
            local num = tonumber(id)
            if num and num > 0 then
                table.insert(validAssets, num)
            end
        end

        applyAssetsToCharacter(character, humanoid, validAssets)

        if humanoid.RigType == Enum.HumanoidRigType.R15 then
            local tool = character:FindFirstChildOfClass("Tool")
            if not tool then
                applyR15Pose(character)
            end
        elseif humanoid.RigType == Enum.HumanoidRigType.R6 then
            local tool = character:FindFirstChildOfClass("Tool")
            if tool then
                character.Torso["Right Shoulder"].CurrentAngle = math.rad(90)
            end
        end
    end
end

local result, requestedUrls = ThumbnailGenerator:Click(fileExtension, x, y, true)
ThumbnailGenerator:AddProfilingCheckpoint("ThumbnailGenerated")

return result, requestedUrls
