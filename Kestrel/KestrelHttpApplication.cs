using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace RpcScandinavia.Core.Kestrel;

/// <summary>
/// The Kestrel HTTP application can be used to run a Kestrel server, typically manually added to the service collection
/// without using the Microsoft build classes. Use the extension methods from <see cref="KestrelServiceCollectionExtensions"/> to add the
/// required services to the service collection.
///
/// Even though this implements <see cref="IHostEnvironment"/> and <see cref="IApplicationBuilder"/>, it is easier to
/// configure the environment and middleware by using the <see cref="KestrelHttpApplicationOptions"/> through
/// the <see cref="KestrelServiceCollectionExtensions.AddKestrelHttpApplication(IServiceCollection, Action{KestrelHttpApplicationOptions})"/> extension method
/// or directly by <see cref="OptionsConfigurationServiceCollectionExtensions.Configure{KestrelHttpApplicationOptions}(Microsoft.Extensions.DependencyInjection.IServiceCollection,Microsoft.Extensions.Configuration.IConfiguration)"/>.
///
/// The Kestrel server is available by the service provider as a <see cref="IServer"/>.
///
/// This class implements the <see cref="IHost"/>, <see cref="IHttpApplication{HttpContext}"/>, <see cref="IHostedService"/>,
/// <see cref="IHostEnvironment"/> and <see cref="IHostApplicationLifetime"/> interfaces.
/// </summary>
public class KestrelHttpApplication : IHttpApplication<HttpContext>, IHost, IHostedService, IHostEnvironment, IHostApplicationLifetime, IDisposable, IAsyncDisposable {
	private readonly IServiceProvider provider;
	private readonly ILogger<KestrelHttpApplication> logger;
	private readonly IOptions<KestrelHttpApplicationOptions> options;
	private readonly IServer server;
	private RequestDelegate pipeline;

	// IHostApplicationLifetime properties.
	public CancellationTokenSource applicationStarted;
	public CancellationTokenSource applicationStopped;
	public CancellationTokenSource applicationStopping;

	//------------------------------------------------------------------------------------------------------------------
	// Constructors.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Instantiates a new Kestrel HTTP application.
	/// </summary>
	/// <param name="provider">The service provider.</param>
	/// <param name="logger">The logging service.</param>
	/// <param name="options">The Kestrel HTTP application options.</param>
	/// <param name="server">The Kestrel server, <see cref="KestrelServer"/>.</param>
	public KestrelHttpApplication(IServiceProvider provider, ILogger<KestrelHttpApplication> logger, IOptions<KestrelHttpApplicationOptions> options, IServer server) {
		this.provider = provider;
		this.logger = logger;
		this.options = options;
		this.server = server;
		this.pipeline = null;

		// IHostApplicationLifetime properties.
		this.applicationStarted = new CancellationTokenSource();
		this.applicationStopped = new CancellationTokenSource();
		this.applicationStopping = new CancellationTokenSource();
	} // KestrelHttpApplication

	public void Dispose() {
		this.server?.Dispose();
	} // Dispose

	public async ValueTask DisposeAsync() {
		// Stop the server.
		await this.StopAsync();

		if (this.server != null) {
			await CastAndDispose(this.server);
		}

		if (this.applicationStarted != null) {
			await CastAndDispose(this.applicationStarted);
		}

		if (this.applicationStopped != null) {
			await CastAndDispose(this.applicationStopped);
		}

		if (this.applicationStopping != null) {
			await CastAndDispose(this.applicationStopping);
		}

		return;

		static async ValueTask CastAndDispose(IDisposable resource) {
			if (resource is IAsyncDisposable resourceAsyncDisposable) {
				await resourceAsyncDisposable.DisposeAsync();
			} else {
				resource.Dispose();
			}
		}
	} // DisposeAsync

	//------------------------------------------------------------------------------------------------------------------
	// Pipeline properties and methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Use the application builder to add middleware to this Kestrel HTTP application.
	/// </summary>
	public IApplicationBuilder ApplicationBuilder {
		get {
			return this.options.Value;
		}
	} // ApplicationBuilder

	//------------------------------------------------------------------------------------------------------------------
	// IHostEnvironment properties.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets or sets the name of the application. This property is automatically set to the assembly containing the
	/// application entry point.
	/// </summary>
	public String ApplicationName {
		get {
			return this.options.Value.ApplicationName;
		}
		set {
			this.options.Value.ApplicationName = value;
		}
	} // ApplicationName

	/// <summary>
	/// Gets or sets an <see cref="IFileProvider"/> pointing at <see cref="ContentRootPath"/>.
	/// </summary>
	public IFileProvider ContentRootFileProvider {
		get {
			return this.options.Value.ContentRootFileProvider;
		}
		set {
			this.options.Value.ContentRootFileProvider = value;
		}
	} // ContentRootFileProvider

	/// <summary>
	/// Gets or sets the absolute path to the directory that contains the application content files.
	/// </summary>
	public String ContentRootPath {
		get {
			return this.options.Value.ContentRootPath;
		}
		set {
			this.options.Value.ContentRootPath = value;
		}
	} // ContentRootPath

	/// <summary>
	/// Gets or sets the name of the environment.
	/// The host automatically sets this property to the value of the "environment" key as specified in configuration.
	/// </summary>
	public String EnvironmentName {
		get {
			return this.options.Value.EnvironmentName;
		}
		set {
			this.options.Value.EnvironmentName = value;
		}
	} // EnvironmentName

	//------------------------------------------------------------------------------------------------------------------
	// IHost properties and methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets the service provider.
	/// </summary>
	public IServiceProvider Services {
		get {
			return this.provider;
		}
	} // Services

