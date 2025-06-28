using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Correlation;
using Shared.Extensions;
using Shared.Models;

namespace Shared.Services;

/// <summary>
/// Service for exposing manager-specific metrics using OpenTelemetry.
/// Provides comprehensive manager operation metrics for analysis and monitoring.
/// Uses consistent labeling from appsettings configuration (Name and Version).
/// </summary>
public class ManagerMetricsService : IManagerMetricsService, IDisposable
{
    private readonly ManagerConfiguration _config;
    private readonly ILogger<ManagerMetricsService> _logger;
    private readonly Meter _meter;

    // Request Processing Metrics
    private readonly Counter<long> _requestsProcessedCounter;
    private readonly Counter<long> _requestsSuccessfulCounter;
    private readonly Counter<long> _requestsFailedCounter;
    private readonly Histogram<double> _requestDurationHistogram;

    // Entity Operation Metrics
    private readonly Counter<long> _entitiesCreatedCounter;
    private readonly Counter<long> _entitiesUpdatedCounter;
    private readonly Counter<long> _entitiesDeletedCounter;
    private readonly Counter<long> _entitiesQueriedCounter;

    // Validation Metrics
    private readonly Counter<long> _validationErrorsCounter;
    private readonly Counter<long> _validationSuccessCounter;
    private readonly Histogram<double> _validationDurationHistogram;

    // Health Metrics
    private readonly Gauge<int> _healthStatusGauge;
    private readonly Counter<long> _healthCheckCounter;

    public ManagerMetricsService(
        IOptions<ManagerConfiguration> config,
        IConfiguration configuration,
        ILogger<ManagerMetricsService> logger)
    {
        _config = config.Value;
        _logger = logger;

        // Use the recommended unique meter name pattern: {Version}_{Name}
        var meterName = $"{_config.Version}_{_config.Name}";
        _meter = new Meter($"{meterName}.Manager");

        // Initialize request processing metrics
        _requestsProcessedCounter = _meter.CreateCounter<long>(
            "manager_requests_processed_total",
            "Total number of requests processed by the manager");

        _requestsSuccessfulCounter = _meter.CreateCounter<long>(
            "manager_requests_successful_total",
            "Total number of requests that completed successfully");

        _requestsFailedCounter = _meter.CreateCounter<long>(
            "manager_requests_failed_total",
            "Total number of requests that failed");

        _requestDurationHistogram = _meter.CreateHistogram<double>(
            "manager_request_duration_seconds",
            "Duration of request processing in seconds");

        // Initialize entity operation metrics
        _entitiesCreatedCounter = _meter.CreateCounter<long>(
            "manager_entities_created_total",
            "Total number of entities created");

        _entitiesUpdatedCounter = _meter.CreateCounter<long>(
            "manager_entities_updated_total",
            "Total number of entities updated");

        _entitiesDeletedCounter = _meter.CreateCounter<long>(
            "manager_entities_deleted_total",
            "Total number of entities deleted");

        _entitiesQueriedCounter = _meter.CreateCounter<long>(
            "manager_entities_queried_total",
            "Total number of entity queries processed");

        // Initialize validation metrics
        _validationErrorsCounter = _meter.CreateCounter<long>(
            "manager_validation_errors_total",
            "Total number of validation errors");

        _validationSuccessCounter = _meter.CreateCounter<long>(
            "manager_validation_success_total",
            "Total number of successful validations");

        _validationDurationHistogram = _meter.CreateHistogram<double>(
            "manager_validation_duration_seconds",
            "Duration of validation operations in seconds");

        // Initialize health metrics
        _healthStatusGauge = _meter.CreateGauge<int>(
            "manager_health_status",
            "Current health status of the manager (0=Healthy, 1=Degraded, 2=Unhealthy)");

        _healthCheckCounter = _meter.CreateCounter<long>(
            "manager_health_checks_total",
            "Total number of health checks performed");
    }

    /// <summary>
    /// Records request processing completion metrics.
    /// </summary>
    public void RecordRequestProcessed(bool success, TimeSpan duration, string operation, string? entityType = null)
    {
        var tags = CreateManagerTags(
            ("operation", operation),
            ("entity_type", entityType ?? "unknown"));

        _requestsProcessedCounter.Add(1, tags);

        if (success)
        {
            _requestsSuccessfulCounter.Add(1, tags);
        }
        else
        {
            _requestsFailedCounter.Add(1, tags);
        }

        _requestDurationHistogram.Record(duration.TotalSeconds, tags);

        _logger.LogDebugWithCorrelation(
            "Recorded request processing metrics: Operation={Operation}, Success={Success}, Duration={Duration}ms",
            operation, success, duration.TotalMilliseconds);
    }

    /// <summary>
    /// Records entity operation metrics.
    /// </summary>
    public void RecordEntityOperation(string operation, string entityType, int count = 1)
    {
        var tags = CreateManagerTags(
            ("operation", operation),
            ("entity_type", entityType));

        switch (operation.ToLowerInvariant())
        {
            case "create":
                _entitiesCreatedCounter.Add(count, tags);
                break;
            case "update":
                _entitiesUpdatedCounter.Add(count, tags);
                break;
            case "delete":
                _entitiesDeletedCounter.Add(count, tags);
                break;
            case "query":
                _entitiesQueriedCounter.Add(count, tags);
                break;
        }
    }

    /// <summary>
    /// Records validation metrics.
    /// </summary>
    public void RecordValidation(bool success, TimeSpan duration, string validationType)
    {
        var tags = CreateManagerTags(
            ("validation_type", validationType));

        if (success)
        {
            _validationSuccessCounter.Add(1, tags);
        }
        else
        {
            _validationErrorsCounter.Add(1, tags);
        }

        _validationDurationHistogram.Record(duration.TotalSeconds, tags);
    }

    /// <summary>
    /// Records health status.
    /// </summary>
    public void RecordHealthStatus(int status)
    {
        var tags = CreateManagerTags();
        _healthStatusGauge.Record(status, tags);
        _healthCheckCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a generic operation with timing and success status.
    /// </summary>
    public void RecordOperation(string operation, string entityType, TimeSpan duration, bool success)
    {
        // Record as both request processing and entity operation
        RecordRequestProcessed(success, duration, operation, entityType);
        RecordEntityOperation(operation, entityType, 1);
    }

    /// <summary>
    /// Creates consistent manager tags using configuration values.
    /// Uses Name and Version from ManagerConfiguration for consistent labeling.
    /// </summary>
    private TagList CreateManagerTags(params (string Key, object? Value)[] additionalTags)
    {
        var baseTags = new List<(string Key, object? Value)>
        {
            ("manager_name", _config.Name),
            ("manager_version", _config.Version),
            ("manager_composite_key", _config.GetCompositeKey())
        };

        baseTags.AddRange(additionalTags);

        return MetricsExtensions.CreateCorrelationTagList(baseTags.ToArray());
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}
