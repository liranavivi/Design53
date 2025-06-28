using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    /// <summary>
    /// Status of activity execution
    /// </summary>
    public enum ActivityExecutionStatus
    {
        /// <summary>
        /// Activity is currently being processed
        /// </summary>
        Processing,

        /// <summary>
        /// Activity completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Activity failed with an error
        /// </summary>
        Failed,

        /// <summary>
        /// Activity was cancelled
        /// </summary>
        Cancelled
    }
}