	/// <summary>
	/// Starts the Kestrel HTTP application, and listen for connections.
	/// The application will run until interrupted or until<see cref="StopAsync"/> or  <see cref="StopApplication"/> is called.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token, that can be used to cancel the Start operation.</param>
	public async Task StartAsync(CancellationToken cancellationToken = new CancellationToken()) {
		try {
			// Build the request pipeline.
			if (this.pipeline == null) {
				this.pipeline = this.options.Value.Build();
			}

			// Start ConsoleLifetime to listen for Ctrl+C.
			IHostApplicationLifetime lifetime = provider.GetRequiredService<IHostApplicationLifetime>();
			if (lifetime != null) {
				IHostLifetime lifetimeService = provider.GetRequiredService<IHostLifetime>();
				Task lifetimeTask = lifetimeService.WaitForStartAsync(cancellationToken);
			}

			// Log.
			if (this.logger.IsEnabled(LogLevel.Information) == true) {
				this.logger.LogInformation($"Starting Kestrel server.");
			}

			// Start the Kestrel server.
			await this.server.StartAsync(this, cancellationToken);
			await this.applicationStarted.CancelAsync();

			//// Wait for shutdown signal
			//await lifetimeTask;
			//await server.StopAsync(CancellationToken.None);
		} catch (Exception exception) {
			// Log.
			if (this.logger.IsEnabled(LogLevel.Error) == true) {
				this.logger.LogError(exception, $"Error starting Kestrel server: {exception.Message}");
			}
		}
	} // StartAsync

	/// <summary>
	/// Attempts to gracefully stop the Kestrel HTTP application.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token, that can be used to cancel the Stop operation.</param>
	public async Task StopAsync(CancellationToken cancellationToken = new CancellationToken()) {
		try {
			// Log.
			if (this.logger.IsEnabled(LogLevel.Information) == true) {
				this.logger.LogInformation($"Stopping Kestrel server.");
			}

			// Stop the Kestrel server.
			await this.applicationStopping.CancelAsync();
			await this.server.StopAsync(cancellationToken);
			await this.applicationStopped.CancelAsync();
		} catch (Exception exception) {
			// Log.
			if (this.logger.IsEnabled(LogLevel.Error) == true) {
				this.logger.LogError(exception, $"Error stopping Kestrel server: {exception.Message}");
			}
		} finally {
			// Reset the IHostApplicationLifetime cancellation token sources.
			if (this.applicationStarted.TryReset() == false) {
				this.applicationStarted = new CancellationTokenSource();
			}

			if (this.applicationStopped.TryReset() == false) {
				this.applicationStopped = new CancellationTokenSource();
			}

			if (this.applicationStopping.TryReset() == false) {
				this.applicationStopping = new CancellationTokenSource();
			}
		}
	} // StopAsync

	//------------------------------------------------------------------------------------------------------------------
	// IHostApplicationLifetime methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Attempts to gracefully stop the Kestrel HTTP application.
	/// This calls the <see cref="StopAsync"/> method.
	/// </summary>
	public void StopApplication() {
		Task.Run(() => this.StopAsync());
	} // StopApplication

	/// <summary>
	/// Gets a cancellation token. Triggered when the Kestrel HTTP application has fully started.
	/// </summary>
	public CancellationToken ApplicationStarted {
		get {
			return this.applicationStarted.Token;
		}
	} // ApplicationStarted

	/// <summary>
	/// Gets a cancellation token. Triggered when the Kestrel HTTP application has completed a graceful shutdown.
	/// The application will not exit until all callbacks registered on this token have completed.
	/// </summary>
	public CancellationToken ApplicationStopped {
		get {
			return this.applicationStopped.Token;
		}
	} // ApplicationStopped

	/// <summary>
	/// Gets a cancellation token. Triggered when the Kestrel HTTP application is starting a graceful shutdown.
	/// Shutdown will block until all callbacks registered on this token have completed.
	/// </summary>
	public CancellationToken ApplicationStopping {
		get {
			return this.applicationStopping.Token;
		}
	} // ApplicationStopping

	//------------------------------------------------------------------------------------------------------------------
	// Context methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Instantiates the new HTTP context for the current request, and assigns a new service scope to it.
	/// </summary>
	/// <param name="contextFeatures">The context features.</param>
	/// <returns>The new HTTP context.</returns>
	public HttpContext CreateContext(IFeatureCollection contextFeatures) {
		// Create a new HTTP context that uses a new service scope.
		IServiceScope scope = this.provider.CreateScope();
		DefaultHttpContext context = new DefaultHttpContext(contextFeatures);
		context.RequestServices = scope.ServiceProvider;
		return context;
	} // CreateContext

	/// <summary>
	/// Run the middleware pipeline.
	/// </summary>
	/// <param name="context">The HTTP context of the current request.</param>
	public async Task ProcessRequestAsync(HttpContext context) {
		// Run the middleware pipeline.
		await this.pipeline(context);
	} // ProcessRequestAsync

	/// <summary>
	/// Disposes the HTTP context of the current request.
	/// </summary>
	/// <param name="context">The HTTP context.</param>
	/// <param name="exception">The exception thrown when handling the current request, or null if the request was handled successfully.</param>
	public void DisposeContext(HttpContext context, Exception exception) {
		// Dispose the HTTP context service scope.
		(context.RequestServices as IDisposable)?.Dispose();

		if (exception != null) {
			// Log.
			if (this.logger.IsEnabled(LogLevel.Error) == true) {
				this.logger.LogError(exception, $"Error: {exception.Message}");
			}
		}
	} // DisposeContext

} // KestrelHttpApplication
