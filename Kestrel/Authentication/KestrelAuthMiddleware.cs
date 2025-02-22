using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace RpcScandinavia.Core.Kestrel;

/// <summary>
/// Kestrel authentication middleware that handles authentication.
/// This requires that you write and add a service implementing <see cref="IKestrelAuthService"/>.
///
/// The <value>Authorization</value> host header is recognized in these forms:
/// * Basic authentication: <value>basic user:password</value>
/// * Bearer authentication: <value>bearer token</value>
/// * API-key authentication: <value>apikey data</value>
/// </summary>
public class KestrelAuthMiddleware {
	private readonly RequestDelegate next;

	public KestrelAuthMiddleware(RequestDelegate next) {
		this.next = next;
	} // KestrelAuthMiddleware

	public async Task Invoke(HttpContext context, IKestrelAuthService kestrelAuthService, ILogger<KestrelAuthMiddleware> logger) {
		try {
			// Authorize a user.
			if (context.Request.Headers.Authorization.Count > 0) {
				// Decode and split the authorization header value.
				String authScheme = context.Request.Headers.Authorization.ToString().Split(" ").FirstOrDefault(String.Empty);
				String authCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(context.Request.Headers.Authorization.ToString().Split(" ").Skip(1).FirstOrDefault(String.Empty)));

				// Authenticate.
				ClaimsPrincipal authPrincipal = null;
				switch (authScheme.ToLower()) {
					case "basic":
						String authUserIdentification = authCredentials.Split(':').FirstOrDefault();
						String authUserPassword = authCredentials.Split(':').Skip(1).FirstOrDefault();
						authPrincipal = await kestrelAuthService.BasicAuthenticateAsync(authUserIdentification, authUserPassword);
						break;
					case "bearer":
						String authToken = authCredentials;
						authPrincipal = await kestrelAuthService.BearerAuthenticateAsync(authToken);
						break;
					case "apikey":
						String authKey = authCredentials;
						authPrincipal = await kestrelAuthService.ApiKeyAuthenticateAsync(authKey);
						break;
					default:
						throw new UnauthorizedAccessException($"Unknown authorization scheme '{authScheme}'.") { HResult = StatusCodes.Status400BadRequest };
				}

				// Set the user.
				context.User = authPrincipal;
				await kestrelAuthService.SetActiveUserAsync(authPrincipal);

				// Set the Bearer token in the response headers.
				String bearerToken = await kestrelAuthService.GetBearerTokenAsync(authPrincipal);
				context.Response.Headers.Authorization = $"Bearer {bearerToken}";

				// Log.
				if (logger.IsEnabled(LogLevel.Information) == true) {
					logger.LogInformation($"User '{authPrincipal.Identity.Name}' was authenticated using '{authScheme}' scheme.");
				}
			}

			// Iterate to the next middleware in the pipe.
			await this.next(context);
		} catch (Exception exception) {
			// Log.
			if (logger.IsEnabled(LogLevel.Error) == true) {
				logger.LogError(exception, $"Error {exception.HResult}: {exception.Message}");
			}

			// Return the error.
			context.Response.StatusCode = (exception.HResult == 0) ? StatusCodes.Status401Unauthorized : exception.HResult;
		}
	} // Invoke

} // KestrelAuthMiddleware
