using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Shared.Processor.Models;

namespace Shared.Processor.Services;

/// <summary>
/// Centralized service for generating consistent processor metrics labels.
/// Reads configuration from ProcessorConfiguration and ensures all metrics use the same labeling standards.
/// </summary>
public class ProcessorMetricsLabelService : IProcessorMetricsLabelService
{
    private readonly ProcessorConfiguration _config;
    private readonly string _instanceId;
    private readonly string _compositeKey;
    private readonly KeyValuePair<string, object?>[] _standardLabels;

    public ProcessorMetricsLabelService(IOptions<ProcessorConfiguration> config)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        
        // Validate required configuration
        if (string.IsNullOrWhiteSpace(_config.Name))
            throw new InvalidOperationException("ProcessorConfiguration.Name is required");
        if (string.IsNullOrWhiteSpace(_config.Version))
            throw new InvalidOperationException("ProcessorConfiguration.Version is required");

        // Generate unique instance ID for this processor instance
        _instanceId = Guid.NewGuid().ToString();
        
        // Generate composite key using configuration
        _compositeKey = _config.GetCompositeKey();
        
        // Pre-build standard labels for performance
        _standardLabels = CreateStandardLabels();
    }

    public string ProcessorCompositeKey => _compositeKey;
    public string ProcessorInstanceId => _instanceId;

    public KeyValuePair<string, object?>[] GetStandardLabels()
    {
        // Return a copy to prevent modification of the cached version
        return _standardLabels.ToArray();
    }

    public KeyValuePair<string, object?>[] GetActivityLabels(string correlationId, string executionId, string stepId, string? orchestratedFlowId = null, string status = "success")
    {
        var labels = new List<KeyValuePair<string, object?>>(_standardLabels);
        labels.Add(new KeyValuePair<string, object?>("correlation_id", correlationId));
        labels.Add(new KeyValuePair<string, object?>("execution_id", executionId));
        labels.Add(new KeyValuePair<string, object?>("step_id", stepId));
        labels.Add(new KeyValuePair<string, object?>("status", status));

        if (!string.IsNullOrWhiteSpace(orchestratedFlowId))
        {
            labels.Add(new KeyValuePair<string, object?>("orchestrated_flow_id", orchestratedFlowId));
        }

        return labels.ToArray();
    }

    public KeyValuePair<string, object?>[] GetFileLabels(string correlationId, string fileType)
    {
        var labels = new List<KeyValuePair<string, object?>>(_standardLabels);
        labels.Add(new KeyValuePair<string, object?>("correlation_id", correlationId));
        labels.Add(new KeyValuePair<string, object?>("file_type", fileType));
        return labels.ToArray();
    }

    public KeyValuePair<string, object?>[] GetHealthLabels(string healthCheckName, string healthCheckStatus)
    {
        var labels = new List<KeyValuePair<string, object?>>(_standardLabels);
        labels.Add(new KeyValuePair<string, object?>("health_check_name", healthCheckName));
        labels.Add(new KeyValuePair<string, object?>("health_check_status", healthCheckStatus));
        return labels.ToArray();
    }

    public KeyValuePair<string, object?>[] GetSystemLabels()
    {
        return GetStandardLabels();
    }

    public KeyValuePair<string, object?>[] GetCacheLabels()
    {
        return GetStandardLabels();
    }

    private KeyValuePair<string, object?>[] CreateStandardLabels()
    {
        return new KeyValuePair<string, object?>[]
        {
            new("processor_composite_key", _compositeKey),
            new("processor_name", _config.Name),
            new("processor_version", _config.Version),
            new("processor_id", _instanceId),
            new("environment", _config.Environment)
            // Removed 'instance' and 'job' labels to avoid conflicts with OpenTelemetry resource labels
        };
    }
}
