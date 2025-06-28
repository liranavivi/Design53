 namespace Shared.Extensions;

/// <summary>
/// Common activity tags used throughout the application
/// </summary>
public static class ActivityTags
{
    public const string EntityType = "entity.type";
    public const string EntityId = "entity.id";
    public const string EntityCompositeKey = "entity.compositeKey";

    // Processor-specific tags
    public const string ProcessorId = "processor.id";
    public const string ProcessorName = "processor.name";
    public const string ProcessorVersion = "processor.version";

    // Activity execution tags
    public const string OrchestratedFlowEntityId = "orchestrated_flow.id";
    public const string StepId = "step.id";
    public const string ExecutionId = "execution.id";
    public const string ActivityStatus = "activity.status";
    public const string ActivityDuration = "activity.duration_ms";
    public const string EntitiesCount = "entities.count";

    public const string CacheOperation = "cache.operation";
    public const string CacheKey = "cache.key";
    public const string CacheMapName = "cache.map_name";
    public const string CacheHit = "cache.hit";

    // Message bus tags
    public const string MessageType = "message.type";
    public const string CorrelationId = "correlation.id";
    public const string ConsumerType = "consumer.type";

    // Validation tags
    public const string ValidationEnabled = "validation.enabled";
    public const string ValidationIsValid = "validation.is_valid";
    public const string ValidationErrorCount = "validation.error_count";
    public const string ValidationErrorPath = "validation.error_path";
    public const string ValidationSchemaType = "validation.schema_type";

    // Health check tags
    public const string HealthCheckName = "health_check.name";
    public const string HealthCheckStatus = "health_check.status";
    public const string HealthCheckDuration = "health_check.duration_ms";

    // Error tags
    public const string ErrorType = "error.type";
    public const string ErrorMessage = "error.message";
    public const string ErrorStackTrace = "error.stack_trace";
}
