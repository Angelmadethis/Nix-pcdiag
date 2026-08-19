namespace PCDiag.Core;

/// <summary>
/// An exception thrown by a check to signal a specific structured failure.
/// </summary>
public sealed class DiagnosticCheckException : Exception
{
    /// <summary>A machine-readable error code.</summary>
    public string Code { get; }

    public DiagnosticCheckException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}