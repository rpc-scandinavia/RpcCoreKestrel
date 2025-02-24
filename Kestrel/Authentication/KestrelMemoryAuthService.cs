using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RpcScandinavia.Core.Kestrel;
namespace RpcScandinavia.Repository;

#region KestrelMemoryAuthService class
//----------------------------------------------------------------------------------------------------------------------
// KestrelMemoryAuthService class.
//----------------------------------------------------------------------------------------------------------------------
/// <summary>
/// Kestrel authentication service, that performs the authentication from data stored in-memory.
/// The <see cref="KestrelAuthMiddleware"/> middleware uses a service implementing this interface to authenticate users.
/// </summary>
public class KestrelMemoryAuthService : IKestrelAuthService {
	private readonly IOptions<KestrelMemoryAuthServiceOptions> options;
	private readonly Dictionary<String, ClaimsPrincipal> tokens;
	private readonly SemaphoreSlim tokensSemaphore;

	public KestrelMemoryAuthService(IOptions<KestrelMemoryAuthServiceOptions> options) {
		this.options = options;
		this.tokens = new Dictionary<String, ClaimsPrincipal>();
		this.tokensSemaphore = new SemaphoreSlim(1, 1);
	} // KestrelMemoryAuthService

	//------------------------------------------------------------------------------------------------------------------
	// KestrelMemoryAuthService methods.
	//------------------------------------------------------------------------------------------------------------------
	public async Task<ClaimsPrincipal> BasicAuthenticateAsync(String userIdentification, String userPassword) {
		// Authenticate the user and create the claims principal.
		KestrelMemoryAuthServiceUser user = this.options.Value.GetUser(userIdentification);
		if (user == null) {
			throw new AuthenticationException($"Unknown user identification '{userIdentification}' or password.") { HResult = StatusCodes.Status404NotFound };
		}
		if (user.Password != userPassword) {
			throw new AuthenticationException($"Unknown user identification '{userIdentification}' or password.") { HResult = StatusCodes.Status401Unauthorized };
		}

		// Generate Bearer token.
		ClaimsPrincipal principal = user.ToClaimsPrincipal("Basic");
		String token = await this.GenerateBearerTokenAsync(principal);

		// Save the bearer token/principal.
		this.tokensSemaphore.Wait();
		this.tokens[token] = principal;
		this.tokensSemaphore.Release();

		// Return the principal.
		return principal;
	} // BasicAuthenticateAsync

	public async Task<ClaimsPrincipal> BearerAuthenticateAsync(String token) {
		// Get the user associated with the token.
		this.tokensSemaphore.Wait();
		if (this.tokens.TryGetValue(token, out ClaimsPrincipal principal) == false) {
			this.tokensSemaphore.Release();
			throw new UnauthorizedAccessException($"Unknown authorization token.") { HResult = StatusCodes.Status404NotFound };
		}
		this.tokensSemaphore.Release();

		// Validate the token.
		JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
		Byte[] tokenKey = Encoding.UTF8.GetBytes(this.options.Value.BearerTokenKey);
		try {
			TokenValidationParameters validationParams = new TokenValidationParameters {
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = new SymmetricSecurityKey(tokenKey),
				ValidateIssuer = false,
				ValidateAudience = false,
				ValidateLifetime = true,
				ClockSkew = TimeSpan.Zero
			};

			// Validate the token.
			// This throws an exception on failure.
			tokenHandler.ValidateToken(token, validationParams, out SecurityToken securityToken);

			// Renew the token, if more then half the time has passed.
			JwtSecurityToken jwtToken = (JwtSecurityToken)securityToken;
			DateTime issuedAt = jwtToken.IssuedAt;
			DateTime expiresAt = jwtToken.ValidTo;
			DateTime now = DateTime.UtcNow;
			if ((now - issuedAt) > ((expiresAt - issuedAt) / 2)) {
				// Generate a new Bearer token, and save the token/principal.
				token = await this.GenerateBearerTokenAsync(principal);
				this.tokensSemaphore.Wait();
				this.tokens[token] = principal;
				this.tokensSemaphore.Release();
			}

			// Return the user principal.
			return principal;
		} catch (SecurityTokenExpiredException) {
			throw new UnauthorizedAccessException($"Authorization token expired.") { HResult = StatusCodes.Status401Unauthorized };
		} catch {
			throw new UnauthorizedAccessException($"Invalid authorization token.") { HResult = StatusCodes.Status401Unauthorized };
		}
	} // BearerAuthenticateAsync

	public async Task<ClaimsPrincipal> ApiKeyAuthenticateAsync(String token) {
		throw new NotImplementedException($"API Key Authentication is not supported.");
	} // ApiKeyAuthenticateAsync

	public async Task SetActiveUserAsync(ClaimsPrincipal principal) {
	} // SetActiveUserAsync

