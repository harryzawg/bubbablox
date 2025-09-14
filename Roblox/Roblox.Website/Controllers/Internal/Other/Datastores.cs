using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Roblox.Exceptions;
using Roblox.Services;
using Roblox.Website.Middleware;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using MVC = Microsoft.AspNetCore.Mvc;
using Roblox.Services.Exceptions;
using Roblox.Dto.Persistence;

namespace Roblox.Website.Controllers 
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class Datastores : ControllerBase 
    {		
		private bool IsRcc()
        {
            var rccAccessKey = Request.Headers.ContainsKey("accesskey") ? Request.Headers["accesskey"].ToString() : null;
            var isRcc = rccAccessKey == Configuration.RccAuthorization;
            return isRcc;
        }
		
        [HttpPostBypass("persistence/increment")]
        public async Task<dynamic> IncrementPersistence(long placeId, string key, string type, string scope, string target, int value)
        {
            // increment?placeId=%i&key=%s&type=%s&scope=%s&target=&value=%i
            
            if (!IsRcc())
                throw new RobloxException(400, 0, "BadRequest");
            
            return new
            {
                data = (object?) null,
            };
        }

        [HttpPostBypass("persistence/getSortedValues")]
        public async Task<dynamic> GetSortedPersistenceValues(long placeId, string type, string scope, string key, int pageSize, bool ascending, int inclusiveMinValue = 0, int inclusiveMaxValue = 0)
        {
            // persistence/getSortedValues?placeId=0&type=sorted&scope=global&key=Level%5FHighscoResponse20&pageSize=10&ascending=False"
            // persistence/set?placeId=124921244&key=BF2%5Fds%5Ftest&&type=standard&scope=global&target=BF2%5Fds%5Fkey%5Ftmp&valueLength=31
            
            if (!IsRcc())
                throw new RobloxException(400, 0, "BadRequest");
            
            return new
            {
                data = new
                {
                    Entries = ArraySegment<int>.Empty,
                    ExclusiveStartKey = (string?)null,
                },
            };
        }

		[HttpPostBypass("persistence/getv2")]
		public async Task<dynamic> GetPersistenceV2(long placeId, string type, string scope)
		{
			// getV2?placeId=%i&type=%s&scope=%s
            // Expected format is:
            //	{ "data" : 
            //		[
            //			{	"Value" : value,
            //				"Scope" : scope,							
            //				"Key" : key,
            //				"Target" : target
            //			}
            //		]
            //	}
            // or for non-existing key:
            // { "data": [] }
            
            // for no sub key:
            // Expected format is:
            //	{ "data" : value }
			using var ds = Roblox.Services.ServiceProvider.GetOrCreate<DataStoreService>();

			if (!Request.HasFormContentType)
				throw new RobloxException(400, 0, "Expected form data");

			var form = await Request.ReadFormAsync();

			Console.WriteLine("Persistence getv2 form data:");
			foreach (var kvp in form)
			{
				Console.WriteLine($"{kvp.Key} = {kvp.Value}");
			}

			var Result = new List<GetKeyEntry>();

			var qkeys = form.Keys.Where(k => k.StartsWith("qkeys[")).Select(k => k.Split(']')[0] + "]").Distinct();

			foreach (var qkey in qkeys)
			{
				var key = form[$"{qkey}.key"];
				var qscope = form[$"{qkey}.scope"];
				var target = form[$"{qkey}.target"];

				if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(qscope) || string.IsNullOrWhiteSpace(target))
					continue;

				var Response = await ds.Get(placeId, type, qscope, key, target);
				if (!string.IsNullOrWhiteSpace(Response))
				{
					Result.Add(new GetKeyEntry()
					{
						Key = key,
						Scope = qscope,
						Target = target,
						Value = Response,
					});
				}
			}

			if (!IsRcc())
				throw new RobloxException(400, 0, "BadRequest");
			
			Console.WriteLine("Persistence getv2 result:");
			foreach (var entry in Result)
			{
				Console.WriteLine($"Key: {entry.Key}, Scope: {entry.Scope}, Target: {entry.Target}, Value: {entry.Value}");
			}

			return new
			{
				data = Result,
			};
		}

		[HttpPostBypass("persistence/set")]
		public async Task<dynamic> Set(long placeId, string key, string type, string scope, string target, int valueLength)
		{
			// { "data" : value }
			if (!IsRcc())
				throw new RobloxException(400, 0, "BadRequest");
			
			string RequestData = null;
			if (Request.HasFormContentType)
			{
				var form = await Request.ReadFormAsync();
				RequestData = form.TryGetValue("data", out var val) ? val.ToString() : form.TryGetValue("value", out var val2) ? val2.ToString() : null;
			}

			Console.WriteLine($"Persistence set data: {RequestData}");

			await Roblox.Services.ServiceProvider.GetOrCreate<DataStoreService>()
				.Set(placeId, key, type, scope, target, valueLength, RequestData);

			return new { data = RequestData };
		}
	}
}	