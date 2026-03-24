namespace backend.Common;

/// <summary>
/// Activity type constants for audit logging.
/// Using PascalCase format to match existing database values.
/// </summary>
public static class ActivityTypes
{
    // Log activities
    public const string LogUpload = "LogUpload";
    public const string LogDelete = "LogDelete";
    public const string LogAnalyze = "LogAnalyze";
    public const string LogBulkDelete = "LogBulkDelete";
    public const string LogBulkAnalyze = "LogBulkAnalyze";

    // Report activities
    public const string ReportGenerated = "ReportGenerated";
    public const string ReportDownload = "ReportDownload";
    public const string ReportDelete = "ReportDelete";
    public const string ReportExport = "ReportExport";

    // Tag activities
    public const string TagCreate = "TagCreate";
    public const string TagUpdate = "TagUpdate";
    public const string TagDelete = "TagDelete";
    public const string TagAssign = "TagAssign";

    // Bookmark activities
    public const string BookmarkCreate = "BookmarkCreate";
    public const string BookmarkUpdate = "BookmarkUpdate";
    public const string BookmarkDelete = "BookmarkDelete";

    // Share link activities
    public const string ShareLinkCreated = "ShareLinkCreated";
    public const string ShareLinkDeleted = "ShareLinkDeleted";
    public const string ShareLinkAccessed = "ShareLinkAccessed";

    // Worker activities
    public const string WorkerRegistered = "WorkerRegistered";
    public const string WorkerDeleted = "WorkerDeleted";
    public const string WorkerConfigUpdated = "WorkerConfigUpdated";

    // Settings activities
    public const string SettingsUpdated = "SettingsUpdated";
    public const string LayoutUpdated = "LayoutUpdated";

    // Subscription activities
    public const string SubscriptionCreated = "SubscriptionCreated";
    public const string SubscriptionChanged = "SubscriptionChanged";
    public const string PaymentCompleted = "PaymentCompleted";

    // Auth activities
    public const string Login = "Login";
    public const string Logout = "Logout";
}

/// <summary>
/// Entity type constants for audit logging
/// </summary>
public static class EntityTypes
{
    public const string Log = "Log";
    public const string Report = "Report";
    public const string Tag = "Tag";
    public const string Bookmark = "Bookmark";
    public const string ShareableLink = "ShareableLink";
    public const string WorkerAgent = "WorkerAgent";
    public const string WorkerConfig = "WorkerConfig";
    public const string NotificationPreference = "NotificationPreference";
    public const string LayoutPreference = "LayoutPreference";
    public const string Subscription = "Subscription";
    public const string Payment = "Payment";
}

/// <summary>
/// Error message constants
/// </summary>
public static class ErrorMessages
{
    // Authentication/Authorization
    public const string Unauthorized = "Unable to determine user identity";
    public const string AccessDenied = "Access denied";

    // Generic
    public const string NotFound = "Resource not found";
    public const string InvalidRequest = "Invalid request";

    // File operations
    public const string FileRequired = "File is required.";
    public const string FileTooLarge = "File size exceeds 10 MB.";
    public const string FileExtensionMissing = "File extension missing.";
    public const string UnsupportedFileType = "Unsupported file type '{0}'.";
    public const string NoExtractableText = "No extractable text found.";
    public const string FileMissing = "File missing";
    public const string ReportContentMissing = "Report content missing";
    public const string ReportFileNotFound = "Report file not found on disk";

    // Logs
    public const string LogNotFound = "Log not found";
    public const string NoLogIdsProvided = "No log IDs provided";

    // Reports
    public const string ReportNotFound = "Report not found";
    public const string NoReportIdsProvided = "No report IDs provided";

    // Tags
    public const string TagNameRequired = "Tag name is required";
    public const string TagAlreadyExists = "Tag with this name already exists";

    // Bookmarks
    public const string AlreadyBookmarked = "Log is already bookmarked";

    // Share links
    public const string LinkNotFound = "Link not found";
    public const string LinkInactive = "Link is no longer active";
    public const string LinkExpired = "Link has expired";
    public const string MaxAccessLimitReached = "Maximum access limit reached";
    public const string InvalidPassword = "Invalid password";
    public const string PasswordRequired = "Password required";

    // Layout
    public const string PageIdRequired = "PageId is required";

