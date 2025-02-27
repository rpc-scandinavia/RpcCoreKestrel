using System;
namespace RpcScandinavia.Core.Kestrel;

public enum KestrelAuthTwoFactorType {

	/// <summary>
	/// No two-factor authentication type.
	/// </summary>
	None = 0,

	/// <summary>
	/// Time-based one-time password (TOTP) two-factor authentication type.
	/// Time-based one-time password is a computer algorithm that generates a one-time password using the current time
	/// as a source of uniqueness. As an extension of the HMAC-based one-time password algorithm, it has been adopted as
	/// Internet Engineering Task Force standard RFC 6238.
	/// </summary>
	/// <remarks>
	/// Use the OTP.NET assembly from <c>https://www.nuget.org/packages/Otp.NET</c>, to handle TOTP codes.
	/// </remarks>
	TimebasedOneTimePassword = 1,

	///// <summary>
	///// Hash-based one-time password (HOTP) two-factor authentication type.
	///// </summary>
	//HashbasedOneTimePassword = 2,

	/// <summary>
	/// Unknown numeric two-factor authentication type.
	/// </summary>
	UnknownNumeric = 3,

} // KestrelAuthTwoFactorType
