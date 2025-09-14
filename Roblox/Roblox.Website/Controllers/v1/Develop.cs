using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Games;
using Roblox.Exceptions;
using Roblox.Models;
using Roblox.Models.Assets;
using Roblox.Website.WebsiteModels.Catalog;

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/apisite/develop/v1")]
public class DevelopControllerV1 : ControllerBase
{
    [HttpGet("user/is-verified-creator")]
    public dynamic IsVerifiedCreator()
    {
        return new
        {
            isVerifiedCreator = true,
        };
    }

    [HttpGet("assets/genres")]
    public RobloxCollection<Models.Assets.Genre> GetAssetGenres()
    {
        return new RobloxCollection<Models.Assets.Genre>()
        {
            data = Enum.GetValues<Models.Assets.Genre>(),
        };
    }

    [HttpGet("assets")]
    public async Task<dynamic> MultiGetAssetInfo(string assetIds)
    {
        var splitIds = assetIds.Split(",").Select(long.Parse).ToList();
        if (splitIds.Count > 100) throw new BadRequestException();
        var details = await services.assets.MultiGetAssetDeveloperDetails(splitIds);
        return new
        {
            data = details,
        };
    }

	[HttpPatch("assets/{assetId:long}")]
	public async Task<IActionResult> UpdateAsset(long assetId, [Required, FromBody] UpdateAssetRequest request)
	{
		try
		{
			await services.assets.ValidatePermissions(assetId, safeUserSession.userId);

			await services.assets.UpdateAsset(assetId, request.description, request.name, request.genres.First(),
				request.isCopyingAllowed, request.enableComments);

			return Ok(new { success = true });
		}
		catch (Roblox.Services.Exceptions.PermissionException ex)
		{
			return StatusCode(403, new
			{
				errors = new[]
				{
					new { code = 0, message = ex.Message }
				}
			});
		}
		catch (ArgumentException ex)
		{
			return BadRequest(new
			{
				errors = new[]
				{
					new { code = 0, message = ex.Message }
				}
			});
		}
		catch (Exception ex)
		{
			return StatusCode(500, new
			{
				errors = new[]
				{
					new { code = 0, message = ex.Message }
				}
			});
		}
	}

    [HttpPatch("universes/{universeId:long}/max-player-count")]
    public async Task SetMaxPlayerCount(long universeId, [Required, FromBody] SetMaxPlayerCountRequest request)
    {
        var place = await services.games.GetRootPlaceId(universeId);
        await services.assets.ValidatePermissions(place, safeUserSession.userId);
        await services.games.SetMaxPlayerCount(place, request.maxPlayers);
    }
	
	[HttpPatch("universes/{universeId:long}/gear-permissions")]
	public async Task SetGearPermission(long universeId, [Required, FromBody] SetGearPermissionRequest request)
	{
		var place = await services.games.GetRootPlaceId(universeId);
		await services.assets.ValidatePermissions(place, safeUserSession.userId);
		await services.games.SetGearPermission(place, request.isEnabled);
	}
	
	[HttpPatch("universes/{universeId:long}/playable")]
	public async Task SetPlayabilityStatus(long universeId, [Required, FromBody] SetPlayableRequest request)
	{
		var place = await services.games.GetRootPlaceId(universeId);
		await services.assets.ValidatePermissions(place, safeUserSession.userId);
		await services.games.SetPlayable(place, request.isPlayable);
    }
	
	[HttpPatch("universes/{universeId:long}/year")]
	public async Task SetPlaceYear(long universeId, [Required, FromBody] SetYearReq request)
	{
		var place = await services.games.GetRootPlaceId(universeId);
		await services.assets.ValidatePermissions(place, safeUserSession.userId);
		await services.games.SetPlaceYear(place, request.year);
    }
	
	[HttpPatch("universes/{universeId:long}/rig-type")]
	public async Task SetRigType(long universeId, [Required, FromBody] SetRigTypeReq request)
	{
		var place = await services.games.GetRootPlaceId(universeId);
		await services.assets.ValidatePermissions(place, safeUserSession.userId);
		await services.games.SetRigType(place, request.rigType);
    }
}