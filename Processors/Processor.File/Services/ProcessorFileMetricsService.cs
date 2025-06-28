using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Correlation;
using Shared.Processor.Models;
using Shared.Processor.Services;
using System.Diagnostics.Metrics;

namespace Processor.File.Services;

/// <summary>
/// Service for recording processor file metrics.
/// </summary>
public class ProcessorFileMetricsService : IProcessorFileMetricsService
{
    private readonly ProcessorConfiguration _config;
    private readonly ILogger<ProcessorFileMetricsService> _logger;
    private readonly IProcessorMetricsLabelService _labelService;
    private readonly Meter _meter;

    private readonly Counter<long> _deserializedInputDataSuccessfulCounter;
    public ProcessorFileMetricsService(
        IOptions<ProcessorConfiguration> config,
        ILogger<ProcessorFileMetricsService> logger,
        IProcessorMetricsLabelService labelService)
    {
        _config = config.Value;
        _logger = logger;
        _labelService = labelService;

        // Use the recommended unique meter name pattern: {Version}_{Name}
        var meterName = $"{_config.Version}_{_config.Name}";
        var fullMeterName = $"{meterName}.File";

        _meter = new Meter(fullMeterName);

        // Initialize Deserialized InputData metrics 

        _deserializedInputDataSuccessfulCounter = _meter.CreateCounter<long>(
            "processor_deserialized_input_data_successful_total",
            "Total number of input data deserializations completed successfully");

        _logger.LogInformationWithCorrelation(
            "ProcessorFileMetricsService initialized with meter name: {MeterName}, Composite Key: {CompositeKey}",
            $"{meterName}.File", _labelService.ProcessorCompositeKey);
    }

    public void RecordDeserializedInputData(string processorName, string processorVersion, Guid? orchestratedFlowId = null)
    {
        var tags = _labelService.GetSystemLabels().ToList();

        // Add orchestrated flow ID label if provided
        if (orchestratedFlowId.HasValue && orchestratedFlowId != Guid.Empty)
        {
            tags.Add(new KeyValuePair<string, object?>("orchestrated_flow_id", orchestratedFlowId.ToString()));
        }

        _deserializedInputDataSuccessfulCounter.Add(1, tags.ToArray());
        
        _logger.LogDebugWithCorrelation(
            "🔥 DEBUG: Recorded Deserialized InputData metrics - ProcessorName: {ProcessorName}, CompositeKey: {CompositeKey}, OrchestratedFlowId: {OrchestratedFlowId}",
            processorName, _labelService.ProcessorCompositeKey, orchestratedFlowId);
    }

    public void Dispose()
    {
        _meter?.Dispose();
    }
}
