using System;
using Microsoft.Extensions.Options;
namespace RpcScandinavia.Core.Kestrel;

/// <summary>
/// This will configure the <see cref="KestrelHttpApplicationOptions"/> instance with the <see cref="IServiceProvider"/>.
/// </summary>
public class KestrelHttpApplicationOptionsSetup : IConfigureOptions<KestrelHttpApplicationOptions> {
	private readonly IServiceProvider services;

	public KestrelHttpApplicationOptionsSetup(IServiceProvider services) {
		this.services = services;
	} // KestrelHttpApplicationOptionsSetup

	public void Configure(KestrelHttpApplicationOptions options) {
		options.ApplicationServices = this.services;
	} // Configure

} // KestrelHttpApplicationOptionsSetup
