using Hazelcast;
using Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Shared.Configuration;

/// <summary>
/// Extension methods for configuring Hazelcast client
/// </summary>
public static class HazelcastConfiguration
{
    /// <summary>
    /// Adds Hazelcast client and cache services
    /// </summary>
    public static IServiceCollection AddHazelcastClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var hazelcastConfig = configuration.GetSection("Hazelcast").Get<ServiceHazelcastConfiguration>()
            ?? new ServiceHazelcastConfiguration();

        // Use lazy initialization to avoid blocking during service registration
        services.AddSingleton<Lazy<Task<IHazelcastClient>>>(serviceProvider =>
        {
            return new Lazy<Task<IHazelcastClient>>(async () =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<CacheService>>();

                var options = new HazelcastOptionsBuilder()
                    .With(o =>
                    {
                        o.ClusterName = hazelcastConfig.ClusterName;
                        foreach (var address in hazelcastConfig.NetworkConfig.Addresses)
                        {
                            o.Networking.Addresses.Add(address);
                        }
                    })
                    .Build();

                logger.LogInformation("Connecting to Hazelcast cluster: {ClusterName}", hazelcastConfig.ClusterName);
                return await HazelcastClientFactory.StartNewClientAsync(options);
            });
        });

        // Register cache services
        services.AddSingleton<ICacheService, CacheService>();

        return services;
    }
}

/// <summary>
/// Configuration model for Hazelcast settings
/// </summary>
public class ServiceHazelcastConfiguration
{
    public string ClusterName { get; set; } = string.Empty;
    public NetworkConfiguration NetworkConfig { get; set; } = new();
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public ConnectionRetryConfiguration ConnectionRetryConfig { get; set; } = new();
}

public class NetworkConfiguration
{
    public List<string> Addresses { get; set; } = new() { "127.0.0.1:5701" };
}

public class ConnectionRetryConfiguration
{
    public int InitialBackoffMillis { get; set; } = 1000;
    public int MaxBackoffMillis { get; set; } = 30000;
    public double Multiplier { get; set; } = 2.0;
    public int ClusterConnectTimeoutMillis { get; set; } = 20000;
    public double JitterRatio { get; set; } = 0.2;
}

