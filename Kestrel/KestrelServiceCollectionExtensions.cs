using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using RpcScandinavia.Repository;
namespace RpcScandinavia.Core.Kestrel;

/// <summary>
/// Useful extension methods when using <see cref="KestrelHttpApplication"/>.
/// </summary>
public static class KestrelServiceCollectionExtensions {

	/// <summary>
	/// Adds the <see cref="KestrelHttpApplication"/> singleton service, and the implemented interface services as
	/// singleton factories. The implemented interface services are <see cref="IHost"/>, <see cref="IHttpApplication"/>,
	/// <see cref="IHostedService"/>, <see cref="IHostEnvironment"/> and <see cref="IHostApplicationLifetime"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddKestrelHttpApplication(this IServiceCollection services) {
		return KestrelServiceCollectionExtensions.AddKestrelHttpApplication(services, _ => {});
	} // AddKestrelHttpApplication

	/// <summary>
	/// Adds the <see cref="KestrelHttpApplication"/> singleton service, and the implemented interface services as
	/// singleton factories. The implemented interface services are <see cref="IHost"/>, <see cref="IHttpApplication"/>,
	/// <see cref="IHostedService"/>, <see cref="IHostEnvironment"/> and <see cref="IHostApplicationLifetime"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The Kestrel HTTP application options.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddKestrelHttpApplication(this IServiceCollection services, Action<KestrelHttpApplicationOptions> options) {
		// Add the Kestrel HTTP application.
		services.AddSingleton<KestrelHttpApplication>();
		services.AddSingleton<IHostApplicationLifetime>((serviceProvider) => serviceProvider.GetRequiredService<KestrelHttpApplication>());
		services.AddSingleton<IHost>((serviceProvider) => serviceProvider.GetRequiredService<KestrelHttpApplication>());
		services.AddSingleton<IHostedService>((serviceProvider) => serviceProvider.GetRequiredService<KestrelHttpApplication>());
		services.AddSingleton<IHttpApplication<HttpContext>>((serviceProvider) => serviceProvider.GetRequiredService<KestrelHttpApplication>());
		services.TryAddSingleton<IHostEnvironment, HostingEnvironment>();

		// Add required services.
		services.AddHttpContextAccessor();

		// The "IConfigureOptions<KestrelHttpApplicationOptions>" must be added before the options are configures.
		services.AddTransient<IConfigureOptions<KestrelHttpApplicationOptions>, KestrelHttpApplicationOptionsSetup>();
		services.Configure<KestrelHttpApplicationOptions>(options);

		return services;
	} // AddKestrelHttpApplication

	/// <summary>
	/// Adds the <see cref="KestrelServer"/> as a <see cref="IServer"/> singleton service.
	/// Use the <see cref="KestrelHttpApplication"/> to run the Kestrel server, see <see cref="AddKestrelHttpApplication"/>.
	/// Thw following required services are added:
	/// * <see cref="KestrelServer"/> as <see cref="IServer"/>
	/// * <see cref="SocketTransportFactory"/> as <see cref="IConnectionListenerFactory"/>
	/// * <see cref="DiagnosticListener"/>
	/// * <see cref="HttpsConfigurationService"/> as <see cref="IHttpsConfigurationService"/>
	/// * <see cref="KestrelMetrics"/>
	/// * <see cref="KestrelServerOptionsSetup"/> as <see cref="IConfigureOptions{KestrelServerOptions}"/>
	/// The following required options are configured:
	/// * <see cref="SocketTransportOptions"/> with its default values.
	/// * <see cref="SocketTransportOptions"/> with its default values.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddKestrel(this IServiceCollection services) {
		return KestrelServiceCollectionExtensions.AddKestrel(services, _ => {});
	} // AddKestrel

	/// <summary>
	/// Adds the <see cref="KestrelServer"/> as a <see cref="IServer"/> singleton service.
	/// Use the <see cref="KestrelHttpApplication"/> to run the Kestrel server, see <see cref="AddKestrelHttpApplication"/>.
	/// Thw following required services are added:
	/// * <see cref="KestrelServer"/> as <see cref="IServer"/>
	/// * <see cref="SocketTransportFactory"/> as <see cref="IConnectionListenerFactory"/>
	/// * <see cref="DiagnosticListener"/>
	/// * <see cref="HttpsConfigurationService"/> as <see cref="IHttpsConfigurationService"/>
	/// * <see cref="KestrelMetrics"/>
	/// * <see cref="KestrelServerOptionsSetup"/> as <see cref="IConfigureOptions{KestrelServerOptions}"/>
	/// The following required options are configured:
	/// * <see cref="SocketTransportOptions"/> from the options parameter.
	/// * <see cref="SocketTransportOptions"/> with its default values.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The Kestrel server options.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddKestrel(this IServiceCollection services, Action<KestrelServerOptions> options) {
		services.TryAddSingleton<IServer, KestrelServer>();

		services.TryAddSingleton<IConnectionListenerFactory, SocketTransportFactory>();
		services.Configure<SocketTransportOptions>(_ => {});
		services.TryAddSingleton<DiagnosticListener>(new DiagnosticListener("KestrelHttpApplication"));

		// Add some required Microsoft internal services, by using reflection.
		services.TryAddSingleton(
			KestrelServiceCollectionExtensions.GetInternalType(typeof(KestrelServer), "Microsoft.AspNetCore.Miscellaneous.Kestrel.Core.IHttpsConfigurationService"),
			KestrelServiceCollectionExtensions.GetInternalType(typeof(KestrelServer), "Microsoft.AspNetCore.Miscellaneous.Kestrel.Core.HttpsConfigurationService")
		);
		services.TryAddSingleton(
			KestrelServiceCollectionExtensions.GetInternalType(typeof(KestrelServer), "Microsoft.AspNetCore.Miscellaneous.Kestrel.Core.Internal.Infrastructure.KestrelMetrics")
		);
		services.AddTransient(
			typeof(IConfigureOptions<KestrelServerOptions>),
			KestrelServiceCollectionExtensions.GetInternalType(typeof(KestrelServer), "Microsoft.AspNetCore.Miscellaneous.Kestrel.Core.Internal.KestrelServerOptionsSetup")
		);

		// Add basic services, in case they are missing.
		services.TryAddSingleton<IConfiguration>((_) => new ConfigurationBuilder().Build());
		//services.TryAddSingleton<IMemoryCache, MemoryCache>();

		// The "KestrelServerOptionsSetup" service extends "IConfigureOptions<KestrelServerOptions>", and assigns
		// the service provider to the "ApplicationServices" property in the "KestrelServerOptions" base class.
		// This prevents the "ArgumentNullException: Value cannot be null. (Parameter 'provider')".
		// This must be after the "KestrelServerOptionsSetup" service has been added!
		// https://github.com/dotnet/aspnetcore/blob/main/src/Servers/Kestrel/Core/src/Internal/KestrelServerOptionsSetup.cs#L8
		services.Configure<KestrelServerOptions>(options);

		return services;
	} // AddKestrel

	/// <summary>
	/// Adds the <see cref="ConsoleLifetime"/> as a <see cref="IHostLifetime"/> singleton service.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection.</returns>
	public static IServiceCollection AddConsoleLifetime(this IServiceCollection services) {
		services.TryAddSingleton<IHostLifetime, ConsoleLifetime>();
		services.TryAddSingleton<IHostEnvironment>((serviceProvider) => new HostingEnvironment());
		services.Configure<HostOptions>(_ => {});
		return services;
	} // AddConsoleLifetime

	/*
	public static IServiceCollection AddSystemDLifetime(this IServiceCollection services) {
		services.TryAddSingleton<IHostLifetime, Microsoft.Extensions.Hosting.Systemd.SystemdLifetime>();
		services.TryAddSingleton<IHostEnvironment>((serviceProvider) => new HostingEnvironment());
		services.Configure<HostOptions>(_ => {});
		return services;
	} // AddSystemDLifetime

	public static IServiceCollection AddWindowsServiceLifetime(this IServiceCollection services) {
		services.TryAddSingleton<IHostLifetime, Microsoft.Extensions.Hosting.WindowsServices.WindowsServiceLifetime>();
		services.TryAddSingleton<IHostEnvironment>((serviceProvider) => new HostingEnvironment());
		services.Configure<HostOptions>(_ => {});
		return services;
	} // AddWindowsServiceLifetime
	*/

	private static Type GetInternalType(Type publicTypeInSameAssembly, String internalTypeName) {
		return publicTypeInSameAssembly.Assembly.GetType(internalTypeName);
	} // GetInternalType

} // KestrelServiceCollectionExtensions
