using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
namespace RpcScandinavia.Core.Kestrel;

/// <summary>
/// The Kestrel HTTP application options.
/// It implements <see cref="IHostEnvironment"/> and <see cref="IApplicationBuilder"/> so everything can be configured
/// in one place.
///
/// Because of that, it depends on the <see cref="IServiceProvider"/> and must be used with <see cref="KestrelServiceCollectionExtensions.AddKestrelHttpApplication(IServiceCollection, Action{KestrelHttpApplicationOptions})"/>
/// or with <see cref="OptionsServiceCollectionExtensions.Configure{T}(IServiceCollection, Action{KestrelHttpApplicationOptions})"/>.
/// </summary>
public class KestrelHttpApplicationOptions : IHostEnvironment, IApplicationBuilder {
	private IHostEnvironment environment;
	private IApplicationBuilder app;

	//------------------------------------------------------------------------------------------------------------------
	// Constructors.
	//------------------------------------------------------------------------------------------------------------------
	public KestrelHttpApplicationOptions() {
		// The IHostEnvironment and the IApplicationBuilder are created in the ApplicationServices property setter,
		// when the service provider is set by the KestrelHttpApplicationOptionsSetup service.
		this.environment = null;
		this.app = null;
	} // KestrelHttpApplicationOptions

	//------------------------------------------------------------------------------------------------------------------
	// IHostEnvironment properties.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets or sets the name of the application. This property is automatically set to the assembly containing the
	/// application entry point.
	/// </summary>
	public String ApplicationName {
		get {
			return this.environment.ApplicationName;
		}
		set {
			this.environment.ApplicationName = value;
		}
	} // ApplicationName

	/// <summary>
	/// Gets or sets an <see cref="IFileProvider"/> pointing at <see cref="ContentRootPath"/>.
	/// </summary>
	public IFileProvider ContentRootFileProvider {
		get {
			return this.environment.ContentRootFileProvider;
		}
		set {
			this.environment.ContentRootFileProvider = value;
		}
	} // ContentRootFileProvider

	/// <summary>
	/// Gets or sets the absolute path to the directory that contains the application content files.
	/// </summary>
	public String ContentRootPath {
		get {
			return this.environment.ContentRootPath;
		}
		set {
			this.environment.ContentRootPath = value;
		}
	} // ContentRootPath

	/// <summary>
	/// Gets or sets the name of the environment.
	/// The host automatically sets this property to the value of the "environment" key as specified in configuration.
	/// </summary>
	public String EnvironmentName {
		get {
			return this.environment.EnvironmentName;
		}
		set {
			this.environment.EnvironmentName = value;
		}
	} // EnvironmentName

	//------------------------------------------------------------------------------------------------------------------
	// IApplicationBuilder properties.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets the <see cref="IServiceProvider"/> for application services.
	/// This is set by the <see cref="KestrelHttpApplicationOptionsSetup"/> service.
	/// </summary>
	public IServiceProvider ApplicationServices {
		get {
			return this.app.ApplicationServices;
		}
		set {
			// Create the IApplicationBuilder.
			// This requires the service provider.
			if (this.app == null) {
				this.app = new KestrelHttpApplicationBuilder(value);
			} else {
				this.app.ApplicationServices = value;
			}

			// Get the IHostEnvironment from the service provider.
			this.environment = value.GetRequiredService<IHostEnvironment>();
		}
	} // ApplicationServices

	/// <summary>
	/// Gets the <see cref="IFeatureCollection"/> for server features.
	/// </summary>
	/// <remarks>
	/// An empty collection is returned if a server wasn't specified for the application builder.
	/// </remarks>
	public IFeatureCollection ServerFeatures {
		get {
			return this.app.ServerFeatures;
		}
	} // ServerFeatures

	/// <summary>
	/// Gets a set of properties for <see cref="KestrelHttpApplicationBuilder"/>.
	/// </summary>
	public IDictionary<String, Object> Properties {
		get {
			return this.app.Properties;
		}
	} // Properties

	/// <summary>
	/// Creates a copy of this application builder.
	/// <para>
	/// The created clone has the same properties as the current instance, but does not copy the request pipeline.
	/// </para>
	/// </summary>
	/// <returns>The cloned instance.</returns>
	public IApplicationBuilder New() {
		return this.app.New();
	} // New

	/// <summary>
	/// Adds the middleware to the application request pipeline.
	/// </summary>
	/// <param name="middleware">The middleware.</param>
	/// <returns>An instance of <see cref="IApplicationBuilder"/> after the operation has completed.</returns>
	public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware) {
		return this.app.Use(middleware);
	} // Use

	/// <summary>
	/// Produces a <see cref="RequestDelegate"/> that executes added middlewares.
	/// </summary>
	/// <returns>The <see cref="RequestDelegate"/>.</returns>
	public RequestDelegate Build() {
		return this.app.Build();
	} // Build

} // KestrelHttpApplicationOptions
