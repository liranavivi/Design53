using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using Shared.Correlation;
using Shared.MassTransit;

namespace Shared.Configuration;

/// <summary>
/// Configuration extension methods for MassTransit setup with RabbitMQ.
/// </summary>
public static class MassTransitConfiguration
{
    /// <summary>
    /// Adds MassTransit with RabbitMQ configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="consumerTypes">The consumer types to register.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddMassTransitWithRabbitMq(
        this IServiceCollection services, 
        IConfiguration configuration, 
        params Type[] consumerTypes)
    {
        services.AddMassTransit(x =>
        {
            // Add consumers dynamically
            foreach (var consumerType in consumerTypes)
            {
                x.AddConsumer(consumerType);
            }

            // Note: Correlation ID filters will be configured per-bus basis
            // as generic filter registration is not available in this MassTransit version

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqSettings = configuration.GetSection("RabbitMQ");

                cfg.Host(rabbitMqSettings["Host"] ?? "localhost", rabbitMqSettings["VirtualHost"] ?? "/", h =>
                {
                    h.Username(rabbitMqSettings["Username"] ?? "guest");
                    h.Password(rabbitMqSettings["Password"] ?? "guest");
                });

                // Configure retry policy
                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromSeconds(30)
                ));

                // Configure endpoints to use message type routing
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
