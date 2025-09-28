using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Exceptions;
using Roblox.Logging;
using Roblox.Services;
using Roblox.Website.Middleware;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using MVC = Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Friends;
using Roblox.Models;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class Thumbs : ControllerBase 
    {		
		public enum ThumbnailType
        {
            UserHeadshot = 1,
            UserAvatar,
            Asset,
            PlaceIcon
        }
		
		public class ThumbnailEntry
		{
			public long targetId { get; set; }
			public ThumbnailState state { get; set; }
			public string? imageUrl { get; set; }
			public string version { get; set; } = "TN3";
		}
		
		public class BatchRequestEntry
		{
			public string requestId { get; set; }
			public string type { get; set; }
			public long targetId { get; set; }
		}
		
		public enum ThumbnailState
		{
			Error = 1,
			Completed,
			InReview,
			Pending,
			Blocked,
			TemporarilyUnavailable
			// 'Error', 'Completed', 'InReview', 'Pending', 'Blocked', 'TemporarilyUnavailable'],
		}
		
		private async Task<IEnumerable<dynamic>> MultiGetThumbnailsGeneric(
			List<BatchRequestEntry> entries, 
			string typeName, 
			Func<IEnumerable<long>, Task<IEnumerable<Roblox.Dto.Thumbnails.ThumbnailEntry>>> getThumbnailsFunc)
		{
			var result = new List<dynamic>();
			var targetIds = entries.Where(x => x.type == typeName).Select(x => x.targetId).ToList();
			
			if (targetIds.Any())
			{
				var thumbnails = await getThumbnailsFunc(targetIds);
				foreach (var thumbnail in thumbnails)
				{
					var imageUrl = thumbnail.imageUrl;
					if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
					{
						imageUrl = Configuration.BaseUrl + imageUrl;
					}
					
					result.Add(new
					{
						requestId = entries.First(x => x.targetId == thumbnail.targetId && x.type == typeName).requestId,
						errorCode = 0,
						errorMessage = "",
						targetId = thumbnail.targetId,
						state = (ThumbnailState)thumbnail.state,
						imageUrl = imageUrl
					});
				}
			}
			
			return result;
		}
						
		private async Task<MVC.RedirectResult> GetThumbnailUrl(long id, ThumbnailType type)
		{
			var authUser18Plus = userSession != null && await services.users.Is18Plus(userSession.userId);
			if (!authUser18Plus)
			{
				var avatar18Plus = await services.avatar.IsUserAvatar18Plus(id);
				if (avatar18Plus)
					return new MVC.RedirectResult("/img/blocked.png", false);
			}

			List<ThumbnailEntry> result = null;

			switch (type)
			{
				case ThumbnailType.UserHeadshot:
					var headshots = await services.thumbnails.GetUserHeadshots(new[] { id });
					result = headshots.Select(x => new ThumbnailEntry
					{
						targetId = x.targetId,
						state = (ThumbnailState)x.state,
						imageUrl = x.imageUrl,
						version = x.version
					}).ToList();
					break;
				case ThumbnailType.UserAvatar:
					var avatars = await services.thumbnails.GetUserThumbnails(new[] { id });
					result = avatars.Select(x => new ThumbnailEntry
					{
						targetId = x.targetId,
						state = (ThumbnailState)x.state,
						imageUrl = x.imageUrl,
						version = x.version
					}).ToList();
					break;
				case ThumbnailType.Asset:
					var assets = await services.thumbnails.GetAssetThumbnails(new[] { id });
					result = assets.Select(x => new ThumbnailEntry
					{
						targetId = x.targetId,
						state = (ThumbnailState)x.state,
						imageUrl = x.imageUrl,
						version = x.version
					}).ToList();
					break;
				case ThumbnailType.PlaceIcon:
					long universeId = await services.games.GetUniverseId(id);
					var gameIcons = await services.thumbnails.GetGameIcons(new[] { universeId });
					result = gameIcons.Select(x => new ThumbnailEntry
					{
						targetId = x.targetId,
						state = (ThumbnailState)x.state,
						imageUrl = x.imageUrl,
						version = x.version
					}).ToList();
					break;
			}

			var imageUrl = result?.FirstOrDefault()?.imageUrl ?? "/img/placeholder.png";
			return new MVC.RedirectResult(imageUrl, false);
		}

        [HttpGetBypass("avatar-thumbnail/image")]
        public async Task<MVC.RedirectResult> GetAvatarThumbnail(long userId, string? username)
        {
            if (username != null)
            {
                try
                {
                    userId = await services.users.GetUserIdFromUsername(username);
                }
                catch (Exception)
                {
                    return new MVC.RedirectResult("/img/blocked.png", false);
                }
            }
            return await GetThumbnailUrl(userId, ThumbnailType.UserAvatar);
        }
		
		[HttpGet("thumbs/avatar.ashx")]
		public async Task<RedirectResult> GetAvatarThumbnail(long userId)
		{
			var authUser18Plus = userSession != null && await services.users.Is18Plus(userSession.userId);
			if (!authUser18Plus)
			{
				var avatar18Plus = await services.avatar.IsUserAvatar18Plus(userId);
				if (avatar18Plus)
					return new RedirectResult("/img/blocked.png", false);
			}

			var result = (await services.thumbnails.GetUserThumbnails(new[] {userId})).ToList();
			
			if (result.Count == 0)
				return new RedirectResult("/img/placeholder.png", false);
			
			var safeUrl = result[0].imageUrl ?? "/img/placeholder.png";
			return new RedirectResult(safeUrl, false);
		}

        //headshot stuff
        [HttpGetBypass("headshot-thumbnail/image")]
        public async Task<MVC.RedirectResult> GetAvatarHeadShot(long userId)
        {
            return await GetThumbnailUrl(userId, ThumbnailType.UserHeadshot);
        }
		
		[HttpGet("thumbs/avatar-headshot.ashx")]
		public async Task<RedirectResult> GetAvatarHeadShotAshx(long userId)
		{
			var authUser18Plus = userSession != null && await services.users.Is18Plus(userSession.userId);
			if (!authUser18Plus)
			{
				var avatar18Plus = await services.avatar.IsUserAvatar18Plus(userId);
				if (avatar18Plus)
					return new RedirectResult("/img/blocked.png", false);
			}

			var result = (await services.thumbnails.GetUserHeadshots(new[] {userId})).ToList();
			if (result.Count == 0)
				return new RedirectResult("/img/placeholder.png", false);
			return new RedirectResult(result[0].imageUrl ?? "/img/placeholder.png", false);
		}

        [HttpGetBypass("Thumbs/PlaceIcon.ashx")]
        [HttpGetBypass("Thumbs/GameIcon.ashx")]
        public async Task<MVC.RedirectResult> GetGameIcon(long assetId)
        {
            return await GetThumbnailUrl(assetId, ThumbnailType.PlaceIcon);
        }

        [HttpGet("asset-thumbnail/image")]
        [HttpGetBypass("Game/Tools/ThumbnailAsset.ashx")]
        public async Task<MVC.RedirectResult> GetAssetThumbnail(long assetId, long? aid)
        {        
            if(aid != null)
                assetId = (long)aid;
            return await GetThumbnailUrl(assetId, ThumbnailType.Asset);
        }
		
		[HttpGet("icons/asset.ashx")]
		public async Task<RedirectResult> GetAssetIcon([Required] long assetId)
		{
			var authUser18Plus = userSession != null && await services.users.Is18Plus(userSession.userId);
			if (!authUser18Plus)
			{
				var asset18Plus = await services.assets.Is18Plus(assetId);
				if (asset18Plus)
					return new RedirectResult("/img/blocked.png", false);
			}
			
			var universe = (await services.games.MultiGetPlaceDetails(new[] {assetId})).First();
			var result = (await services.thumbnails.GetGameIcons(new[] {universe.universeId})).ToList();

			if (result.Count == 0 || result[0].imageUrl == null)
				return new RedirectResult("/img/placeholder.png", false);
			return new RedirectResult(result[0].imageUrl ?? "/img/placeholder.png", false);
		}
		
        [HttpGetBypass("avatar-thumbnail/json")]
        public async Task<dynamic> GetAvatarThumbnailJson([Required] long userId)
        {
            var result = (await services.thumbnails.GetUserThumbnails(new[] {userId})).ToList();
            return new
            {
                Url = $"{Configuration.BaseUrl}{result[0].imageUrl}",
                Final = true,
                SubstitutionType = 0
            };
        }

        [HttpGetBypass("asset-thumbnail/json")]
        public async Task<dynamic> GetAssetThumbnailJson([Required] long assetId)
        {
            var result = (await services.thumbnails.GetAssetThumbnails(new[] {assetId})).ToList();
            return new
            {
                Url = $"{Configuration.BaseUrl}{result[0].imageUrl}",
                Final = true,
                SubstitutionType = 0
            };
        }     

		[HttpGetBypass("asset-gameicon/multiget")]
		public async Task<dynamic> GetGameIconMultiGet([MVC.FromQuery] List<long> universeId)
		{
			var gameIcons = await services.thumbnails.GetGameIcons(universeId);
			return gameIcons.Select(x => new
			{
				x.targetId,
				state = (ThumbnailState)x.state,
				imageUrl = x.imageUrl,
				version = x.version
			}).ToList();
		}

		[HttpGetBypass("v1/games/icons")]
		public async Task<RobloxCollection<ThumbnailEntry>> GetGameIcons(string universeIds)
		{
			var parsed = universeIds.Split(",").Select(long.Parse).Distinct().ToList();
			if (parsed.Count is > 200 or < 0) throw new BadRequestException();
			
			var result = await services.thumbnails.GetGameIcons(parsed);
			var result2 = result.Select(thumbnail => new ThumbnailEntry
			{
				targetId = thumbnail.targetId,
				imageUrl = Configuration.BaseUrl + thumbnail.imageUrl,
				state = (ThumbnailState)thumbnail.state,
			}).ToList();
			
			return new()
			{
				data = result2,
			};
		}

		[HttpGet("v1/users/avatar-headshot")]
		public async Task<RobloxCollection<ThumbnailEntry>> GetMultiHeadshot(string userIds)
		{
			var parsed = userIds.Split(",").Select(long.Parse).Distinct().ToList();
			if (parsed.Count is > 200 or < 0) throw new BadRequestException();
			
			var result = (await services.thumbnails.GetUserHeadshots(parsed)).ToList();
			var result2 = result.Select(x => new ThumbnailEntry
			{
				targetId = x.targetId,
				state = (ThumbnailState)x.state,
				imageUrl = x.imageUrl,
				version = x.version
			}).ToList();
			
			var authUser18Plus = userSession != null && await services.users.Is18Plus(userSession.userId);
			if (!authUser18Plus)
			{
				foreach (var item in result2)
				{
					if (item.imageUrl is null) continue;

					var avatar18Plus = await services.avatar.IsUserAvatar18Plus(item.targetId);
					if (avatar18Plus)
					{
						item.state = ThumbnailState.Blocked;
						item.imageUrl = "/img/blocked.png";
					}
					else
					{
						item.imageUrl = Configuration.BaseUrl + item.imageUrl;
					}
				}
			}
			else
			{
				foreach (var item in result2)
				{
					if (item.imageUrl is null) continue;
					item.imageUrl = Configuration.BaseUrl + item.imageUrl;
				}
			}
			return new()
			{
				data = result2,
			};
		}
		
		[HttpPostBypass("v1/batch")]
		public async Task<dynamic> BatchThumbnailsRequest()
		{
			try
			{
				bool isGzip = Request.Headers["Content-Encoding"].ToString() == "gzip";
				
				IEnumerable<BatchRequestEntry> requestEntries;
				var tasks = new List<Task<IEnumerable<dynamic>>>();
				
				if (isGzip)
				{
					using (var decompressedStream = new MemoryStream())
					{
						using (var requestStream = Request.Body)
						{
							using (var gzipStream = new GZipStream(requestStream, CompressionMode.Decompress))
							{
								Console.WriteLine($"[BatchThumbnails] decompressing gzip stream");
								await gzipStream.CopyToAsync(decompressedStream);
							}
						}
						decompressedStream.Seek(0, SeekOrigin.Begin);

						using (var reader = new StreamReader(decompressedStream, Encoding.UTF8))
						{
							var json = await reader.ReadToEndAsync();
							
							try
							{
								requestEntries = JsonConvert.DeserializeObject<IEnumerable<BatchRequestEntry>>(json);
								Console.WriteLine($"[BatchThumbnails] deserialized {requestEntries?.Count() ?? 0} entrie(s) from gzip");
							}
							catch (Exception deserializeEx)
							{
								Console.WriteLine($"[BatchThumbnails] JSON deserialization failed: {deserializeEx.Message}");
								Console.WriteLine($"[BatchThumbnails] StackTrace: {deserializeEx.StackTrace}");
								throw;
							}
						}
					}
				}
				else
				{
					using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
					{
						var json = await reader.ReadToEndAsync();
						
						try
						{
							requestEntries = JsonConvert.DeserializeObject<IEnumerable<BatchRequestEntry>>(json);
							Console.WriteLine($"[BatchThumbnails] deserialized {requestEntries?.Count() ?? 0} JSON entrie(s)");
						}
						catch (Exception deserializeEx)
						{
							Console.WriteLine($"[BatchThumbnails] JSON deserialization failed: {deserializeEx.Message}");
							Console.WriteLine($"[BatchThumbnails] StackTrace: {deserializeEx.StackTrace}");
							throw;
						}
					}
				}

				var thumbs = requestEntries.ToList();
				Console.WriteLine($"[BatchThumbnails] processing {thumbs.Count} thumbnail request(s)");
				
				var taskDefinitions = new List<(string name, Func<IEnumerable<long>, Task<IEnumerable<Roblox.Dto.Thumbnails.ThumbnailEntry>>> func)>
				{
					("Avatar", services.thumbnails.GetUserThumbnails),
					("AvatarThumbnail", services.thumbnails.GetUserThumbnails),
					("AvatarHeadShot", services.thumbnails.GetUserHeadshots),
					("GameIcon", services.thumbnails.GetGameIcons),
					("GameThumbnail", services.thumbnails.GetAssetThumbnails),
					("Asset", services.thumbnails.GetAssetThumbnails),
					("AssetThumbnail", services.thumbnails.GetAssetThumbnails),
					("GroupIcon", services.thumbnails.GetGroupIcons),
				};

				var taskList = new List<Task<IEnumerable<dynamic>>>();
				
				foreach (var (name, func) in taskDefinitions)
				{
					var targetIds = thumbs.Where(x => x.type == name).Select(x => x.targetId).ToList();
					if (targetIds.Any())
					{
						Console.WriteLine($"[BatchThumbnails] queueing {name} task with target IDs: {string.Join(", ", targetIds)}");
						taskList.Add(MultiGetThumbnailsGeneric(thumbs, name, func));
					}
					else
					{
						//Console.WriteLine($"[BatchThumbnails] no entries for {name}, skipping task");
					}
				}

				Console.WriteLine($"[BatchThumbnails] starting {taskList.Count} tasks");
				var stopwatch = Stopwatch.StartNew();
				
				try
				{
					var allResults = await Task.WhenAll(taskList);
					stopwatch.Stop();

					foreach (var result in allResults)
					{
						if (result != null)
						{
							foreach (var item in result)
							{
								Console.WriteLine($"[BatchThumbnails] res: {JsonConvert.SerializeObject(item)}");
							}
						}
					}

					return new RobloxCollection<dynamic>()
					{
						data = allResults.SelectMany(x => x),
					};
				}
				catch (Exception taskEx)
				{
					stopwatch.Stop();
					Console.WriteLine($"[BatchThumbnails] batch exec failed after {stopwatch.ElapsedMilliseconds}ms: {taskEx.Message}");
					Console.WriteLine($"[BatchThumbnails] trace: {taskEx.StackTrace}");
					throw;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[BatchThumbnails] somethimg failed: {ex.Message}");
				Console.WriteLine($"[BatchThumbnails] trace: {ex.StackTrace}");
				throw;
			}
		}
	}
}	