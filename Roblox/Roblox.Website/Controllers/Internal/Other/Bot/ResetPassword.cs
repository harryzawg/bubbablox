using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Website.Middleware;
using Roblox.Services.App.FeatureFlags;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using MVC = Microsoft.AspNetCore.Mvc;
using Roblox.Services;
using Roblox.Services.Exceptions;
using Roblox.Models.Users;
using Roblox.Models.Economy;
using Roblox.Dto.Assets;
using Roblox.Models.Assets;
using Roblox.Website.WebsiteModels.Asset;
using Roblox.Dto.Tickets;

namespace Roblox.Website.Controllers
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class ResetPasswordBot : ControllerBase
    {
        private void ValidateBotAuth()
        {
            if (Request.Headers["BB-botAPIkey"].ToString() != Roblox.Configuration.BotAuthorization)
            {
                throw new Exception("Internal");
            }
        }

        [HttpGetBypass("botapi/resetpassword")]
        public async Task<dynamic> ResetPassword(long userId)
        {
            ValidateBotAuth();
            string randomlyGeneratedPassword = (Guid.NewGuid().ToString().Replace("-", "") + Guid.NewGuid().ToString().Replace("-", "")).Substring(0, 32);
            await services.users.UpdatePassword(userId, newPassword: randomlyGeneratedPassword);
            return new
            {
                success = true,
                password = randomlyGeneratedPassword,
                message = "The password has been successfully reset. \nThe password has been sent in your DMs"
            };
        }
    }
}
        
