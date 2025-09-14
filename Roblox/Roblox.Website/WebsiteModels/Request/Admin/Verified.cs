using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Roblox.Website.WebsiteModels.Admin.Verified;

public class VerifiedReq
{
	public long userId { get; set; }
}