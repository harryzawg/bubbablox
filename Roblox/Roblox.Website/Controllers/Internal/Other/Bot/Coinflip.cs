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

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class CoinflipBot : ControllerBase
    {
		private void ValidateBotAuth()
        {
	        if (Request.Headers["BB-botAPIkey"].ToString() != Roblox.Configuration.BotAuthorization)
	        {
		        throw new Exception("Internal");
	        }
        }
		
        [HttpGetBypass("botapi/discord/coinflip")]
        public async Task<dynamic> Coinflip([FromQuery] string ID, [FromQuery] string amount)
        {
            ValidateBotAuth();
            
            try
            {
                if (!long.TryParse(amount, out long Amount) || Amount <= 0)
                {
                    throw new RobloxException(400, 0, "Bad amount");
                }
				
				if (Amount < 1)
                {
                    throw new BadRequestException(0, $"Minimum flip amount is 1 Robux");
                }

                if (Amount > 500)
                {
                    throw new BadRequestException(0, $"Max flip amount is 500 Robux");
                }

                var userId = await services.users.GetUserIdFromDiscordId(ID);

                if (!await services.cooldown.TryCooldownCheck($"CF_cooldown:{userId}", TimeSpan.FromSeconds(4)))
                {
                    return new 
                    {
                        error = "You are on cooldown, please wait before flipping again."
                    };
                }

                if (!await services.cooldown.TryIncrementBucketCooldown($"CF_day:{userId}", 25, TimeSpan.FromDays(1)))
                {
                    return new 
                    { 
                        error = "You have reached the daily limit of 25 coinflips. Please try again tomorrow."
                    };
                }

                var RobuxBalance = await services.economy.GetUserBalance(userId);
                long CurrentBalance = RobuxBalance.robux;
				
                if (CurrentBalance < Amount)
                {
                    throw new RobloxException(400, 0, "You do not have enough Robux to flip this amount.");
                }
				
                await services.economy.DecrementCurrency(CreatorType.User, userId, CurrencyType.Robux, Amount);

                // 50/50
                var random = new Random();
                bool isHeads = random.Next(2) == 0; // 0 is heads, 1 is tails

                long Balance;
                string Message;

                if (isHeads)
                {
                    long winnings = Amount * 2;
                    await services.economy.IncrementCurrency(CreatorType.User, userId, CurrencyType.Robux, winnings);
                    Balance = CurrentBalance - Amount + winnings;
                    Message = $"You flipped heads and won {winnings} Robux! Your robux is now {Balance}";
                }
                else
                {
                    Balance = CurrentBalance - Amount;
                    Message = $"You flipped tails and lost. Your Robux is now {Balance}";
                }

                return new 
                {
                    Status = Message,
                    Won = isHeads,
                    Winnings = isHeads ? Amount * 2 : 0,
                    NewBalance = Balance
                };
            }
            catch (RecordNotFoundException)
            {
                throw new RobloxException(400, 0, "An account with your Discord ID was not found. Sign up at https://bbblox.org");
            }
            catch (RobloxException ex)
            {
                throw;
            }
            catch (BadRequestException ex)
            {
                throw new RobloxException(400, 0, ex.Message);
            }
			catch (Exception ex)
            {
                Console.WriteLine($"Coinflip error: {ex.Message}");
                throw new RobloxException(500, 0, "An error occurred, please try again later!");
            }
        }
    }
}