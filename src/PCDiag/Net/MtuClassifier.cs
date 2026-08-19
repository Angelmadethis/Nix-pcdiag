namespace PCDiag.Net;

/// <summary>Verdict of the interface/path MTU comparison.</summary>
public enum MtuVerdict
{
    /// <summary>The measured path MTU is at least as large as the interface MTU.</summary>
    Healthy,

    /// <summary>The path MTU may be below the interface MTU but the measurement is not fully confirmed.</summary>
    PotentialIssue,

    /// <summary>The path MTU is below the interface MTU and the boundary was confirmed.</summary>
    ConfirmedMismatch,

    /// <summary>No path MTU could be measured (target dead or ICMP echo blocked).</summary>
    Unmeasurable,

    /// <summary>The path MTU was measured but the interface MTU is unknown.</summary>
    InterfaceMtuUnknown
}

/// <summary>
/// Pure classification of interface MTU versus measured path MTU. Testable without
/// network access. The internet path is preferred over the gateway path when it was
/// measured across the full interface range; otherwise the gateway path is used.
/// A measurement whose search limit is below the interface MTU cannot confirm a
/// mismatch, because sizes beyond its limit were never tested.
/// </summary>
public static class MtuClassifier
{
    public static MtuVerdict Classify(int? interfaceMtu, PathMtuResult? gatewayPath, PathMtuResult? internetPath)
    {
        var path = SelectRepresentativePath(interfaceMtu, gatewayPath, internetPath);
        if (path is null || path.DetectedPathMtu is not int detected)
            return MtuVerdict.Unmeasurable;

        if (interfaceMtu is not int mtu)
            return MtuVerdict.InterfaceMtuUnknown;

        var interfacePayload = mtu - MtuOptions.IcmpIpv4Overhead;
        if (path.PayloadLimitTested < interfacePayload)
        {
            // The search could not test sizes up to the interface MTU. If it reached
            // its own limit, sizes beyond it are unverified, so no mismatch is confirmed.
            return path.MaxPayloadSucceeded >= path.PayloadLimitTested
                ? MtuVerdict.Healthy
                : MtuVerdict.PotentialIssue;
        }

        if (detected >= mtu)
            return MtuVerdict.Healthy;

        return path.BoundaryConfirmed ? MtuVerdict.ConfirmedMismatch : MtuVerdict.PotentialIssue;
    }

    /// <summary>
    /// Pick the path whose measurement best represents the interface MTU range:
    /// the internet path when it spans the range (the common &lt;=1500 case), otherwise
    /// the gateway path, which is probed across the full range.
    /// </summary>
    public static PathMtuResult? SelectRepresentativePath(
        int? interfaceMtu,
        PathMtuResult? gatewayPath,
        PathMtuResult? internetPath)
    {
        if (interfaceMtu is int mtu)
        {
            var interfacePayload = mtu - MtuOptions.IcmpIpv4Overhead;
            if (internetPath is not null && internetPath.DetectedPathMtu is not null && internetPath.PayloadLimitTested >= interfacePayload)
                return internetPath;
            if (gatewayPath is not null && gatewayPath.DetectedPathMtu is not null)
                return gatewayPath;
            return internetPath ?? gatewayPath;
        }

        if (internetPath is not null && internetPath.DetectedPathMtu is not null)
            return internetPath;
        return gatewayPath;
    }
}