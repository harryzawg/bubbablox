return function()
	local request = require(script.Parent.requestInternalWrapper)

	local Modules = game:GetService("CoreGui").RobloxGui.Modules
	local HttpError = require(Modules.LuaApp.Http.HttpError)
	local StatusCodes = require(Modules.LuaApp.Http.StatusCodes)

	HACK_NO_XPCALL()

	local function createTestRequestFunc(testResponse)
		local requestService = {}
		function requestService:RequestInternal()
			local httpRequest = {}
			function httpRequest:Start(callback)
				callback(true, testResponse)
			end
			return httpRequest
		end

		return request(requestService)
	end

	it("should return a function", function()
		expect(request()).to.be.ok()
		expect(type(request())).to.equal("function")
	end)

	it("which returns a promise that resolves to an HttpResponse", function()
		local responseUpval

		local testRequest = createTestRequestFunc({
            StatusCode = 200,
            RoundTripTime = 0,
            Body = '{"data" : "foo"}',
		})
		local httpPromise = testRequest("testUrl", "GET")
		httpPromise:andThen(function(response)
			responseUpval = response
		end)

		wait()

		expect(responseUpval.requestUrl).to.equal("testUrl")
		expect(responseUpval.responseBody.data).to.equal("foo")
		expect(responseUpval.responseCode).to.equal(StatusCodes.OK)
	end)
end