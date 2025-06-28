using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Shared.Models;

namespace Manager.Orchestrator.Services;

/// <summary>
/// Centralized service for generating consistent orchestrator metrics labels.
/// Reads configuration from ManagerConfiguration and ensures all metrics use the same labeling standards.
/// Follows the same design pattern as ProcessorMetricsLabelService for consistency.
/// </summary>
public class OrchestratorMetricsLabelService : IOrchestratorMetricsLabelService
{
    private readonly ManagerConfiguration _config;
    private readonly string _instanceId;
    private readonly string _compositeKey;
    private readonly KeyValuePair<string, object?>[] _standardLabels;

    public OrchestratorMetricsLabelService(IOptions<ManagerConfiguration> config)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        
        // Validate required configuration
        if (string.IsNullOrWhiteSpace(_config.Name))
            throw new InvalidOperationException("ManagerConfiguration.Name is required");
        if (string.IsNullOrWhiteSpace(_config.Version))
            throw new InvalidOperationException("ManagerConfiguration.Version is required");

        // Generate unique instance ID for this orchestrator instance
        _instanceId = Guid.NewGuid().ToString();
        
        // Generate composite key using configuration
        _compositeKey = _config.GetCompositeKey();
        
        // Pre-build standard labels for performance
        _standardLabels = CreateStandardLabels();
    }

    public string OrchestratorCompositeKey => _compositeKey;
    public string OrchestratorInstanceId => _instanceId;

    public KeyValuePair<string, object?>[] GetStandardLabels()
    {
        // Return a copy to prevent modification of the cached version
        return _standardLabels.ToArray();
    }

    public KeyValuePair<string, object?>[] GetStepPublishLabels(string stepVersionName, string operation = "publish_step_command", string status = "success")
    {
        var labels = new List<KeyValuePair<string, object?>>(_standardLabels);
        labels.Add(new KeyValuePair<string, object?>("step_version_name", stepVersionName));
        labels.Add(new KeyValuePair<string, object?>("operation", operation));
        labels.Add(new KeyValuePair<string, object?>("status", status));
        return labels.ToArray();
    }

    public KeyValuePair<string, object?>[] GetStepConsumptionLabels(string stepVersionName, string orchestratedFlowVersionName, string operation = "process_activity_event", string status = "success")
    {
        var labels = new List<KeyValuePair<string, object?>>(_standardLabels);
        labels.Add(new KeyValuePair<string, object?>("step_version_name", stepVersionName));
        labels.Add(new KeyValuePair<string, object?>("orchestrated_flow_version_name", orchestratedFlowVersionName));
        labels.Add(new KeyValuePair<string, object?>("operation", operation));
        labels.Add(new KeyValuePair<string, object?>("status", status));
        return labels.ToArray();
    }

    public KeyValuePair<string, object?>[] GetFlowLabels(string orchestratedFlowVersionName, string operation, string status = "success")
    {
        var labels = new List<KeyValuePair<string, object?>>(_standardLabels);
        labels.Add(new KeyValuePair<string, object?>("orchestrated_flow_version_name", orchestratedFlowVersionName));
        labels.Add(new KeyValuePair<string, object?>("operation", operation));
        labels.Add(new KeyValuePair<string, object?>("status", status));
        return labels.ToArray();
    }

    public KeyValuePair<string, object?>[] GetCacheLabels(string operationType, string status = "success")
    {
        var labels = new List<KeyValuePair<string, object?>>(_standardLabels);
        labels.Add(new KeyValuePair<string, object?>("operation_type", operationType));
        labels.Add(new KeyValuePair<string, object?>("status", status));
        return labels.ToArray();
    }

    public KeyValuePair<string, object?>[] GetCacheLabels()
    {
        return GetStandardLabels();
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

    private KeyValuePair<string, object?>[] CreateStandardLabels()
    {
        return new KeyValuePair<string, object?>[]
        {
            new("orchestrator_composite_key", _compositeKey),
            new("orchestrator_name", _config.Name),
            new("orchestrator_version", _config.Version),
            new("orchestrator_id", _instanceId),
            new("environment", "Development") // Default environment, could be made configurable
            // Removed 'instance' and 'job' labels to avoid conflicts with OpenTelemetry resource labels
        };
    }
}
