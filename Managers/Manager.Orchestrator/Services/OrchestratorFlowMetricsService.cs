using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Correlation;
using Shared.Extensions;
using Shared.Models;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Service for recording orchestrator flow metrics optimized for anomaly detection.
/// Follows the processor pattern with focused metrics: consume counter, publish counter, and anomaly detection.
/// Reduces metric volume while focusing on important operational issues.
/// Uses IOrchestratorMetricsLabelService for consistent labeling across all metrics.
/// </summary>
public class OrchestratorFlowMetricsService : IOrchestratorFlowMetricsService
{
    private readonly ManagerConfiguration _config;
    private readonly ILogger<OrchestratorFlowMetricsService> _logger;
    private readonly IOrchestratorMetricsLabelService _labelService;
    private readonly Meter _meter;

    // Core Flow Metrics (Optimized for Anomaly Detection)
    private readonly Counter<long> _commandsConsumedCounter;
    private readonly Counter<long> _commandsConsumedSuccessfulCounter;
    private readonly Counter<long> _commandsConsumedFailedCounter;

    private readonly Counter<long> _eventsPublishedCounter;
    private readonly Counter<long> _eventsPublishedSuccessfulCounter;
    private readonly Counter<long> _eventsPublishedFailedCounter;

    // Anomaly Detection Metric
    private readonly Gauge<long> _flowAnomalyGauge;

    public OrchestratorFlowMetricsService(
        IOptions<ManagerConfiguration> config,
        ILogger<OrchestratorFlowMetricsService> logger,
        IOrchestratorMetricsLabelService labelService)
    {
        _config = config.Value;
        _logger = logger;
        _labelService = labelService;

        // Use the recommended unique meter name pattern: {Version}_{Name}
        var meterName = $"{_config.Version}_{_config.Name}";
        _meter = new Meter($"{meterName}.Flow");

        // Initialize core flow metrics
        (_commandsConsumedCounter, _commandsConsumedSuccessfulCounter, _commandsConsumedFailedCounter) = InitializeCommandConsumptionMetrics();
        (_eventsPublishedCounter, _eventsPublishedSuccessfulCounter, _eventsPublishedFailedCounter) = InitializeEventPublishingMetrics();
        _flowAnomalyGauge = InitializeFlowAnomalyMetrics();

        _logger.LogInformationWithCorrelation(
            "✅ OrchestratorFlowMetricsService initialized with meter name: {MeterName}, Composite Key: {CompositeKey}",
            $"{meterName}.Flow", _labelService.OrchestratorCompositeKey);
    }

    private (Counter<long>, Counter<long>, Counter<long>) InitializeCommandConsumptionMetrics()
    {
        var commandsConsumedCounter = _meter.CreateCounter<long>(
            "orchestrator_commands_consumed_total",
            "Total number of commands consumed by the orchestrator");

        var commandsConsumedSuccessfulCounter = _meter.CreateCounter<long>(
            "orchestrator_commands_consumed_successful_total",
            "Total number of commands consumed successfully by the orchestrator");

        var commandsConsumedFailedCounter = _meter.CreateCounter<long>(
            "orchestrator_commands_consumed_failed_total",
            "Total number of commands that failed to be consumed by the orchestrator");

        _logger.LogInformationWithCorrelation(
            "✅ TRACE: Command consumption metrics initialized - orchestrator_commands_consumed_*");

        return (commandsConsumedCounter, commandsConsumedSuccessfulCounter, commandsConsumedFailedCounter);
    }

    private (Counter<long>, Counter<long>, Counter<long>) InitializeEventPublishingMetrics()
    {
        var eventsPublishedCounter = _meter.CreateCounter<long>(
            "orchestrator_events_published_total",
            "Total number of events published by the orchestrator");

        var eventsPublishedSuccessfulCounter = _meter.CreateCounter<long>(
            "orchestrator_events_published_successful_total",
            "Total number of events published successfully by the orchestrator");

        var eventsPublishedFailedCounter = _meter.CreateCounter<long>(
            "orchestrator_events_published_failed_total",
            "Total number of events that failed to be published by the orchestrator");

        _logger.LogInformationWithCorrelation(
            "✅ TRACE: Event publishing metrics initialized - orchestrator_events_published_*");

        return (eventsPublishedCounter, eventsPublishedSuccessfulCounter, eventsPublishedFailedCounter);
    }

    private Gauge<long> InitializeFlowAnomalyMetrics()
    {
        var flowAnomalyGauge = _meter.CreateGauge<long>(
            "orchestrator_flow_anomaly",
            "Flow anomaly detection metric showing difference between consumed and published counts");

        _logger.LogInformationWithCorrelation(
            "✅ TRACE: Flow anomaly metrics initialized - orchestrator_flow_anomaly");

        return flowAnomalyGauge;
    }

