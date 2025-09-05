return function()
	local Modules = game:GetService("CoreGui").RobloxGui.Modules
	local AEEquippedAssets = require(script.Parent.AEEquippedAssets)
	local AEToggleEquipAsset = require(Modules.LuaApp.Actions.AEActions.AEToggleEquipAsset)
	local AEEquipOutfit = require(Modules.LuaApp.Actions.AEActions.AEEquipOutfit)
	local AEConstants = require(Modules.LuaApp.Components.Avatar.AEConstants)

	it("should be unchanged by other actions", function()
		local oldState = AEEquippedAssets(nil, {})
		local newState = AEEquippedAssets(oldState, { type = "not a real action" })
		expect(oldState).to.equal(newState)
	end)

	describe("AEToggleEquipAsset", function()
		it("should equip an asset with AEToggleEquipAsset", function()
			local newState = AEEquippedAssets(nil, AEToggleEquipAsset(1, 333))
			expect(newState[1][1]).to.equal(333)
		end)

		it("should unequip an asset with AEToggleEquipAsset", function()
			local newState = AEEquippedAssets(nil, AEToggleEquipAsset(1, 333))

			expect(newState[1][1]).to.equal(333)

			newState = AEEquippedAssets(newState, AEToggleEquipAsset(1, 333))
			expect(newState[1][1]).never.to.be.ok()
		end)
	end)

	describe("AEEquipOutfit", function()
		it("should equip an outfit with AEEquipOutfit", function()
			local outfit = {
				[1] = 1,
				[2] = 2,
				[3] = 3,
			}
			local newState = AEEquippedAssets(nil, AEEquipOutfit(outfit))

			expect(newState[1]).to.equal(1)
			expect(newState[2]).to.equal(2)
			expect(newState[3]).to.equal(3)
		end)

		it("should unequip all assets that are not part of the outfit.", function()
			local outfit = {
				[1] = 1,
				[2] = 2,
				[3] = 3,
			}
			local newState = AEEquippedAssets(nil, AEEquipOutfit(outfit))

			local newOutfit = {
				[1] = 1,
				[5] = 2,
			}

			newState = AEEquippedAssets(newState, AEEquipOutfit(newOutfit))

			expect(newState[1]).to.equal(1)
			expect(#newState[2]).to.equal(0) -- There should be no more assets of typeId 2 & 3.
			expect(#newState[3]).to.equal(0)
			expect(newState[5]).to.equal(2)
		end)
	end)

	describe("AEEquipOutfit and AEToggleEquipAsset", function()
		it("should equip multiple hats, and only keep 3", function()
			local hats = {
				[AEConstants.AssetTypes.Hat] = {1, 2, 3}
			}

			local newState = AEEquippedAssets(nil, AEEquipOutfit(hats))
			expect(#newState[AEConstants.AssetTypes.Hat]).to.equal(3)

			newState = AEEquippedAssets(newState, AEToggleEquipAsset(AEConstants.AssetTypes.Hat, 4))
			expect(#newState[AEConstants.AssetTypes.Hat]).to.equal(3)

			expect(newState[AEConstants.AssetTypes.Hat][1]).to.equal(4)
			expect(newState[AEConstants.AssetTypes.Hat][3]).to.equal(2)

			-- Unequip a hat
			newState = AEEquippedAssets(newState, AEToggleEquipAsset(AEConstants.AssetTypes.Hat, 4))
			expect(#newState[AEConstants.AssetTypes.Hat]).to.equal(2)
		end)
	end)
end