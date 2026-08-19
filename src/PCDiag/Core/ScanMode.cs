namespace PCDiag.Core;

/// <summary>
/// Depth of a diagnostic scan.
/// </summary>
public enum ScanMode
{
    /// <summary>Fast overview. Skips checks that require elevation or are slow.</summary>
    Quick,

    /// <summary>Standard depth. Runs all checks that can run in the current context.</summary>
    Standard,

    /// <summary>Thorough analysis. Includes every registered check.</summary>
    Deep
}