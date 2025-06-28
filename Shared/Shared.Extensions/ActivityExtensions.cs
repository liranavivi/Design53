using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shared.Models;
using Shared.Correlation;

namespace Shared.Extensions;

/// <summary>
/// Extension methods for Activity to add common tags
/// </summary>
public static class ActivityExtensions
{
    /// <summary>
    /// Sets common processor tags on an activity
    /// </summary>
    public static Activity? SetProcessorTags(this Activity? activity, Guid processorId, string name, string version)
    {
        return activity?
            .SetTag(ActivityTags.ProcessorId, processorId.ToString())
            .SetTag(ActivityTags.ProcessorName, name)
            .SetTag(ActivityTags.ProcessorVersion, version);
    }

    /// <summary>
    /// Sets activity execution tags on an activity
    /// </summary>
    public static Activity? SetActivityExecutionTags(this Activity? activity, 
        Guid orchestratedFlowEntityId, 
        Guid stepId, 
        Guid executionId, 
        ActivityExecutionStatus status)
    {
        return activity?
            .SetTag(ActivityTags.OrchestratedFlowEntityId, orchestratedFlowEntityId.ToString())
            .SetTag(ActivityTags.StepId, stepId.ToString())
            .SetTag(ActivityTags.ExecutionId, executionId.ToString())
            .SetTag(ActivityTags.ActivityStatus, status.ToString());
    }

    /// <summary>
    /// Sets activity execution tags with correlation ID
    /// </summary>
    public static Activity? SetActivityExecutionTags(this Activity? activity,
        Guid orchestratedFlowEntityId,
        Guid stepId,
        Guid executionId,
        Guid correlationId)
    {
        activity?
            .SetTag(ActivityTags.OrchestratedFlowEntityId, orchestratedFlowEntityId.ToString())
            .SetTag(ActivityTags.StepId, stepId.ToString())
            .SetTag(ActivityTags.ExecutionId, executionId.ToString());

        if (correlationId != Guid.Empty)
        {
            activity?.SetTag(ActivityTags.CorrelationId, correlationId.ToString());
        }

        return activity;
    }

    /// <summary>
    /// Sets cache operation tags on an activity
    /// </summary>
    public static Activity? SetCacheTags(this Activity? activity, string operation, string mapName, string key, bool? hit = null)
    {
        activity?
            .SetTag(ActivityTags.CacheOperation, operation)
            .SetTag(ActivityTags.CacheMapName, mapName)
            .SetTag(ActivityTags.CacheKey, key);

        if (hit.HasValue)
        {
            activity?.SetTag(ActivityTags.CacheHit, hit.Value);
        }

        return activity;
    }

    /// <summary>
    /// Sets validation tags on an activity
    /// </summary>
    public static Activity? SetValidationTags(this Activity? activity, 
        bool enabled, 
        bool isValid, 
        int errorCount, 
        string schemaType,
        string? errorPath = null)
    {
        activity?
            .SetTag(ActivityTags.ValidationEnabled, enabled)
            .SetTag(ActivityTags.ValidationIsValid, isValid)
            .SetTag(ActivityTags.ValidationErrorCount, errorCount)
            .SetTag(ActivityTags.ValidationSchemaType, schemaType);

        if (!string.IsNullOrEmpty(errorPath))
        {
            activity?.SetTag(ActivityTags.ValidationErrorPath, errorPath);
        }

        return activity;
    }

    /// <summary>
    /// Sets health check tags on an activity
    /// </summary>
    public static Activity? SetHealthCheckTags(this Activity? activity, string name, HealthStatus status, TimeSpan duration)
    {
        return activity?
            .SetTag(ActivityTags.HealthCheckName, name)
            .SetTag(ActivityTags.HealthCheckStatus, status.ToString())
            .SetTag(ActivityTags.HealthCheckDuration, duration.TotalMilliseconds);
    }

    /// <summary>
    /// Sets error tags on an activity 
    /// </summary>
    public static Activity? SetErrorTags(this Activity? activity, Exception exception)
    {
        return activity?
            .SetStatus(ActivityStatusCode.Error, exception.Message)
            .SetTag(ActivityTags.ErrorType, exception.GetType().Name)
            .SetTag(ActivityTags.ErrorMessage, exception.Message)
            .SetTag(ActivityTags.ErrorStackTrace, exception.StackTrace ?? string.Empty);
    }

    /// <summary>
    /// Sets message bus tags on an activity
    /// </summary>
    public static Activity? SetMessageTags(this Activity? activity, string messageType, string consumerType, Guid correlationId = default)
    {
        activity?
            .SetTag(ActivityTags.MessageType, messageType)
            .SetTag(ActivityTags.ConsumerType, consumerType);

        if (correlationId != Guid.Empty)
        {
            activity?.SetTag(ActivityTags.CorrelationId, correlationId.ToString());
        }

        return activity;
    }

    /// <summary>
    /// Sets entity processing tags on an activity
    /// </summary>
    public static Activity? SetEntityTags(this Activity? activity, int entitiesCount, string? entityType = null)
    {
        activity?.SetTag(ActivityTags.EntitiesCount, entitiesCount);

        if (!string.IsNullOrEmpty(entityType))
        {
            activity?.SetTag(ActivityTags.EntityType, entityType);
        }

        return activity;
    }

    /// <summary>
    /// Starts an activity with automatic correlation ID inclusion from context.
    /// </summary>
    public static Activity? StartActivityWithCorrelation(this ActivitySource source, string name)
    {
        var activity = source.StartActivity(name);
        return activity?.SetCorrelationContext();
    }

    /// <summary>
    /// Sets correlation ID from current context on an activity.
    /// </summary>
    public static Activity? SetCorrelationContext(this Activity? activity)
    {
        if (activity == null) return null;

        var correlationId = GetCurrentCorrelationId();
        if (correlationId != Guid.Empty)
        {
            activity.SetTag(ActivityTags.CorrelationId, correlationId.ToString());
            activity.SetBaggage("correlation.id", correlationId.ToString());
        }

        return activity;
    }

    /// <summary>
    /// Sets activity execution tags with automatic correlation ID from context.
    /// </summary>
    public static Activity? SetActivityExecutionTagsWithCorrelation(this Activity? activity,
        Guid orchestratedFlowEntityId,
        Guid stepId,
        Guid executionId)
    {
        return activity?
            .SetTag(ActivityTags.OrchestratedFlowEntityId, orchestratedFlowEntityId.ToString())
            .SetTag(ActivityTags.StepId, stepId.ToString())
            .SetTag(ActivityTags.ExecutionId, executionId.ToString())
            .SetCorrelationContext();
    }

    /// <summary>
    /// Sets message bus tags with automatic correlation ID from context.
    /// </summary>
    public static Activity? SetMessageTagsWithCorrelation(this Activity? activity, string messageType, string consumerType)
    {
        return activity?
            .SetTag(ActivityTags.MessageType, messageType)
            .SetTag(ActivityTags.ConsumerType, consumerType)
            .SetCorrelationContext();
    }

    private static Guid GetCurrentCorrelationId()
    {
        // Fallback to Activity baggage
        var activity = Activity.Current;
        if (activity?.GetBaggageItem("correlation.id") is string baggageValue &&
            Guid.TryParse(baggageValue, out var correlationId))
        {
            return correlationId;
        }

        return Guid.Empty;
    }
}
