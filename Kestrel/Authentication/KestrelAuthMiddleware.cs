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
/// * gRPC-key authentication: <value>apikey data</value>
///
/// The <value>X-2FA-Code</value> host header is recognized in this form:
/// * Two-factor authenticatin: <value>two-factor code</value>
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
				// Get, decode and split the authorization header value.
				String authScheme = context.Request.Headers.Authorization.ToString().Split(" ").FirstOrDefault(String.Empty);
				String authCredentials = String.Empty;
				try {
					authCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(context.Request.Headers.Authorization.ToString().Split(" ").Skip(1).FirstOrDefault(String.Empty)));
				} catch {}

				// Authenticate.
				ClaimsPrincipal principal = null;
				String authTwoFactorCode = null;
				String token = null;
				switch (authScheme.ToLower()) {
					case "basic":
						// Get the "X-2FA-Code" header value.
						if (context.Request.Headers.ContainsKey("X-2FA-Code") == true) {
							authTwoFactorCode = context.Request.Headers["X-2FA-Code"].ToString();
						}

						String authUserIdentification = authCredentials.Split(':').FirstOrDefault(String.Empty);
						String authUserPassword = authCredentials.Split(':').Skip(1).FirstOrDefault(String.Empty);
						principal = await kestrelAuthService.BasicAuthenticateAsync(authUserIdentification, authUserPassword, authTwoFactorCode);
						token = await kestrelAuthService.GetBearerTokenAsync(principal);
						break;
					case "bearer":
						String authToken = authCredentials;
						principal = await kestrelAuthService.BearerAuthenticateAsync(authToken);
						token = await kestrelAuthService.GetBearerTokenAsync(principal);
						break;
					case "apikey":
						String authKey = authCredentials;
						principal = await kestrelAuthService.ApiKeyAuthenticateAsync(authKey);
						token = await kestrelAuthService.GetBearerTokenAsync(principal);
						break;
					default:
						throw new UnauthorizedAccessException($"Unknown authorization scheme '{authScheme}'.") { HResult = StatusCodes.Status400BadRequest };
				}

				// Validate that the IKestrelAuthService actually returned a user claims principal.
				if (principal == null) {
					throw new Exception($"Internal server error. The Kestrel authentication ervice did not return a claims principal!");
				}

				// Set the user.
				context.User = principal;
				await kestrelAuthService.SetActiveUserAsync(principal);

				// Set the bearer token in the response headers.
				// The bearer token should be Base 64 encoded.
				context.Response.Headers.Authorization = $"Bearer {Convert.ToBase64String(Encoding.Default.GetBytes(token))}";

				// Log.
				if (logger.IsEnabled(LogLevel.Information) == true) {
					if (authTwoFactorCode == null) {
						logger.LogInformation($"User '{principal.Identity?.Name}' was authenticated using '{authScheme}' scheme.");
					} else {
						logger.LogInformation($"User '{principal.Identity?.Name}' was authenticated using two-factor code '{authTwoFactorCode}' and '{authScheme}' scheme.");
					}
				}
				if (logger.IsEnabled(LogLevel.Debug) == true) {
					logger.LogDebug($"Bearer token: '{token}'.");
				}
			}

			// Iterate to the next middleware in the pipe.
			await this.next(context);
		} catch (KestrelAuthInvalidUserIdentificationException exception) {
			// Log.
			if (logger.IsEnabled(LogLevel.Error) == true) {
				logger.LogError(exception, exception.Message);
			}

			// Return the error.
			context.Response.StatusCode = exception.HResult;
		} catch (KestrelAuthInvalidUserPasswordException exception) {
			// Log.
			if (logger.IsEnabled(LogLevel.Error) == true) {
				logger.LogError(exception, exception.Message);
			}

			// Return the error.
			context.Response.StatusCode = exception.HResult;
		} catch (KestrelAuthInvalidBearerTokenException exception) {
			// Log.
			if (logger.IsEnabled(LogLevel.Error) == true) {
				logger.LogError(exception, exception.Message);
			}

			// Return the error.
			context.Response.StatusCode = exception.HResult;
		} catch (KestrelAuthExpiredBearerTokenException exception) {
			// Log.
			if (logger.IsEnabled(LogLevel.Error) == true) {
				logger.LogError(exception, exception.Message);
			}

			// Return the error.
			context.Response.StatusCode = exception.HResult;
		} catch (KestrelAuthInvalidTwoFactorException exception) {
			// Log.
			if (logger.IsEnabled(LogLevel.Error) == true) {
				logger.LogError(exception, exception.Message);
			}

			// Return the error.
			context.Response.StatusCode = exception.HResult;
		} catch (KestrelAuthExpiredTwoFactorException exception) {
			// Log.
			if (logger.IsEnabled(LogLevel.Error) == true) {
				logger.LogError(exception, exception.Message);
			}

			// Return the error.
			context.Response.Headers.Append("X-2FA-Required", true.ToString());
			context.Response.StatusCode = exception.HResult;
		} catch (KestrelAuthTwoFactorRequiredException exception) {
			// Log.
			if (logger.IsEnabled(LogLevel.Warning) == true) {
				logger.LogWarning(exception, exception.Message);
			}

			// Return the error.
			context.Response.Headers.Append("X-2FA-Required", true.ToString());
			context.Response.StatusCode = exception.HResult;
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
