using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
namespace RpcScandinavia.Core.Kestrel;

/// <summary>
/// Useful extension methods when configuring the Kestrel server with <see cref="KestrelServerOptions"/>.
/// </summary>
public static class KestrelOptionsExtensions {

	/// <summary>
	/// Configure the use of a Unix Socked or Windows Pipe for interprocess communication.
	/// </summary>
	/// <param name="options">The Kestrel server options.</param>
	/// <param name="localSocketName">The socket name. Defaults to the executing assembly name.</param>
	/// <param name="unixSocketPath">The unix socket path. Defaults to the temporary directory.</param>
	/// <returns>The Kestrel server options.</returns>
	public static KestrelServerOptions ConfigureSocket(this KestrelServerOptions options, String localSocketName = null, String unixSocketPath = null) {
		// Validate.
		if (String.IsNullOrWhiteSpace(localSocketName) == true) {
			localSocketName = Assembly.GetExecutingAssembly().GetName().Name;
		}
		if (Directory.Exists(unixSocketPath) == false) {
			unixSocketPath = Path.GetTempPath();
		}

		if (OperatingSystem.IsWindows() == true) {
			// Use Pipe on Windows.
			options.ListenNamedPipe(
				localSocketName,
				(listenOptions) => {
					listenOptions.Protocols = HttpProtocols.Http2;
				}
			);
		} else {
			// Use Unix Socket on Linux.
			String socketPath = Path.Join(unixSocketPath, localSocketName);
			options.ListenUnixSocket(
				socketPath,
				(listenOptions) => {
					listenOptions.Protocols = HttpProtocols.Http2;
				}
			);

			// Ensure the old socket is removed before creating a new one.
			if (File.Exists(socketPath) == true) {
				File.Delete(socketPath);
			}
		}

		return options;
	} // ConfigureSocket

	/// <summary>
	/// Configures the HTTPS options.
	/// * Allow any client certificate.
	/// </summary>
	/// <param name="options">The Kestrel server options.</param>
	/// <returns>The Kestrel server options.</returns>
	public static KestrelServerOptions ConfigureHttpsOptions(this KestrelServerOptions options) {
		options.ConfigureHttpsDefaults((httpsOptions) => {
			httpsOptions.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
			httpsOptions.AllowAnyClientCertificate();
		});

		return options;
	} // ConfigureHttpsOptions

	/// <summary>
	/// Configures the use of HTTPS listening on <value>localhost</value>.
	/// </summary>
	/// <param name="options">The Kestrel server options.</param>
	/// <param name="port">The post number. Defaults to port 443.</param>
	/// <param name="serverCertificate">The server certificate. Defaults to a new self-signed certificate.</param>
	/// <returns>The Kestrel server options.</returns>
	public static KestrelServerOptions ConfigureLocalhostAsHttps(this KestrelServerOptions options, Int32 port = 443, X509Certificate2 serverCertificate = null) {
		// Validate
		if ((port < 0) || (port > 65535)) {
			port = 443;
		}
		if (serverCertificate == null) {
			serverCertificate = KestrelOptionsExtensions.CreateSelfSignedCertificate();
		}

		options.ListenLocalhost(
			port,
			(listenOptions) => {
				listenOptions.Protocols = HttpProtocols.Http2;
				listenOptions.UseHttps(serverCertificate);
			}
		);

		return options;
	} // ConfigureLocalhostAsHttps

	/// <summary>
	/// Configures the use of HTTPS listening on all network interfaces.
	/// </summary>
	/// <param name="options">The Kestrel server options.</param>
	/// <param name="port">The post number. Defaults to port 443.</param>
	/// <param name="serverCertificate">The server certificate. Defaults to a new self-signed certificate.</param>
	/// <returns>The Kestrel server options.</returns>
	public static KestrelServerOptions ConfigureAnyIpAsHttps(this KestrelServerOptions options, Int32 port = 443, X509Certificate2 serverCertificate = null) {
		// Validate
		if ((port < 0) || (port > 65535)) {
			port = 443;
		}
		if (serverCertificate == null) {
			serverCertificate = KestrelOptionsExtensions.CreateSelfSignedCertificate();
		}

		options.ListenAnyIP(
			port,
			(listenOptions) => {
				listenOptions.Protocols = HttpProtocols.Http2;
				listenOptions.UseHttps(serverCertificate);
			}
		);

		return options;
	} // ConfigureAnyIpAsHttps

	/// <summary>
	/// Creates a self-signet certificate.
	/// </summary>
	/// <returns></returns>
	private static X509Certificate2 CreateSelfSignedCertificate() {
		using (RSA rsa = RSA.Create(2048)) {
			String localSocketName = Assembly.GetExecutingAssembly().GetName().Name;
			CertificateRequest request = new CertificateRequest($"CN={localSocketName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

			// Generate the certificate
			DateTimeOffset validFrom = DateTimeOffset.Now;
			DateTimeOffset validUntil = validFrom.AddYears(1);
			X509Certificate2 certificate = request.CreateSelfSigned(validFrom, validUntil);

			return certificate;
		}
	} // CreateSelfSignedCertificate

} // KestrelOptionsExtensions
