using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RpcScandinavia.Repository;
namespace RpcScandinavia.Core.Kestrel;

/// <summary>
/// Useful extension methods when using Kestrel authentication middleware.
/// </summary>
public static class KestrelAuthExtensions {

	/// <summary>
	/// Adds the Kestrel authentication service <see cref="KestrelMemoryAuthService"/>, that performs the authentication
	/// from data stored in-memory. It implements <see cref="IKestrelAuthService"/> and requires the <see cref="KestrelAuthMiddleware"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddKestrelMemoryAuthentication(this IServiceCollection services) {
		return KestrelAuthExtensions.AddKestrelMemoryAuthentication(services, _ => {});
	} // AddKestrelMemoryAuthentication

	/// <summary>
	/// Adds the Kestrel authentication service <see cref="KestrelMemoryAuthService"/>, that performs the authentication
	/// from data stored in-memory. It implements <see cref="IKestrelAuthService"/> and requires the <see cref="KestrelAuthMiddleware"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The Kestrel in-memory authorization options.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddKestrelMemoryAuthentication(this IServiceCollection services, Action<KestrelMemoryAuthServiceOptions> options) {
		services.TryAddSingleton<IKestrelAuthService, KestrelMemoryAuthService>();
		services.Configure<KestrelMemoryAuthServiceOptions>(options);

		return services;
	} // AddKestrelMemoryAuthentication

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
