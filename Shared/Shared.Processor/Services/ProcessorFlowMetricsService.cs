using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Correlation;
using Shared.Processor.Models;
using System.Diagnostics.Metrics;

namespace Shared.Processor.Services;

/// <summary>
/// Service for recording processor flow metrics optimized for anomaly detection.
/// Follows the orchestrator pattern with focused metrics: consume counter, publish counter, and anomaly detection.
/// Reduces metric volume while focusing on important operational issues.
/// </summary>
public class ProcessorFlowMetricsService : IProcessorFlowMetricsService
{
    private readonly ProcessorConfiguration _config;
    private readonly ILogger<ProcessorFlowMetricsService> _logger;
    private readonly IProcessorMetricsLabelService _labelService;
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
    
    public ProcessorFlowMetricsService(
        IOptions<ProcessorConfiguration> config,
        ILogger<ProcessorFlowMetricsService> logger,
        IProcessorMetricsLabelService labelService)
    {
        _config = config.Value;
        _logger = logger;
        _labelService = labelService;

        // Use the recommended unique meter name pattern: {Version}_{Name}
        var meterName = $"{_config.Version}_{_config.Name}";
        _meter = new Meter($"{meterName}.Flow");

        // Initialize command consumption metrics (Core for Anomaly Detection)
        _commandsConsumedCounter = _meter.CreateCounter<long>(
            "processor_commands_consumed_total",
            "Total number of ExecuteActivityCommand messages consumed by the processor");

        _commandsConsumedSuccessfulCounter = _meter.CreateCounter<long>(
            "processor_commands_consumed_successful_total",
            "Total number of ExecuteActivityCommand messages successfully consumed");

        _commandsConsumedFailedCounter = _meter.CreateCounter<long>(
            "processor_commands_consumed_failed_total",
            "Total number of ExecuteActivityCommand messages that failed to consume");

        // Initialize event publishing metrics (Core for Anomaly Detection)
        _eventsPublishedCounter = _meter.CreateCounter<long>(
            "processor_events_published_total",
            "Total number of activity events published by the processor");

        _eventsPublishedSuccessfulCounter = _meter.CreateCounter<long>(
            "processor_events_published_successful_total",
            "Total number of activity events successfully published");

        _eventsPublishedFailedCounter = _meter.CreateCounter<long>(
            "processor_events_published_failed_total",
            "Total number of activity events that failed to publish");

        // Initialize flow anomaly detection metric
        _flowAnomalyGauge = _meter.CreateGauge<long>(
            "processor_flow_anomaly_difference",
            "Absolute difference between consumed commands and published events (anomaly indicator)");

        
        _logger.LogInformationWithCorrelation(
            "ProcessorFlowMetricsService initialized with meter name: {MeterName}, Composite Key: {CompositeKey}",
            $"{meterName}.Flow", _labelService.ProcessorCompositeKey);
    }

    public void RecordCommandConsumed(bool success, string processorName, string processorVersion, Guid? orchestratedFlowId = null)
    {
        var tags = _labelService.GetSystemLabels().ToList();

        // Add orchestrated flow ID label if provided
        if (orchestratedFlowId.HasValue && orchestratedFlowId != Guid.Empty)
        {
            tags.Add(new KeyValuePair<string, object?>("orchestrated_flow_id", orchestratedFlowId.ToString()));
        }

        _commandsConsumedCounter.Add(1, tags.ToArray());

        if (success)
            _commandsConsumedSuccessfulCounter.Add(1, tags.ToArray());
        else
            _commandsConsumedFailedCounter.Add(1, tags.ToArray());

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded command consumed metrics - ProcessorName: {ProcessorName}, CompositeKey: {CompositeKey}, Success: {Success}, OrchestratedFlowId: {OrchestratedFlowId}, Total: +1, Successful: {SuccessfulIncrement}, Failed: {FailedIncrement}",
            processorName, _labelService.ProcessorCompositeKey, success, orchestratedFlowId, success ? "+1" : "+0", success ? "+0" : "+1");
    }

    public void RecordEventPublished(bool success, string eventType, string processorName, string processorVersion, Guid? orchestratedFlowId = null)
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
            _eventsPublishedSuccessfulCounter.Add(1, tags.ToArray());
        else
            _eventsPublishedFailedCounter.Add(1, tags.ToArray());

        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded event published metrics - ProcessorName: {ProcessorName}, CompositeKey: {CompositeKey}, EventType: {EventType}, Success: {Success}, OrchestratedFlowId: {OrchestratedFlowId}, Total: +1, Successful: {SuccessfulIncrement}, Failed: {FailedIncrement}",
            processorName, _labelService.ProcessorCompositeKey, eventType, success, orchestratedFlowId, success ? "+1" : "+0", success ? "+0" : "+1");
    }

    public void RecordFlowAnomaly(string processorName, string processorVersion, long consumedCount, long publishedCount, Guid? orchestratedFlowId = null)
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
                "🚨 Flow anomaly detected for processor {ProcessorName}: Consumed={Consumed}, Published={Published}, Difference={Difference}, OrchestratedFlowId={OrchestratedFlowId}",
                processorName, consumedCount, publishedCount, difference, orchestratedFlowId);
        }
        else
        {
            _logger.LogDebugWithCorrelation(
                "🔥 DEBUG: Recorded flow anomaly metrics - ProcessorName: {ProcessorName}, CompositeKey: {CompositeKey}, Status: {AnomalyStatus}, Consumed: {Consumed}, Published: {Published}, Difference: {Difference}, OrchestratedFlowId: {OrchestratedFlowId}",
                processorName, _labelService.ProcessorCompositeKey, anomalyStatus, consumedCount, publishedCount, difference, orchestratedFlowId);
        }
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}
