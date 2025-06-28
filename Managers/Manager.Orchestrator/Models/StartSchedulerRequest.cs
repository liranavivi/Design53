using System.ComponentModel.DataAnnotations;

namespace Manager.Orchestrator.Models;

/// <summary>
/// Request model for starting a scheduler for an orchestrated flow.
/// </summary>
public class StartSchedulerRequest
{
    /// <summary>
    /// Gets or sets the cron expression for scheduled execution.
    /// Example: "0 0 12 * * ?" for daily at noon
    /// </summary>
    [Required(ErrorMessage = "Cron expression is required")]
    [StringLength(100, ErrorMessage = "Cron expression cannot exceed 100 characters")]
    public string CronExpression { get; set; } = string.Empty;
}