    // Workers
    public const string WorkerNotFound = "Worker not found";
    public const string WorkerAlreadyExists = "A worker with this name already exists";
    public const string WorkerAlreadyRevoked = "Worker is already revoked";
    public const string WorkerAlreadyActive = "Worker is already active";
    public const string CannotRotateKeyForRevoked = "Cannot rotate key for revoked worker";
    public const string WorkerLimitReached = "Maximum worker limit ({0}) reached. Upgrade your plan or revoke unused workers.";
    public const string WorkerLimitReachedReactivate = "Maximum worker limit ({0}) reached. Revoke other workers first.";

    // Worker Config
    public const string WorkerConfigNotFound = "Worker configuration not found. Initialize first.";
    public const string WorkerConfigAlreadyExists = "Worker configuration already exists. Use rotate-psk to generate a new key.";
    public const string WorkerConfigDisabled = "Your worker configuration is disabled";
    public const string InitializeConfigFirst = "Please initialize your worker configuration first at /api/worker-config/initialize";

    // Similarity Search
    public const string QueryRequired = "Query text is required";
    public const string LogContentRequired = "Log content is required";
    public const string SystemIdRequired = "SystemId (WorkerId) is required";
    public const string FailedToDeleteUserData = "Failed to delete user data";
    public const string FailedToDeleteReportData = "Failed to delete report data";

    // Linux System
    public const string WorkerDidNotRespond = "Worker did not respond in time";
    public const string CommandBlocked = "Command blocked for security reasons";
    public const string CommandTimedOut = "Command timed out";
    public const string InvalidAction = "Invalid action. Valid: {0}";

    // Subscription
    public const string CheckoutFailed = "An error occurred during checkout. Please try again.";
    public const string PaymentVerificationFailed = "Payment verification failed. Please contact support.";
    public const string FailedToCancelSubscription = "Failed to cancel subscription. Please try again or contact support.";
    public const string FailedToCreateOrder = "Failed to create order. Please try again.";
    public const string InvalidPaymentSignature = "Invalid payment signature";
}

/// <summary>
/// Supported file extensions for log uploads
/// </summary>
public static class FileExtensions
{
    public static readonly HashSet<string> Text = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".json", ".xml", ".csv"
    };

    public static readonly HashSet<string> Image = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".tiff"
    };

    public static readonly HashSet<string> Pdf = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    public static bool IsSupported(string extension)
    {
        return Text.Contains(extension) || Image.Contains(extension) || Pdf.Contains(extension);
    }

    public static bool IsText(string extension) => Text.Contains(extension);
    public static bool IsImage(string extension) => Image.Contains(extension);
    public static bool IsPdf(string extension) => Pdf.Contains(extension);
}

/// <summary>
/// Default values
/// </summary>
public static class Defaults
{
    public const string TagColor = "#6c757d";
    public const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    public const int MaxLogsPerList = 50;
    public const int MaxActivitiesPerPage = 100;
    public const int DefaultPageSize = 20;
    public const int MaxRecentActivities = 50;
    public const string EmptyLayoutJson = "{}";
    public const int DefaultMaxWorkers = 3;
    public const string DefaultWorkerConfigName = "Default Configuration";
    public const int DefaultWorkerTimeoutSeconds = 30;
    public const int DefaultCommandTimeoutSeconds = 60;
}

/// <summary>
/// Worker status enum (mirroring WorkerAgentStatus)
/// </summary>
public enum WorkerStatus
{
    Active,
    Revoked,
    Inactive
}

/// <summary>
/// Valid service actions for Linux system management
/// </summary>
public static class ServiceActions
{
    public const string Start = "start";
    public const string Stop = "stop";
    public const string Restart = "restart";
    public const string Enable = "enable";
    public const string Disable = "disable";
    public const string Reload = "reload";
    public const string Status = "status";

    public static readonly string[] ValidActions = { Start, Stop, Restart, Enable, Disable, Reload, Status };

    public static bool IsValid(string action) => ValidActions.Contains(action.ToLower());
}

/// <summary>
/// Blocked command patterns for security
/// </summary>
public static class SecurityPatterns
{
    public static readonly string[] BlockedCommands =
    {
        "rm -rf /",
        "mkfs",
        "dd if=",
        ":(){:|:&};:",
        "chmod -R 777 /",
        "passwd",
        "useradd",
        "userdel"
    };

    public static bool IsBlocked(string command) =>
        BlockedCommands.Any(p => command.Contains(p, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Page identifiers for layout preferences
/// </summary>
public static class PageIds
{
    public const string LogCenter = "log-center";
    public const string Analysis = "analysis";
    public const string SystemMonitor = "system-monitor";
    public const string WorkerHub = "worker-hub";
    public const string Dashboard = "dashboard";
    public const string Settings = "settings";
}
