namespace TourneeVeto.Ui.Data;

public enum VisitStorageError
{
    QuotaExceeded,
    Unavailable,
    UpgradeBlocked,
    UnsupportedVersion,
    InvalidData,
    Unknown
}

public sealed class VisitStorageException(
    VisitStorageError code, string message, string? detail = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public VisitStorageError Code { get; } = code;
    public string? Detail { get; } = detail;
}
