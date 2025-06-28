using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Shared.MongoDB;
using Shared.Services;


namespace Shared.Configuration;

/// <summary>
/// Configuration extension methods for MongoDB setup and dependency injection.
/// </summary>
public static class MongoDbConfiguration
{
    /// <summary>
    /// Adds MongoDB services and repository registration to the service collection.
    /// </summary>
    /// <typeparam name="TInterface">The repository interface type.</typeparam>
    /// <typeparam name="TImplementation">The repository implementation type.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="databaseName">The default database name to use.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddMongoDb<TInterface, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration,
        string databaseName)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        // Configure BSON serialization
        BsonConfiguration.Configure();

        // Register MongoDB client and database
        services.AddSingleton<IMongoClient>(provider =>
        {
            var connectionString = configuration.GetConnectionString("MongoDB");
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            return new MongoClient(settings);
        });

        services.AddScoped<IMongoDatabase>(provider =>
        {
            var client = provider.GetRequiredService<IMongoClient>();
            var configuredDatabaseName = configuration.GetValue<string>("MongoDB:DatabaseName") ?? databaseName;
            return client.GetDatabase(configuredDatabaseName);
        });

        // Register event publisher
        services.AddScoped<IEventPublisher, EventPublisher>();

        // Register generic repository
        services.AddScoped<TInterface, TImplementation>();

        return services;
    }

    /// <summary>
    /// Adds MongoDB services with a specific repository type.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="databaseName">The default database name to use.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddMongoDb(
        this IServiceCollection services,
        IConfiguration configuration,
        string databaseName)
    {
        // Configure BSON serialization
        BsonConfiguration.Configure();

        // Register MongoDB client and database
        services.AddSingleton<IMongoClient>(provider =>
        {
            var connectionString = configuration.GetConnectionString("MongoDB");
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            return new MongoClient(settings);
        });

        services.AddScoped<IMongoDatabase>(provider =>
        {
            var client = provider.GetRequiredService<IMongoClient>();
            var configuredDatabaseName = configuration.GetValue<string>("MongoDB:DatabaseName") ?? databaseName;
            return client.GetDatabase(configuredDatabaseName);
        });

        // Register event publisher
        services.AddScoped<IEventPublisher, EventPublisher>();

        return services;
    }
}
