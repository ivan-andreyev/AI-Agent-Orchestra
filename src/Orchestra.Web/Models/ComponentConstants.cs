namespace Orchestra.Web.Models;

/// <summary>
/// Configuration constants for UI components to eliminate magic numbers
/// and provide centralized configuration management.
/// </summary>
public static class ComponentConstants
{
    /// <summary>
    /// History Display Constants
    /// </summary>
    public static class History
    {
        /// <summary>
        /// Threshold for truncating history entry content display.
        /// Entries longer than this will show a "Show more" button.
        /// </summary>
        public const int TruncateThreshold = 300;

        /// <summary>
        /// Maximum number of history entries to load per agent.
        /// </summary>
        public const int EntryLimit = 50;
    }

    /// <summary>
    /// Task Display Constants
    /// </summary>
    public static class Tasks
    {
        /// <summary>
        /// Maximum number of tasks to display in the task list view.
        /// Additional tasks will be shown as a count summary.
        /// </summary>
        public const int DisplayLimit = 10;

        /// <summary>
        /// Maximum number of log entries to keep in the task log.
        /// Older entries will be removed when this limit is exceeded.
        /// </summary>
        public const int LogEntryLimit = 50;

        /// <summary>
        /// Maximum length of task command text to display in logs.
        /// Longer commands will be truncated with ellipsis.
        /// </summary>
        public const int CommandLogLength = 50;
    }
}