	public async Task<String> GetBearerTokenAsync(ClaimsPrincipal principal) {
		this.tokensSemaphore.Wait();
		String bearerToken = this.tokens
			.Where((keyValuePair) => keyValuePair.Value == principal)
			.Select((keyValuePair) => keyValuePair.Key)
			.SingleOrDefault();
		this.tokensSemaphore.Wait();
		return bearerToken;
	} // GetBearerTokenAsync

	//------------------------------------------------------------------------------------------------------------------
	// Helper methods.
	//------------------------------------------------------------------------------------------------------------------
	private async Task<String> GenerateBearerTokenAsync(ClaimsPrincipal principal) {
		// The length of the key matters.
		// System.ArgumentOutOfRangeException: IDX10720: Unable to create KeyedHashAlgorithm for algorithm 'HS256',
		// the key size must be greater than: '256' bits, key has '240' bits. (Parameter 'keyBytes')
		SymmetricSecurityKey securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.options.Value.BearerTokenKey));
		SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

		JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
		SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor {
			Subject = new ClaimsIdentity(principal.Claims),
			Expires = DateTime.UtcNow.AddMinutes(this.options.Value.BearerExpirationMinutes),
			SigningCredentials = credentials
		};

		SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
		return tokenHandler.WriteToken(token);
	} // GenerateBearerTokenAsync

} // KestrelMemoryAuthService
#endregion

#region KestrelMemoryAuthServiceOptions class
//----------------------------------------------------------------------------------------------------------------------
// KestrelMemoryAuthServiceOptions class.
//----------------------------------------------------------------------------------------------------------------------
/// <summary>
/// Kestrel authentication in-memory user data.
/// </summary>
public class KestrelMemoryAuthServiceOptions {
	private String bearerTokenKey;
	private Int32 bearerExpirationMinutes;
	private List<KestrelMemoryAuthServiceUser> users;

	public KestrelMemoryAuthServiceOptions() {
		this.bearerTokenKey = Guid.NewGuid().ToString();
		this.bearerExpirationMinutes = (60);
		this.users = new List<KestrelMemoryAuthServiceUser>();
	} // KestrelMemoryAuthServiceOptions

	public String BearerTokenKey {
		get {
			return this.bearerTokenKey;
		}
		set {
			this.bearerTokenKey = value ?? this.bearerTokenKey;
		}
	} // BearerTokenKey

	public Int32 BearerExpirationMinutes {
		get {
			return this.bearerExpirationMinutes;
		}
		set {
			bearerExpirationMinutes = value;
		}
	} // bearerExpirationMinutes

	public List<KestrelMemoryAuthServiceUser> Users {
		get {
			return this.users;
		}
	} // Users

	public KestrelMemoryAuthServiceUser GetUser(String userIdentifier) {
		return this.users.FirstOrDefault((user) => user.Identification.Equals(userIdentifier));
	} // GetUser

} // KestrelMemoryAuthServiceOptions
#endregion

#region KestrelMemoryAuthServiceUser class
//----------------------------------------------------------------------------------------------------------------------
// KestrelMemoryAuthServiceUser class.
//----------------------------------------------------------------------------------------------------------------------
/// <summary>
/// Kestrel authentication in-memory user.
/// </summary>
public class KestrelMemoryAuthServiceUser {
	private String userIdentification;
	private String userPassword;
	private String userName;
	private List<Claim>	userClaims;

	public KestrelMemoryAuthServiceUser() {
		this.userIdentification = Guid.NewGuid().ToString();
		this.userPassword = String.Empty;
		this.userName = String.Empty;
		this.userClaims = new List<Claim>();
	} // KestrelMemoryAuthServiceUser

	public String Identification {
		get {
			return this.userIdentification;
		}
		set {
			this.userIdentification = value ?? this.userIdentification;
		}
	} // Identification

	public String Password {
		get {
			return this.userPassword;
		}
		set {
			this.userPassword = value ?? String.Empty;
		}
	} // Password

	public String Name {
		get {
			return this.userName;
		}
		set {
			this.userName = value ?? String.Empty;
		}
	} // Name

	public List<Claim> Claims {
		get {
			return this.userClaims;
		}
	} // Claims

	public ClaimsPrincipal ToClaimsPrincipal(String authentication = "Basic") {
		if (this.userClaims.FirstOrDefault((claim) => claim.Type.Equals(ClaimTypes.Authentication)) == null) {
			this.userClaims.Add(new Claim(ClaimTypes.Authentication, true.ToString()));
		}
		if (this.userClaims.FirstOrDefault((claim) => claim.Type.Equals(ClaimTypes.Name)) == null) {
			this.userClaims.Add(new Claim(ClaimTypes.Name, this.userName));
		}

		return new ClaimsPrincipal(
			new ClaimsIdentity(
				this.userClaims,
				authentication ?? "Basic",
				ClaimTypes.Name,
				ClaimTypes.Role
			)
		);
	} // ToClaimsPrincipal

} // KestrelMemoryAuthServiceUser
#endregion
