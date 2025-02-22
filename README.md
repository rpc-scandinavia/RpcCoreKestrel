[![.NET](https://github.com/rpc-scandinavia/RpcCoreKestrel/actions/workflows/dotnet.yml/badge.svg)](https://github.com/rpc-scandinavia/RpcCoreKestrel/actions/workflows/dotnet.yml)
[![GitHub](https://img.shields.io/github/license/rpc-scandinavia/RpcCoreKestrel?logo=github)](https://github.com/rpc-scandinavia/RpcCoreKestrel/blob/master/LICENSE)

# RpcCoreKestrel
RPC Core Kestrel contains interfaces and classes used setup Kestrel without using the Microsoft builder classes.

### Example of setting up a Kestrel server with gRPC
This example only show the Kestrel setup, using RpcCoreKestrel instead of the Microsoft builders.
```
IServiceCollection services;

services.AddLogging((options) => {
	options.SetMinimumLevel(LogLevel.Information);
	options.AddConsole();
});

services.AddKestrel((options) => {
	// Default listen options.
	options.ConfigureEndpointDefaults((listenOptions) => {
		listenOptions.Protocols = HttpProtocols.Http2;
	});

	// Allow any client certificate.
	options.ConfigureHttpsOptions();

	// Local socket.
	options.ConfigureSocket("SocketName", "/tmp");

	// Local endpoint.
	options.ConfigureLocalhostAsHttps(12345);

	// Any endpoint.
	options.ConfigureAnyIpAsHttps(443);
});

services.AddKestrelHttpApplication((options) => {
	// Configure middleware.
	options.UseKestrelAuthentication();
	options.UseRouting();
	options.UseEndpoints((endpoints) => {
		endpoints.MapGrpcService<TestService>();
	});
});

services.AddKestrelAuthentication<MyAuthMiddleware>();	// Implements IKestrelAuthService.
services.AddRouting();
services.AddGrpc();
services.AddConsoleLifetime();
services.AddSingleton<TimeProvider>(TimeProvider.System);
... and other services ...

// Build the service provider and run the host.
IServiceProvider provider = services.BuildServiceProvider();
KestrelHttpApplication host = this.provider.GetService<KestrelHttpApplication>();
host.Run();
```
