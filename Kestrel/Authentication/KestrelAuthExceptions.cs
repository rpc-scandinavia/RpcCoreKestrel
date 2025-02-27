using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
namespace RpcScandinavia.Core.Kestrel;

#region KestrelAuthInvalidUserIdentificationException
//----------------------------------------------------------------------------------------------------------------------
// KestrelAuthInvalidUserIdentificationException exception class.
//----------------------------------------------------------------------------------------------------------------------
/// <summary>
/// The <see cref="IKestrelAuthService"/> should throw this exception when the specified user identifier is invalid.
/// </summary>
public class KestrelAuthInvalidUserIdentificationException : Exception {
	private readonly String userIdentification;

	public KestrelAuthInvalidUserIdentificationException(String userIdentification) {
		base.HResult = StatusCodes.Status404NotFound;
		this.userIdentification = userIdentification ?? String.Empty;
	} // KestrelAuthInvalidUserIdentificationException

	public String UserIdentification {
		get {
			return this.userIdentification;
		}
	} // UserIdentification

	public override String Message {
		get {
			return $"{base.HResult}. User identification '{this.userIdentification}' is invalid.";
		}
	} // Message

} // KestrelAuthInvalidUserIdentificationException
#endregion

#region KestrelAuthInvalidUserPasswordException
//----------------------------------------------------------------------------------------------------------------------
// KestrelAuthInvalidUserPasswordException exception class.
//----------------------------------------------------------------------------------------------------------------------
/// <summary>
/// The <see cref="IKestrelAuthService"/> should throw this exception when the specified user password is invalid.
/// </summary>
public class KestrelAuthInvalidUserPasswordException : Exception {
	private readonly String userIdentification;
	private readonly String userPassword;

	public KestrelAuthInvalidUserPasswordException(String userIdentification, String userPassword) {
		base.HResult = StatusCodes.Status401Unauthorized;
		this.userIdentification = userIdentification ?? String.Empty;
		this.userPassword = userPassword ?? String.Empty;
	} // KestrelAuthInvalidUserPasswordException

	public String UserIdentification {
		get {
			return this.userIdentification;
		}
	} // UserIdentification

	public String UserPassword {
		get {
			return this.userPassword;
		}
	} // UserPassword

	public override String Message {
		get {
			return $"{base.HResult}. User identification '{this.userIdentification}' or password is invalid.";
		}
	} // Message

} // KestrelAuthInvalidUserPasswordException
#endregion

#region KestrelAuthInvalidBearerTokenException
//----------------------------------------------------------------------------------------------------------------------
// KestrelAuthInvalidBearerTokenException exception class.
//----------------------------------------------------------------------------------------------------------------------
/// <summary>
/// The <see cref="IKestrelAuthService"/> should throw this exception when the specified bearer token is invalid.
/// </summary>
public class KestrelAuthInvalidBearerTokenException : Exception {
	private readonly String token;

	public KestrelAuthInvalidBearerTokenException(String token) {
		base.HResult = StatusCodes.Status404NotFound;
		this.token = token ?? String.Empty;
	} // KestrelAuthInvalidBearerTokenException

	public String Token {
		get {
			return this.token;
		}
	} // Token

	public override String Message {
		get {
			return $"{base.HResult}. Bearer token '{this.token}' is invalid.";
		}
	} // Message

} // KestrelAuthInvalidBearerTokenException
#endregion

#region KestrelAuthExpiredBearerTokenException
//----------------------------------------------------------------------------------------------------------------------
// KestrelAuthExpiredBearerTokenException exception class.
//----------------------------------------------------------------------------------------------------------------------
/// <summary>
/// The <see cref="IKestrelAuthService"/> should throw this exception when the specified bearer token has expired.
/// </summary>
public class KestrelAuthExpiredBearerTokenException : Exception {
	private readonly String token;
	private readonly DateTime expires;

	public KestrelAuthExpiredBearerTokenException(String token, DateTime expires) {
		base.HResult = StatusCodes.Status401Unauthorized;
		this.token = token ?? String.Empty;
		this.expires = expires;
	} // KestrelAuthExpiredBearerTokenException

	public String Token {
		get {
			return this.token;
		}
	} // Token

	public DateTime Expires {
		get {
			return this.expires;
		}
	} // Expires

	public override String Message {
		get {
			return $"{base.HResult}. Bearer token '{this.token}' expired at {this.expires:f}.";
		}
	} // Message

} // KestrelAuthExpiredBearerTokenException
#endregion

