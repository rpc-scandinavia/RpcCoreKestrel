using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
namespace RpcScandinavia.Core.Kestrel;

/// <summary>
/// Useful extension methods when using Kestrel authentication middleware.
/// </summary>
public static class KestrelAuthExtensions {

	/// <summary>
	/// Adds the Kestrel authentication service, which must be an implementation of <see cref="IKestrelAuthService"/>.
	/// This requires that you use the <see cref="KestrelAuthMiddleware"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <typeparam name="TKestrelAuthService">The Kestrel authentication service type, used by the Kestrel authentication middleware.</typeparam>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddKestrelAuthentication<TKestrelAuthService>(this IServiceCollection services) where TKestrelAuthService : class, IKestrelAuthService {
		services.AddSingleton<IKestrelAuthService, TKestrelAuthService>();

		return services;
	} // AddKestrelAuthentication

	/// <summary>
	/// Adds the Kestrel authentication middleware, <see cref="KestrelAuthMiddleware"/>.
	/// This requires that you write and add a service implementing <see cref="IKestrelAuthService"/>.
	/// </summary>
	/// <param name="builder">The application builder, typically <see cref="KestrelHttpApplicationOptions"/>.</param>
	/// <returns>The application builder.</returns>
	public static IApplicationBuilder UseKestrelAuthentication(this IApplicationBuilder builder) {
		builder.UseMiddleware<KestrelAuthMiddleware>();

		return builder;
	} // UseKestrelAuthentication

} // KestrelAuthExtensions