    /// <summary>
    /// Records ExecuteActivityCommand consumption metrics (activity events consumed by orchestrator)
    /// </summary>
    /// <param name="success">Whether the command was consumed successfully</param>
    /// <param name="orchestratedFlowId">The orchestrated flow ID for filtering (optional)</param>
    public void RecordCommandConsumed(bool success, Guid? orchestratedFlowId = null)
    {
        var tags = _labelService.GetSystemLabels().ToList();

        // Add orchestrated flow ID label if provided
        if (orchestratedFlowId.HasValue && orchestratedFlowId != Guid.Empty)
        {
            tags.Add(new KeyValuePair<string, object?>("orchestrated_flow_id", orchestratedFlowId.ToString()));
        }

        _commandsConsumedCounter.Add(1, tags.ToArray());

        if (success)
        {
            _commandsConsumedSuccessfulCounter.Add(1, tags.ToArray());
        }
        else
        {
            _commandsConsumedFailedCounter.Add(1, tags.ToArray());
        }

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded command consumed metrics - CompositeKey: {CompositeKey}, Success: {Success}, OrchestratedFlowId: {OrchestratedFlowId}, Total: +1, Successful: {SuccessfulIncrement}, Failed: {FailedIncrement}",
            _labelService.OrchestratorCompositeKey, success, orchestratedFlowId, success ? "+1" : "+0", success ? "+0" : "+1");
    }

    /// <summary>
    /// Records activity event publishing metrics (step commands published by orchestrator)
    /// </summary>
    /// <param name="success">Whether the event was published successfully</param>
    /// <param name="eventType">Type of event published</param>
    /// <param name="orchestratedFlowId">The orchestrated flow ID for filtering (optional)</param>
    public void RecordEventPublished(bool success, string eventType, Guid? orchestratedFlowId = null)
    {
        var tags = _labelService.GetSystemLabels().ToList();
        tags.Add(new KeyValuePair<string, object?>("event_type", eventType));

        // Add orchestrated flow ID label if provided
        if (orchestratedFlowId.HasValue && orchestratedFlowId != Guid.Empty)
        {
            tags.Add(new KeyValuePair<string, object?>("orchestrated_flow_id", orchestratedFlowId.ToString()));
        }

        _eventsPublishedCounter.Add(1, tags.ToArray());

        if (success)
        {
            _eventsPublishedSuccessfulCounter.Add(1, tags.ToArray());
        }
        else
        {
            _eventsPublishedFailedCounter.Add(1, tags.ToArray());
        }

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded event published metrics - EventType: {EventType}, CompositeKey: {CompositeKey}, Success: {Success}, OrchestratedFlowId: {OrchestratedFlowId}, Total: +1, Successful: {SuccessfulIncrement}, Failed: {FailedIncrement}",
            eventType, _labelService.OrchestratorCompositeKey, success, orchestratedFlowId, success ? "+1" : "+0", success ? "+0" : "+1");
    }

    /// <summary>
    /// Records flow anomaly detection metrics
    /// </summary>
    /// <param name="consumedCount">Number of commands consumed</param>
    /// <param name="publishedCount">Number of events published</param>
    /// <param name="orchestratedFlowId">The orchestrated flow ID for filtering (optional)</param>
    public void RecordFlowAnomaly(long consumedCount, long publishedCount, Guid? orchestratedFlowId = null)
    {
        var difference = Math.Abs(consumedCount - publishedCount);
        var anomalyStatus = difference > 0 ? "anomaly_detected" : "healthy";

        var tags = _labelService.GetSystemLabels().ToList();
        tags.Add(new KeyValuePair<string, object?>("anomaly_status", anomalyStatus));

        // Add orchestrated flow ID label if provided
        if (orchestratedFlowId.HasValue && orchestratedFlowId != Guid.Empty)
        {
            tags.Add(new KeyValuePair<string, object?>("orchestrated_flow_id", orchestratedFlowId.ToString()));
        }

        _flowAnomalyGauge.Record(difference, tags.ToArray());

        if (difference > 0)
        {
            _logger.LogWarningWithCorrelation(
                "🚨 Flow anomaly detected: CompositeKey={CompositeKey}, Consumed={Consumed}, Published={Published}, Difference={Difference}, OrchestratedFlowId={OrchestratedFlowId}",
                _labelService.OrchestratorCompositeKey, consumedCount, publishedCount, difference, orchestratedFlowId);
        }
        else
        {
            _logger.LogDebugWithCorrelation(
                "🔥 DEBUG: Recorded flow anomaly metrics - CompositeKey: {CompositeKey}, Status: {AnomalyStatus}, Consumed: {Consumed}, Published: {Published}, Difference: {Difference}, OrchestratedFlowId: {OrchestratedFlowId}",
                _labelService.OrchestratorCompositeKey, anomalyStatus, consumedCount, publishedCount, difference, orchestratedFlowId);
        }
    }

    public void Dispose()
    {
        _meter?.Dispose();
        _logger.LogInformationWithCorrelation("OrchestratorFlowMetricsService disposed");
    }
}
