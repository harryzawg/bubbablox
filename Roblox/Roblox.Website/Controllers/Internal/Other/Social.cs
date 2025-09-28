using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Roblox.Exceptions;
using Roblox.Website.Middleware;
using Roblox.Services.App.FeatureFlags;
using MVC = Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Friends;
using Roblox.Models;
using Roblox.Services.Exceptions;
using Roblox.Website.Filters;
using Roblox.Dto.Users;
using Newtonsoft.Json;
using Roblox.Models.Users;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class Social : ControllerBase 
    {
		[HttpGetBypass("Users/ListStaff.ashx")]
        public async Task<IEnumerable<long>> GetStaffList()
        {
            return (await StaffFilter.GetStaff()).Where(c => c != 12);
        }

        [HttpGetBypass("Users/GetBanStatus.ashx")]
        public async Task<IEnumerable<dynamic>> MultiGetBanStatus(string userIds)
        {

            var ids = userIds.Split(",").Select(long.Parse).Distinct();
            var Result = new List<dynamic>();
#if DEBUG
            return ids.Select(c => new
            {
                userId = c,
                isBanned = false,
            });
#else
            var multiGetResult = await services.users.MultiGetAccountStatus(ids);
            foreach (var user in multiGetResult)
            {
                Result.Add(new
                {
                    userId = user.userId,
                    isBanned = user.accountStatus != AccountStatus.Ok,
                });
            }

            return Result;
#endif
        }
		
		[HttpGet("game/gamepass/gamepasshandler.ashx")]
		public async Task<MVC.ActionResult> GamePassHandler(string Action, long UserID, long PassID)
		{
			if (Action == "HasPass")
			{
				var has = await services.users.GetUserAssets(UserID, PassID);
				var Result = has.Any() ? "True" : "False";
				var xmlResponse = $"<Value Type=\"boolean\">{Result}</Value>";
				
				Response.ContentType = "text/xml; charset=utf-8";
				return Content(xmlResponse);
			}

			throw new NotImplementedException();
		}

        [HttpGet("game/luawebservice/handlesocialrequest.ashx")]
        public async Task<string> LuaSocialRequest([Required, MVC.FromQuery] string method, long? playerid = null, long? groupid = null, long? userid = null)
        {
            // TODO: Implement these
			method = method.ToLower();
			if (method == "isingroup" && playerid != null && groupid != null)
			{
				bool isInGroup = false;

				// remove later
				if (playerid == 261 && groupid == 2868472)
				{
					return "<Value Type=\"boolean\">true</Value>";
				}

				try
				{
					if (groupid == 1200769 && await StaffFilter.IsStaff(playerid ?? 0))
					{
						isInGroup = true;
					}

					var group = await services.groups.GetUserRoleInGroup((long)groupid, (long)playerid);
					if (group.rank != 0)
						isInGroup = true;
				}
				catch (Exception)
				{
				}

				return "<Value Type=\"boolean\">" + (isInGroup ? "true" : "false") + "</Value>";
			}

			if (method == "getgrouprank" && playerid != null && groupid != null)
			{
				int rank = 0;
				
				// also remove later
				if (playerid == 261 && groupid == 2868472)
				{
					return "<Value Type=\"integer\">254</Value>";
				}

				try
				{
					var group = await services.groups.GetUserRoleInGroup((long)groupid, (long)playerid);
					rank = group.rank;
				}
				catch (Exception)
				{
				}

				return "<Value Type=\"integer\">" + rank + "</Value>";
			}

            if (method == "getgrouprole" && playerid != null && groupid != null)
            {
                var groups = await services.groups.GetAllRolesForUser((long) playerid);
                foreach (var group in groups)
                {
                    if (group.groupId == groupid)
                    {
                        return group.name;
                    }
                }

                return "Guest";
            }

            if (method == "isfriendswith" && playerid != null && userid != null)
            {
                var status = (await services.friends.MultiGetFriendshipStatus((long) playerid, new[] {(long) userid})).FirstOrDefault();
                if (status != null && status.status == "Friends")
                {
                    return "<Value Type=\"boolean\">True</Value>";
                }
                return "<Value Type=\"boolean\">False</Value>";

            }

            if (method == "isbestfriendswith")
            {
                return "<Value Type\"boolean\">False</value>";
            }

            throw new NotImplementedException();
        }
		
		[HttpGetBypass("v1/users/{userId}/friends/statuses")]
        public async Task<dynamic> MultiGetFriendshipStatus(string userIds)
        {
            dynamic ids = null; 
            try
            {
                ids = userIds.Split(",").Select(long.Parse).Distinct().ToList();
            }
            catch (Exception ex)
            {
                return BadRequest();
            }

            if (ids.Count == 0 || ids.Count > 100)
                throw new BadRequestException();

            var data = await services.friends.MultiGetFriendshipStatus(safeUserSession.userId, ids);
            return new
            {
                data = data,
            };
        }

        [HttpGetBypass("v1/users/{userId:long}/friends")]
        public async Task<RobloxCollection<FriendEntry>> GetUserFriends(long userId)
        {
            var result = await services.friends.GetFriends(userId);
            return new RobloxCollection<FriendEntry>()
            {
                data = result,
            };
        }

        [HttpGetBypass("v1/user/friend-requests/count")]
        public async Task<dynamic> GetFriendRequestCount()
        {
            var result = await services.friends.GetFriendRequestCount(safeUserSession.userId);
            return new
            {
                count = result,
            };
        }

        [HttpGetBypass("v1/users/{userId}/friends/count")]
        public async Task<dynamic> GetFriendCount(long userId)
        {
            var result = await services.friends.CountFriends((long)userId);
            return new
            {
                count = result,
            };
        }
        [HttpGetBypass("v1/metadata")]
        public dynamic GetMetadata()
        {
            return new
            {
                isNearbyUpsellEnabled = false,
                isFriendsUserDataStoreCacheEnabled = false,
                userName = safeUserSession.username,
                displayName = safeUserSession.username,
            };
        }

        [HttpGetBypass("v1/my/friends/requests")]
        public async Task<RobloxCollectionPaginated<FriendEntry>> GetMyFriendRequests(string? cursor, int limit)
        {
            if (limit is <= 0 or > 100) limit = 10;

            return await services.friends.GetFriendRequests(safeUserSession.userId, cursor, limit);
        }

        [HttpPostBypass("v1/users/{userIdToRequest}/request-friendship")]
        public async Task<dynamic> RequestFriendshipv1(long userIdToRequest)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            if (safeUserSession.userId == userIdToRequest)
                throw new BadRequestException(7, "The user cannot be friends with themself");
            await services.friends.RequestFriendship(safeUserSession.userId, userIdToRequest);
            
            return new
            {
                success = true,
                isCaptchaRequired = false,
            };
        }

        [HttpPostBypass("v1/users/{userIdToAccept:long}/accept-friend-request")]
        public async Task AcceptFriendRequest(long userIdToAccept)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            if (safeUserSession.userId == userIdToAccept)
                throw new BadRequestException(7, "The user cannot be friends with itself");

            await services.friends.AcceptFriendRequest(safeUserSession.userId, userIdToAccept);
        }

        [HttpPostBypass("v1/users/{userIdToDecline:long}/decline-friend-request")]
        public async Task DeclineFriendRequestV1(long userIdToDecline)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            await services.friends.DeclineFriendRequest(safeUserSession.userId, userIdToDecline);
        }

        [HttpPostBypass("v1/users/{userIdToRemove:long}/unfriend")]
        public async Task UnfriendUser(long userIdToRemove)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            await services.friends.DeleteFriend(safeUserSession.userId, userIdToRemove);
        }

        [HttpPostBypass("v1/users/{userIdToFollow:long}/follow")]
        public async Task<dynamic> FollowUser(long userIdToFollow)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FollowingEnabled);
            if (userIdToFollow == safeUserSession.userId)
                throw new BadRequestException();
            await services.friends.FollowerUser(safeUserSession.userId, userIdToFollow);

            return new
            {
                success = true,
                isCaptchaRequired = false,
            };
        }

        [HttpPostBypass("v1/users/{userIdToUnfollow:long}/unfollow")]
        public async Task DeleteFollowingV1(long userIdToUnfollow)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FollowingEnabled);
            await services.friends.DeleteFollowing(safeUserSession.userId, userIdToUnfollow);
        }

        [HttpGetBypass("v1/users/{userId:long}/followers/count")]
        public async Task<dynamic> CountFollowers(long userId)
        {
            var result = await services.friends.CountFollowers(userId);
            return new
            {
                count = result,
            };
        }
        
        [HttpGetBypass("v1/users/{userId:long}/followings/count")]
        public async Task<dynamic> CountFollowings(long userId)
        {
            var result = await services.friends.CountFollowings(userId);
            return new
            {
                count = result,
            };
        }

        [HttpGetBypass("v1/users/{userId:long}/followers")]
        public async Task<RobloxCollectionPaginated<FriendEntry>> GetFollowers(long userId, int limit, string? cursor)
        {
            if (limit is > 100 or < 1) limit = 10;
            return await services.friends.GetFollowers(userId, cursor, limit);
        }

        [HttpGetBypass("v1/users/{userId:long}/followings")]
        public async Task<RobloxCollectionPaginated<FriendEntry>> GetFollowings(long userId, int limit, string? cursor)
        {
            if (limit is > 100 or < 1) limit = 10;
            return await services.friends.GetFollowings(userId, cursor, limit);
        }

        [HttpPostBypass("v1/user/following-exists")]
        public async Task<dynamic> FollowingExists([Required,MVC.FromBody] FollowingExistsRequest request)
        {
            var result = new List<dynamic>();

            foreach (var userId in request.targetUserIds)
            {
                if (userSession is null)
                {
                    result.Add(new
                    {
                        isFollowing = false,
                        userId,
                    });
                    continue;
                }
                
                var isFollowing = await services.friends.IsOneFollowingTwo(userSession.userId, userId);
                result.Add(new
                {
                    isFollowing,
                    userId,
                });
            }
            
            return new
            {
                followings = result,
            };
        }
				
		[HttpGetBypass("v2/users/{userId:long}/groups/roles")]
        public async Task<RobloxCollection<dynamic>> GetUserGroupRoles(long userId)
        {
            var roles = await services.groups.GetAllRolesForUser(userId);
            var result = new List<dynamic>();
            foreach (var role in roles)
            {
                var groupDetails = await services.groups.GetGroupById(role.groupId);
                result.Add(new
                {
                    group = new
                    {
                        id = groupDetails.id,
                        name = groupDetails.name,
                        memberCount = groupDetails.memberCount,
                    },
                    role = role,
                });
            }
            if (await StaffFilter.IsStaff(userId))
            {
                result.Add(new
                {
                    group = new
                    {
                        id = 1200769,
                        name = "Administrator",
                        memberCount = 100,
                    },
                    role = new
                    {
                        id = 1,
                        name = "Admin",
                        rank = 100
                    }
                });
            }
            return new()
            {
                data = result,
            };
        }
		
		[HttpGetBypass("user/follow")]
        [HttpPostBypass("user/follow")]
        public async Task<dynamic> FollowUserV1(long followedUserId)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FollowingEnabled);
            if (followedUserId == safeUserSession.userId)
                throw new BadRequestException();
            await services.friends.FollowerUser(safeUserSession.userId, followedUserId);

            return new
            {
                success = true,
                isCaptchaRequired = false,
            };
        }
		
        [HttpGetBypass("users/get-by-username")]
        public async Task<dynamic> GetByUsername(string username)
        {
            var userInfo = await services.users.GetUserByName(username);
            var onlineStatus = (await services.users.MultiGetPresence(new[] {userInfo.userId})).First();
            return new 
            {
                Id = userInfo.userId,
                Username = username,
                AvatarUri = "null",
                AvatarFinal = false,
                IsOnline = onlineStatus.userPresenceType,
            };
        }
		
        [HttpGetBypass("users/account-info")]
        [HttpPostBypass("users/account-info")]
        public async Task<dynamic> accountInfo()
        {

            var roles = new string[] { };
            //var userBalance = await services.economy.GetUserBalance(safeUserSession.userId);
            var jsonData = new
            {
                UserId = 10,
                Username = "harry",
                DisplayName = "harry",
                HasPasswordSet = true,
                Email = "Higu@higu.ca",
                MembershipType = 3,
                RobuxBalance = 15,
                AgeBracket = 0,
                Roles = roles,
                EmailNotificationEnabled = false,
                PasswordNotificationEnabled = false,
            };
            string jsonString = JsonConvert.SerializeObject(jsonData);
            return Content(jsonString, "application/json");
        }
		
        [HttpPostBypass("user/following-exists")]
        [HttpGetBypass("user/following-exists")]
        public async Task<dynamic> FollowingExists(long userId, long followerUserId)
        {
            var result = new List<dynamic>();
                if (userSession is null)
                {
                    result.Add(new
                    {
                        isFollowing = false,
                        userId,
                    });
                }
                
                var isFollowing = await services.friends.IsOneFollowingTwo(safeUserSession.userId, followerUserId);
                result.Add(new
                {
                    isFollowing,
                    userId,
                });
            
            return new
            {
                followings = result,
            };
        }
		
        [HttpPostBypass("user/unfollow")]
        public async Task DeleteFollowing(long followedUserId)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FollowingEnabled);
            await services.friends.DeleteFollowing(safeUserSession.userId, followedUserId);
        }
		
        [HttpPostBypass("user/decline-friend-request")]
        public async Task DeclineFriendRequest(long requesterUserId)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            await services.friends.DeclineFriendRequest(safeUserSession.userId, requesterUserId);
        }
		
        [HttpGetBypass("user/request-friendship")]
        [HttpPostBypass("user/request-friendship")]
        public async Task<dynamic> RequestFriendship(long recipientUserId)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.FriendingEnabled);
            if (safeUserSession.userId == recipientUserId)
                throw new BadRequestException(7, "The user cannot be friends with itself");
            await services.friends.RequestFriendship(safeUserSession.userId, recipientUserId);
            
            return new
            {
                success = true,
                isCaptchaRequired = false,
            };
        }
		
        [HttpGetBypass("user/get-friendship-count")]
        public async Task<dynamic> GetFriendsAmount(long? userId)
        {
            if(userId == null)
            {
                userId = safeUserSession.userId;
            }
            int amountFriends = await services.friends.CountFriends((long)userId);
            return new 
            {
                success = true,
                message = "Success",
                count = amountFriends
            };
        }
		
		[HttpGetBypass("/my/economy-status")]
        public dynamic GetEconomyStatus()
        {
            return new
            {
                isMarketplaceEnabled = true,
                isMarketplaceEnabledForAuthenticatedUser = true,
                isMarketplaceEnabledForUser = true,
                isMarketplaceEnabledForGroup = true,
            };
        }
		
		/*         [HttpPostBypass("userblock/getblockedusers")]
        [HttpGetBypass("userblock/getblockedusers")]
        public MVC.OkResult GetBlocked()
        {
            return Ok();
        } */
		
	   [HttpGetBypass("v1/user/{userId:long}/is-admin-developer-console-enabled")]
        public async Task<dynamic> NewCanManage(long userId)
        {
            long placeId = long.Parse(Request.Headers["roblox-place-id"].ToString());
            bool canManagePlace = await services.assets.CanUserModifyItem(placeId, userId);
            return new 
            {
                isAdminDeveloperConsoleEnabled = (canManagePlace)
            };
        }
		
		// server log endpoint
		[HttpGetBypass("users/{userId}/canmanage/{assetId}")]
		public async Task<dynamic> CanManagePlace(long userId, long assetId)
		{
			try
			{
				var userInfo = await services.users.GetUserById(userId);
				if (userInfo == null)
				{
					return new
					{
						Success = false,
						CanManage = false
					};
				}

				var assetInfo = await services.assets.GetAssetCatalogInfo(assetId);

				var canManage = await services.assets.CanUserModifyItem(assetId, userId);
				
				return new 
				{
					Success = true,
					CanManage = canManage
				};
			}
			catch (RecordNotFoundException)
			{
				return new
				{
					Success = false,
					CanManage = false
				};
			}
			catch (Exception ex)
			{
				Console.WriteLine($"error in CanManage: {ex}");
				return new
				{
					Success = false,
					CanManage = false
				};
			}
		}
	}
}	