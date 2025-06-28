using System.ComponentModel;

namespace Shared.Entities.Enums;

/// <summary>
/// Defines the entry conditions that determine when a step should be executed.
/// These conditions control the workflow execution logic and step transitions.
/// </summary>
public enum StepEntryCondition
{
    /// <summary>
    /// Execute only if the previous step completed successfully.
    /// This is the default behavior for most workflow steps.
    /// </summary>
    [Description("Execute only if previous step succeeded")]
    PreviousSuccess = 0,

    /// <summary>
    /// Execute only if the previous step failed or encountered an error.
    /// Useful for error handling, cleanup, or alternative processing paths.
    /// </summary>
    [Description("Execute only if previous step failed")]
    PreviousFailure = 1,

    /// <summary>
    /// Always execute this step regardless of previous step results.
    /// Useful for initialization, logging, or mandatory processing steps.
    /// </summary>
    [Description("Always execute regardless of previous results")]
    Always = 2,

    /// <summary>
    /// Never execute this step - it is disabled.
    /// Useful for temporarily disabling steps without removing them.
    /// </summary>
    [Description("Never execute - step is disabled")]
    Never = 3,

}
