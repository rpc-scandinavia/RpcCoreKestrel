using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Internal;
using Microsoft.AspNetCore.Builder;
namespace RpcScandinavia.Core.Kestrel;

/// <summary>
/// Kestrel HTTP application implementation for <see cref="IApplicationBuilder"/>.
/// </summary>
public class KestrelHttpApplicationBuilder : IApplicationBuilder {
	private const String ServerFeaturesKey = "server.Features";
	private const String ApplicationServicesKey = "application.Services";
	private const String MiddlewareDescriptionsKey = "__MiddlewareDescriptions";
	private const String RequestUnhandledKey = "__RequestUnhandled";

	private IServiceProvider serviceProvider;
	private readonly IDictionary<String, Object> properties;
	private readonly List<Func<RequestDelegate, RequestDelegate>> components;
	private readonly List<String> descriptions;

	//------------------------------------------------------------------------------------------------------------------
	// Constructors.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Initializes a new instance of <see cref="KestrelHttpApplicationBuilder"/>.
	/// </summary>
	/// <param name="serviceProvider">The service provider for application services.</param>
	public KestrelHttpApplicationBuilder(IServiceProvider serviceProvider) : this(serviceProvider, new FeatureCollection()) {
	} // KestrelHttpApplicationBuilder

	/// <summary>
	/// Initializes a new instance of <see cref="KestrelHttpApplicationBuilder"/>.
	/// </summary>
	/// <param name="serviceProvider">The service provider for application services.</param>
	/// <param name="server">The server instance that hosts the application.</param>
	public KestrelHttpApplicationBuilder(IServiceProvider serviceProvider, Object server) {
		this.serviceProvider = serviceProvider;
		this.properties = new Dictionary<String, Object>(StringComparer.Ordinal);
		this.components = new List<Func<RequestDelegate, RequestDelegate>>();
		this.descriptions = new List<String>();

		this.SetProperty<IServiceProvider>(ApplicationServicesKey, this.serviceProvider);
		this.SetProperty(ServerFeaturesKey, server);
	} // KestrelHttpApplicationBuilder

	private KestrelHttpApplicationBuilder(KestrelHttpApplicationBuilder builder) {
		// The CopyOnWriteDictionary is an internal class.
		// this.properties = new CopyOnWriteDictionary<String, Object>(builder.Properties, StringComparer.Ordinal);
		Type copyOnWriteDictionaryType = typeof(SystemClock).Assembly.GetType("Microsoft.Extensions.Internal.CopyOnWriteDictionary");
		copyOnWriteDictionaryType = copyOnWriteDictionaryType.MakeGenericType([ typeof(String), typeof(Object) ]);
		this.properties = (Dictionary<String, Object>)Activator.CreateInstance(copyOnWriteDictionaryType, [builder.Properties, StringComparer.Ordinal]);
	} // KestrelHttpApplicationBuilder

	//------------------------------------------------------------------------------------------------------------------
	// Properties.
	//------------------------------------------------------------------------------------------------------------------
	private T GetProperty<T>(String key) {
		return this.properties.TryGetValue(key, out Object value) ? (T)value : default(T);
	} // GetProperty

	private void SetProperty<T>(String key, T value) {
		this.properties[key] = value;
	} // SetProperty

	private String CreateMiddlewareDescription(Func<RequestDelegate, RequestDelegate> middleware) {
		if (middleware.Target != null) {
			// To IApplicationBuilder, middleware is just a func. Getting a good description is hard.
			// Inspect the incoming func and attempt to resolve it back to a middleware type if possible.
			// UseMiddlewareExtensions adds middleware via a method with the name CreateMiddleware.
			// If this pattern is matched, then ToString on the target returns the middleware type name.
			if (middleware.Method.Name == "CreateMiddleware") {
				return middleware.Target.ToString()!;
			}

			return middleware.Target.GetType().FullName + "." + middleware.Method.Name;
		}

		return middleware.Method.Name.ToString();
	} // CreateMiddlewareDescription

	//------------------------------------------------------------------------------------------------------------------
	// IApplicationBuilder properties and methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets the <see cref="IServiceProvider"/> for application services.
	/// </summary>
	public IServiceProvider ApplicationServices {
		get {
			return this.serviceProvider;
		}
		set {
			this.serviceProvider = value;
			this.SetProperty<IServiceProvider>(ApplicationServicesKey, this.serviceProvider);
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
			return this.GetProperty<IFeatureCollection>(ServerFeaturesKey)!;
		}
	} // ServerFeatures

	/// <summary>
	/// Gets a set of properties for <see cref="KestrelHttpApplicationBuilder"/>.
	/// </summary>
	public IDictionary<String, Object> Properties {
		get {
			return this.properties;
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
		return new KestrelHttpApplicationBuilder(this);
	} // New

	/// <summary>
	/// Adds the middleware to the application request pipeline.
	/// </summary>
	/// <param name="middleware">The middleware.</param>
	/// <returns>An instance of <see cref="IApplicationBuilder"/> after the operation has completed.</returns>
	public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware) {
		this.components.Add(middleware);
		this.descriptions.Add(this.CreateMiddlewareDescription(middleware));		// Set even though it is newer used.

		return this;
	} // Use

	/// <summary>
	/// Produces a <see cref="RequestDelegate"/> that executes added middlewares.
	/// </summary>
	/// <returns>The <see cref="RequestDelegate"/>.</returns>
	public RequestDelegate Build() {
		RequestDelegate app = (context) => {
			// If we reach the end of the pipeline, but we have an endpoint, then something unexpected has happened.
			// This could happen if user code sets an endpoint, but they forgot to add the UseEndpoint middleware.
			Endpoint endpoint = context.GetEndpoint();
			RequestDelegate endpointRequestDelegate = endpoint?.RequestDelegate;
			if (endpointRequestDelegate != null) {
				String message =
					$"The request reached the end of the pipeline without executing the endpoint: '{endpoint!.DisplayName}'. " +
					$"Please register the EndpointMiddleware using '{nameof(IApplicationBuilder)}.UseEndpoints(...)' if using routing.";
				throw new InvalidOperationException(message);
			}

			// Flushing the response and calling through to the next middleware in the pipeline is
			// a user error, but don't attempt to set the status code if this happens. It leads to a confusing
			// behavior where the client response looks fine, but the server side logic results in an exception.
			if (!context.Response.HasStarted) {
				context.Response.StatusCode = StatusCodes.Status404NotFound;
			}

			// Communicates to higher layers that the request wasn't handled by the app pipeline.
			context.Items[RequestUnhandledKey] = true;

			return Task.CompletedTask;
		};

		for (Int32 count = this.components.Count - 1; count >= 0; count--) {
			app = this.components[count](app);
		}

		return app;
	} // Build

} // KestrelHttpApplicationBuilder
