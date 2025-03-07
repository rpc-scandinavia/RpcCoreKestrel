using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
	public async Task<ClaimsPrincipal> BasicAuthenticateAsync(String userIdentification, String userPassword, String twoFactorCode = null) {
		// Authenticate the user and create the claims principal.
		KestrelMemoryAuthServiceUser user = this.options.Value.GetUser(userIdentification);
		if (user == null) {
			throw new KestrelAuthInvalidUserIdentificationException(userIdentification);
		}
		if (user.Password != userPassword) {
			throw new KestrelAuthInvalidUserPasswordException(userIdentification, userPassword);
		}

		// Two-factor authentication.
		if (user.TwoFactorCode > 0) {
			if (String.IsNullOrWhiteSpace(twoFactorCode) == true) {
				throw new KestrelAuthTwoFactorRequiredException(KestrelAuthTwoFactorType.UnknownNumeric, userIdentification, userPassword);
			} else if (user.TwoFactorCode.ToString() != twoFactorCode) {
				throw new KestrelAuthInvalidTwoFactorException(KestrelAuthTwoFactorType.UnknownNumeric, userIdentification, userPassword, twoFactorCode);
			}
		}

		// Generate bearer token.
		ClaimsPrincipal principal = user.ToClaimsPrincipal("Basic");
		String token = await this.GenerateBearerTokenAsync(principal);

		// Save the bearer token/claims principal.
		this.tokensSemaphore.Wait();
		try {
			this.tokens[token] = principal;
		} finally {
			this.tokensSemaphore.Release();
		}

		// Return the principal.
		return principal;
	} // BasicAuthenticateAsync

	public async Task<ClaimsPrincipal> BearerAuthenticateAsync(String token) {
		// Get the user associated with the bearer token.
		ClaimsPrincipal principal = null;
		this.tokensSemaphore.Wait();
		try {
			if (this.tokens.TryGetValue(token, out principal) == false) {
				throw new KestrelAuthInvalidBearerTokenException(token);
			}
		} finally {
			this.tokensSemaphore.Release();
		}

		// Validate the bearer token.
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

			// Validate the bearer token.
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
				try {
					this.tokens[token] = principal;
				} finally {
					this.tokensSemaphore.Release();
				}
			}

			// Return the user principal.
			return principal;
		} catch (SecurityTokenExpiredException exception) {
			throw new KestrelAuthExpiredBearerTokenException(token, exception.Expires);
		} catch {
			throw new KestrelAuthInvalidBearerTokenException(token);
		}
	} // BearerAuthenticateAsync

	public async Task<ClaimsPrincipal> ApiKeyAuthenticateAsync(String token) {
		throw new NotImplementedException($"gRPC Key Authentication is not supported.");
	} // ApiKeyAuthenticateAsync

	public async Task SetActiveUserAsync(ClaimsPrincipal principal) {
	} // SetActiveUserAsync

	public async Task<String> GetBearerTokenAsync(ClaimsPrincipal principal) {
		this.tokensSemaphore.Wait();
		try {
			return this.tokens
				.Where((keyValuePair) => keyValuePair.Value == principal)
				.Select((keyValuePair) => keyValuePair.Key)
				.SingleOrDefault(String.Empty);
		} finally {
			this.tokensSemaphore.Release();
		}
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
	private Int32 userTwoFactorCode;
	private String userName;
	private List<Claim>	userClaims;

	public KestrelMemoryAuthServiceUser() {
		this.userIdentification = Guid.NewGuid().ToString();
		this.userPassword = String.Empty;
		this.userTwoFactorCode = 0;
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

	public Int32 TwoFactorCode {
		get {
			return this.userTwoFactorCode;
		}
		set {
			this.userTwoFactorCode = value;
		}
	} // TwoFactorCode

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
		List<Claim> claims = new List<Claim>(this.userClaims);

		if (claims.FirstOrDefault((claim) => claim.Type.Equals(ClaimTypes.Authentication)) == null) {
			claims.Add(new Claim(ClaimTypes.Authentication, true.ToString()));
		}
		if (claims.FirstOrDefault((claim) => claim.Type.Equals(ClaimTypes.Name)) == null) {
			claims.Add(new Claim(ClaimTypes.Name, this.userName));
		}

		return new ClaimsPrincipal(
			new ClaimsIdentity(
				claims,
				authentication ?? "Basic",
				ClaimTypes.Name,
				ClaimTypes.Role
			)
		);
	} // ToClaimsPrincipal

} // KestrelMemoryAuthServiceUser
#endregion
