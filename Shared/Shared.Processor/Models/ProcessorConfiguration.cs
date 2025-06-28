using System.ComponentModel.DataAnnotations;

namespace Shared.Processor.Models;

/// <summary>
/// Configuration model for processor application settings
/// </summary>
public class ProcessorConfiguration
{
    /// <summary>
    /// Version of the processor (used in composite key)
    /// </summary>
    [Required]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Name of the processor (used in composite key)
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the processor functionality
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Schema ID for validating input data
    /// </summary>
    [Required]
    public Guid InputSchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Schema ID for validating output data
    /// </summary>
    [Required]
    public Guid OutputSchemaId { get; set; } = Guid.Empty;

    /// <summary>
    /// Input schema definition (retrieved at runtime)
    /// </summary>
    public string InputSchemaDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Output schema definition (retrieved at runtime)
    /// </summary>
    public string OutputSchemaDefinition { get; set; } = string.Empty;

    /// <summary>
    /// Environment name for the processor (defaults to Development)
    /// </summary>
    public string Environment { get; set; } = "Development";

    /// <summary>
    /// Gets the composite key for this processor
    /// </summary>
    public string GetCompositeKey() => $"{Version}_{Name}";
}

/// <summary>
/// Configuration model for RabbitMQ settings
/// </summary>
public class RabbitMQConfiguration
{
    public string Host { get; set; } = "localhost";
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public int RetryLimit { get; set; } = 3;
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(30);
    public ushort PrefetchCount { get; set; } = 16;
    public int ConcurrencyLimit { get; set; } = 10;
}

/// <summary>
/// Configuration model for Hazelcast settings
/// </summary>
public class ProcessorHazelcastConfiguration
{
    public string ClusterName { get; set; } = "EntitiesManager";
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

/// <summary>
/// Configuration for processor initialization retry behavior
/// </summary>
public class ProcessorInitializationConfiguration
{
    /// <summary>
    /// Whether to retry initialization endlessly until successful.
    /// When true, processor will never terminate due to initialization failures.
    /// </summary>
    public bool RetryEndlessly { get; set; } = true;

    /// <summary>
    /// Base delay between initialization retry attempts (default: 5 seconds).
    /// This is the initial delay that may be increased with exponential backoff.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum delay between retry attempts (default: 60 seconds).
    /// Prevents exponential backoff from growing too large.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Whether to use exponential backoff for retry delays.
    /// When true, delay increases exponentially up to MaxRetryDelay.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Timeout for individual initialization attempts (default: 30 seconds).
    /// Each retry attempt will timeout after this duration.
    /// </summary>
    public TimeSpan InitializationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to log each retry attempt.
    /// Useful for monitoring but can generate significant log volume.
    /// </summary>
    public bool LogRetryAttempts { get; set; } = true;
}

/// <summary>
/// Configuration for schema validation
/// </summary>
public class SchemaValidationConfiguration
{
    /// <summary>
    /// Enable input schema validation
    /// </summary>
    public bool EnableInputValidation { get; set; } = true;

    /// <summary>
    /// Enable output schema validation
    /// </summary>
    public bool EnableOutputValidation { get; set; } = true;

    /// <summary>
    /// Fail processing on validation error
    /// </summary>
    public bool FailOnValidationError { get; set; } = true;

    /// <summary>
    /// Log validation warnings
    /// </summary>
    public bool LogValidationWarnings { get; set; } = true;

    /// <summary>
    /// Log validation errors
    /// </summary>
    public bool LogValidationErrors { get; set; } = true;

    /// <summary>
    /// Include validation telemetry
    /// </summary>
    public bool IncludeValidationTelemetry { get; set; } = true;
}
