using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Shared.Correlation;

namespace Shared.Extensions;

/// <summary>
/// Extension methods for metrics that automatically include correlation IDs as tags.
/// Provides consistent metric tagging with correlation context.
/// </summary>
public static class MetricsExtensions
{
    /// <summary>
    /// Records a counter value with automatic correlation ID tag inclusion.
    /// </summary>
    public static void AddWithCorrelation<T>(this Counter<T> counter, T delta, params (string Key, object? Value)[] additionalTags)
        where T : struct
    {
        var tags = CreateCorrelationTagList(additionalTags);
        counter.Add(delta, tags);
    }

    /// <summary>
    /// Records a histogram value with automatic correlation ID tag inclusion.
    /// </summary>
    public static void RecordWithCorrelation<T>(this Histogram<T> histogram, T value, params (string Key, object? Value)[] additionalTags)
        where T : struct
    {
        var tags = CreateCorrelationTagList(additionalTags);
        histogram.Record(value, tags);
    }

    /// <summary>
    /// Records an up-down counter value with automatic correlation ID tag inclusion.
    /// </summary>
    public static void AddWithCorrelation<T>(this UpDownCounter<T> counter, T delta, params (string Key, object? Value)[] additionalTags)
        where T : struct
    {
        var tags = CreateCorrelationTagList(additionalTags);
        counter.Add(delta, tags);
    }

    /// <summary>
    /// Creates a TagList with correlation ID and additional tags.
    /// </summary>
    public static TagList CreateCorrelationTagList(params (string Key, object? Value)[] additionalTags)
    {
        var tags = new TagList();
        
        // Add correlation ID if available
        var correlationId = GetCurrentCorrelationId();
        if (correlationId != Guid.Empty)
        {
            tags.Add("correlation.id", correlationId.ToString());
        }

        // Add additional tags
        foreach (var (key, value) in additionalTags)
        {
            if (value != null)
            {
                tags.Add(key, value);
            }
        }

        return tags;
    }

    /// <summary>
    /// Creates a TagList with correlation ID and common processor tags.
    /// </summary>
    public static TagList CreateProcessorTagList(
        Guid processorId,
        Guid? orchestratedFlowId = null,
        Guid? stepId = null,
        Guid? executionId = null,
        params (string Key, object? Value)[] additionalTags)
    {
        var baseTags = new List<(string Key, object? Value)>
        {
            ("processor.id", processorId.ToString())
        };

        if (orchestratedFlowId.HasValue && orchestratedFlowId != Guid.Empty)
            baseTags.Add(("orchestrated_flow.id", orchestratedFlowId.ToString()));

        if (stepId.HasValue && stepId != Guid.Empty)
            baseTags.Add(("step.id", stepId.ToString()));

        if (executionId.HasValue && executionId != Guid.Empty)
            baseTags.Add(("execution.id", executionId.ToString()));

        baseTags.AddRange(additionalTags);

        return CreateCorrelationTagList(baseTags.ToArray());
    }

    /// <summary>
    /// Creates a TagList with correlation ID and common HTTP tags.
    /// </summary>
    public static TagList CreateHttpTagList(
        string method,
        string? endpoint = null,
        int? statusCode = null,
        params (string Key, object? Value)[] additionalTags)
    {
        var baseTags = new List<(string Key, object? Value)>
        {
            ("http.method", method)
        };

        if (!string.IsNullOrEmpty(endpoint))
            baseTags.Add(("http.endpoint", endpoint));

        if (statusCode.HasValue)
            baseTags.Add(("http.status_code", statusCode.Value));

        baseTags.AddRange(additionalTags);

        return CreateCorrelationTagList(baseTags.ToArray());
    }

    /// <summary>
    /// Creates a TagList with correlation ID and common cache operation tags.
    /// </summary>
    public static TagList CreateCacheTagList(
        string operation,
        string? mapName = null,
        bool? hit = null,
        params (string Key, object? Value)[] additionalTags)
    {
        var baseTags = new List<(string Key, object? Value)>
        {
            ("cache.operation", operation)
        };

        if (!string.IsNullOrEmpty(mapName))
            baseTags.Add(("cache.map_name", mapName));

        if (hit.HasValue)
            baseTags.Add(("cache.hit", hit.Value));

        baseTags.AddRange(additionalTags);

        return CreateCorrelationTagList(baseTags.ToArray());
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
