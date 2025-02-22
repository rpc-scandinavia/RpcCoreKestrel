using System;
using System.Security.Claims;
using System.Threading.Tasks;
namespace RpcScandinavia.Core.Kestrel;

/// <summary>
/// Kestrel authentication service, that performs the authentication.
/// The <see cref="KestrelAuthMiddleware"/> middleware uses a service implementing this interface to authenticate users.
/// </summary>
public interface IKestrelAuthService {

	/// <summary>
	/// Called by the Kestrel authentication middleware to perform a basic user authentication.
	/// </summary>
	/// <param name="userIdentification">The received user identifier.</param>
	/// <param name="userPassword">The received user password.</param>
	/// <returns>The authenticated user claims principal, or throws an exception when the authentication fails.</returns>
	/// <exception cref="Exception">Exception containing the reason the authentication failed.</exception>
	public Task<ClaimsPrincipal> BasicAuthenticateAsync(String userIdentification, String userPassword);

	/// <summary>
	/// Called by the Kestrel authentication middleware to perform a bearer token user authentication.
	/// The Kestrel authentication service must validate the bearer token, and return the user claims principal that
	/// is associated with the token, and returned by <see cref="GetBearerTokenAsync"/>.
	/// </summary>
	/// <param name="token">The received bearer token.</param>
	/// <returns>The authenticated user claims principal, or throws an exception when the authentication fails.</returns>
	/// <exception cref="Exception">Exception containing the reason the authentication failed.</exception>
	public Task<ClaimsPrincipal> BearerAuthenticateAsync(String token);

	/// <summary>
	/// Called by the Kestrel authentication middleware to perform a API-key authentication.
	/// The Kestrel authentication service must validate the API-key, and return the user claims principal that is
	/// associated with the API-key. The user claims principal can for instance be a system API-user.
	/// </summary>
	public Task<ClaimsPrincipal> ApiKeyAuthenticateAsync(String authKey);

	public Task SetActiveUserAsync(ClaimsPrincipal principal);

	/// <summary>
	/// Called by the Kestrel authentication middleware when a user is authenticated, and a bearer token is sent to the client.
	/// The bearer token must be able to authenticate the user claims principal by calling <see cref="BearerAuthenticateAsync"/>.
	/// </summary>
	/// <param name="principal">The user claims principal.</param>
	/// <returns>The bearer token.</returns>
	public Task<String> GetBearerTokenAsync(ClaimsPrincipal principal);

} // IKestrelAuthService
