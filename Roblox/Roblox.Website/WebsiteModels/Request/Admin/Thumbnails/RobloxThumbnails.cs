using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Roblox.Models;
using Roblox.Models.Assets;
using Roblox.Dto.Assets;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Website.WebsiteModels.Admin.Thumbnails;

	public class RobloxThumbnailBatchResponse
	{
		public RobloxThumbnailData[] data { get; set; }
	}

	public class RobloxThumbnailData
	{
		public string requestId { get; set; }
		public int errorCode { get; set; }
		public string errorMessage { get; set; }
		public long targetId { get; set; }
		public string state { get; set; }
		public string imageUrl { get; set; }
		public string version { get; set; }
	}