#region KestrelAuthTwoFactorRequiredException
//----------------------------------------------------------------------------------------------------------------------
// KestrelAuthTwoFactorRequiredException exception class.
//----------------------------------------------------------------------------------------------------------------------
/// <summary>
/// The <see cref="IKestrelAuthService"/> should throw this exception when two-factor authentication is required.
/// </summary>
public class KestrelAuthTwoFactorRequiredException : Exception {
	private readonly KestrelAuthTwoFactorType type;
	private readonly String userIdentification;
	private readonly String userPassword;

	public KestrelAuthTwoFactorRequiredException(KestrelAuthTwoFactorType type, String userIdentification, String userPassword) {
		base.HResult = StatusCodes.Status426UpgradeRequired;
		this.type = type;
		this.userIdentification = userIdentification ?? String.Empty;
		this.userPassword = userPassword ?? String.Empty;
	} // KestrelAuthTwoFactorRequiredException

	public KestrelAuthTwoFactorType Type {
		get {
			return this.type;
		}
	} // Type

	public String UserIdentification {
		get {
			return this.userIdentification;
		}
	} // UserIdentification

	public String UserPassword {
		get {
			return this.userPassword;
		}
	} // UserPassword

	public override String Message {
		get {
			return $"{base.HResult}. User identification '{this.userIdentification}' requires {this.type} two-factor authentication.";
		}
	} // Message

} // KestrelAuthTwoFactorRequiredException
#endregion

#region KestrelAuthInvalidTwoFactorException
//----------------------------------------------------------------------------------------------------------------------
// KestrelAuthInvalidTwoFactorException exception class.
//----------------------------------------------------------------------------------------------------------------------
/// <summary>
/// The <see cref="IKestrelAuthService"/> should throw this exception when two-factor code is invalid.
/// </summary>
public class KestrelAuthInvalidTwoFactorException : Exception {
	private readonly KestrelAuthTwoFactorType type;
	private readonly String userIdentification;
	private readonly String userPassword;
	private readonly String twoFactorCode;

	public KestrelAuthInvalidTwoFactorException(KestrelAuthTwoFactorType type, String userIdentification, String userPassword, String twoFactorCode) {
		base.HResult = StatusCodes.Status401Unauthorized;
		this.type = type;
		this.userIdentification = userIdentification ?? String.Empty;
		this.userPassword = userPassword ?? String.Empty;
		this.twoFactorCode = twoFactorCode ?? String.Empty;
	} // KestrelAuthInvalidTwoFactorException

	public KestrelAuthTwoFactorType Type {
		get {
			return this.type;
		}
	} // Type

	public String UserIdentification {
		get {
			return this.userIdentification;
		}
	} // UserIdentification

	public String UserPassword {
		get {
			return this.userPassword;
		}
	} // UserPassword

	public String TwoFactorCode {
		get {
			return this.twoFactorCode;
		}
	} // TwoFactorCode

	public override String Message {
		get {
			return $"{base.HResult}. User identification '{this.userIdentification}' two-factor code {this.twoFactorCode} is invalid.";
		}
	} // Message

} // KestrelAuthInvalidTwoFactorException
#endregion

#region KestrelAuthExpiredTwoFactorException
//----------------------------------------------------------------------------------------------------------------------
// KestrelAuthExpiredTwoFactorException exception class.
//----------------------------------------------------------------------------------------------------------------------
/// <summary>
/// The <see cref="IKestrelAuthService"/> should throw this exception when two-factor code has expired.
/// </summary>
public class KestrelAuthExpiredTwoFactorException : Exception {
	private readonly KestrelAuthTwoFactorType type;
	private readonly String userIdentification;
	private readonly String userPassword;
	private readonly String twoFactorCode;
	private readonly DateTime expires;

	public KestrelAuthExpiredTwoFactorException(KestrelAuthTwoFactorType type, String userIdentification, String userPassword, String twoFactorCode, DateTime expires) {
		base.HResult = StatusCodes.Status401Unauthorized;
		this.type = type;
		this.userIdentification = userIdentification ?? String.Empty;
		this.userPassword = userPassword ?? String.Empty;
		this.twoFactorCode = twoFactorCode ?? String.Empty;
		this.expires = expires;
	} // KestrelAuthExpiredTwoFactorException

	public KestrelAuthTwoFactorType Type {
		get {
			return this.type;
		}
	} // Type

	public String UserIdentification {
		get {
			return this.userIdentification;
		}
	} // UserIdentification

	public String UserPassword {
		get {
			return this.userPassword;
		}
	} // UserPassword

	public String TwoFactorCode {
		get {
			return this.twoFactorCode;
		}
	} // TwoFactorCode

	public DateTime Expires {
		get {
			return this.expires;
		}
	} // Expires

	public override String Message {
		get {
			return $"{base.HResult}. User identification '{this.userIdentification}' two-factor code {this.twoFactorCode} expired at {this.expires:f}.";
		}
	} // Message

} // KestrelAuthExpiredTwoFactorException
#endregion
