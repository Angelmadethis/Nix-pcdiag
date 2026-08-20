namespace PCDiag.Core;

/// <summary>
/// Optional capability for diagnostic checks that can propose remediation. Checks that
/// do not implement this interface never offer fixes. A check that does implement it
/// must only ever return fixes for findings, and those fixes are applied only after
/// explicit user confirmation - never automatically.
/// </summary>
public interface IFixableCheck : IDiagnosticCheck
{
    /// <summary>
    /// The fixes applicable to a given result, in display order. Returns an empty list
    /// when the result is healthy or has no remediable issue.
    /// </summary>
    IReadOnlyList<PCDiag.Fixes.DiagnosticFix> GetFixes(PCDiag.Core.DiagnosticResult result);
}