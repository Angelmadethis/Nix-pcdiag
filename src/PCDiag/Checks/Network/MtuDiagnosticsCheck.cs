using System.Net;
using PCDiag.Core;
using PCDiag.Fixes;

namespace PCDiag.Checks.Network;

/// <summary>
/// Measures the interface MTU and the largest Don't-Fragment packet that traverses
/// the path, and compares them. Different network technologies legitimately use
/// different MTUs (PPPoE at 1492, jumbo LANs at 9000), so a non-1500 value is never
/// flagged on its own. Findings use the wording "Potential MTU/path issue" unless the
/// measurement is strong and confirmed. Read-only: no settings are modified.
/// </summary>
public sealed class MtuDiagnosticsCheck : DiagnosticCheck, IFixableCheck
{
    private readonly PCDiag.Net.MtuOptions _options;
    private readonly PCDiag.Net.IPingProbe _probe;
    private readonly PCDiag.Net.IInterfaceMtuSource _mtuSource;
    private readonly IReadOnlyList<string> _targetOverrides;

    public override string CheckId => "NET-MTU-001";
    public override string Name => "Interface & Path MTU";
    public override DiagnosticCategory Category => DiagnosticCategory.Network;
    public override string Description =>
        "Measures the interface MTU and the largest packet that passes the path without fragmentation.";

    public MtuDiagnosticsCheck(
        PCDiag.Net.MtuOptions? options = null,
        PCDiag.Net.IPingProbe? probe = null,
        PCDiag.Net.IInterfaceMtuSource? mtuSource = null,
        IReadOnlyList<string>? targetOverrides = null)
    {
        _options = options ?? PCDiag.Net.MtuOptions.Default;
        _probe = probe ?? new PCDiag.Net.SystemPingProbe();
        _mtuSource = mtuSource ?? new PCDiag.Net.WmiInterfaceMtuSource();
        _targetOverrides = targetOverrides ?? Array.Empty<string>();
    }

    private static readonly IReadOnlyList<string> CheckLimitations = new[]
    {
        "Measurements are point-in-time; a single run may not reflect sustained conditions.",
        "Only the ICMP echo path to the tested targets is measured; path MTU can differ per destination.",
        "Some networks block ICMP echo or do not return fragmentation-needed errors, which can make path MTU unmeasurable or hide PMTU black holes.",
        "IPv6 path MTU and jumbo frames beyond the tested range are not probed."
    };

    protected override async Task<DiagnosticResult> RunAsync(DiagnosticContext context, CancellationToken cancellationToken)
    {
        var active = context.Inventory?.Network.ActiveConnection;
        if (active is null)
            return NoActiveConnectionResult();

        var interfaceMtu = _mtuSource.GetMtu(active.IpAddresses, active.Name);
        var gateway = PCDiag.Net.TargetResolver.FirstGateway(active.GatewayAddresses);
        if (gateway is null)
            return NoGatewayResult(interfaceMtu);

        var configuredTargets = _targetOverrides.Count > 0 ? _targetOverrides : _options.DefaultInternetTargets;
        var internetTargets = await PCDiag.Net.TargetResolver
            .ResolveAsync(configuredTargets, _options.MaxMtuTargets, cancellationToken)
            .ConfigureAwait(false);

        var gatewayPayloadLimit = Math.Max(
            _options.SearchMinPayload,
            (interfaceMtu ?? _options.DefaultMaxPayload) - PCDiag.Net.MtuOptions.IcmpIpv4Overhead);
        var internetPayloadLimit = Math.Max(
            _options.SearchMinPayload,
            Math.Min(gatewayPayloadLimit, _options.DefaultMaxPayload));

        var gatewayPath = await MeasurePathAsync(gateway, gatewayPayloadLimit, cancellationToken).ConfigureAwait(false);

        PCDiag.Net.PathMtuResult? internetPath = null;
        if (gatewayPath.DetectedPathMtu is not null && internetTargets.Count > 0)
        {
            internetPath = await MeasurePathAsync(internetTargets[0], internetPayloadLimit, cancellationToken).ConfigureAwait(false);
        }

        var representativePath = PCDiag.Net.MtuClassifier.SelectRepresentativePath(interfaceMtu, gatewayPath, internetPath);

        var verdict = PCDiag.Net.MtuClassifier.Classify(interfaceMtu, gatewayPath, internetPath);
        var blackHole = representativePath?.SawBlackHole == true;
        var effectiveVerdict = blackHole && verdict != PCDiag.Net.MtuVerdict.ConfirmedMismatch
            ? PCDiag.Net.MtuVerdict.PotentialIssue
            : verdict;

        var (severity, status) = MapVerdict(effectiveVerdict, blackHole);

        var internetIp = internetTargets.Count > 0 ? internetTargets[0] : null;
        var evidence = BuildEvidence(active, gateway, interfaceMtu, gatewayPath, internetPath, internetIp, representativePath, configuredTargets);
        var summary = BuildSummary(effectiveVerdict, blackHole, interfaceMtu, representativePath);
        var detail = BuildDetail(effectiveVerdict, blackHole, interfaceMtu, representativePath);

        return BuildResult(
            severity,
            status,
            summary,
            detail: detail,
            evidence: evidence,
            recommendations: BuildRecommendations(effectiveVerdict, blackHole, interfaceMtu, representativePath),
            possibleCauses: PossibleCauses(effectiveVerdict),
            limitations: CheckLimitations,
            confidence: ComputeConfidence(effectiveVerdict, blackHole));
    }

    private Task<PCDiag.Net.PathMtuResult> MeasurePathAsync(IPAddress target, int maxPayload, CancellationToken cancellationToken)
        => PCDiag.Net.PathMtuSearcher.MeasureAsync(
            _options.SearchMinPayload,
            maxPayload,
            _options.ConfirmationProbes,
            (payload, token) => _probe.ProbeAsync(target, payload, dontFragment: true, _options.ProbeTimeout, token),
            cancellationToken);

    private static (DiagnosticSeverity Severity, DiagnosticStatus Status) MapVerdict(PCDiag.Net.MtuVerdict verdict, bool blackHole)
    {
        return verdict switch
        {
            PCDiag.Net.MtuVerdict.ConfirmedMismatch => (DiagnosticSeverity.Warning, DiagnosticStatus.Finding),
            PCDiag.Net.MtuVerdict.PotentialIssue when blackHole => (DiagnosticSeverity.Warning, DiagnosticStatus.Finding),
            PCDiag.Net.MtuVerdict.PotentialIssue => (DiagnosticSeverity.Suspicious, DiagnosticStatus.Finding),
            PCDiag.Net.MtuVerdict.Healthy => (DiagnosticSeverity.Healthy, DiagnosticStatus.Passed),
            _ => (DiagnosticSeverity.Info, DiagnosticStatus.Unavailable)
        };
    }

    private DiagnosticResult NoActiveConnectionResult()
    {
        return BuildResult(
            DiagnosticSeverity.Info,
            DiagnosticStatus.Unavailable,
            "No active network connection was found, so interface and path MTU could not be measured.",
            detail:
                "The check relies on the active network adapter (its IP addresses for the interface MTU and its " +
                "default gateway for path measurement). None was present in the inventory.",
            evidence: new[]
            {
                new DiagnosticEvidence
                {
                    Description = "Active Connection",
                    Value = "None",
                    Source = "SystemInventory.Network"
                }
            },
            recommendations: new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "Verify that a network adapter is connected (cable or Wi-Fi) and has a default gateway configured.",
                    RequiresAdmin = false,
                    Priority = 1
                }
            },
            confidence: 0.9);
    }

    private DiagnosticResult NoGatewayResult(int? interfaceMtu)
    {
        return BuildResult(
            DiagnosticSeverity.Info,
            DiagnosticStatus.Unavailable,
            "No default gateway is configured, so path MTU could not be measured.",
            detail:
                "Path MTU measurement sends Don't-Fragment probes to the default gateway and, when it responds, to an " +
                "internet endpoint. Without a gateway the path cannot be probed.",
            evidence: new[]
            {
                new DiagnosticEvidence
                {
                    Description = "Interface MTU",
                    Value = interfaceMtu?.ToString() ?? "Unknown",
                    Source = "Win32_NetworkAdapterConfiguration.MTU"
                },
                new DiagnosticEvidence
                {
                    Description = "Default Gateway",
                    Value = "None configured",
                    Source = "Network adapter default gateway"
                }
            },
            recommendations: new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "Verify that the active network adapter obtains an IP address automatically (DHCP) or has a valid gateway configured.",
                    RequiresAdmin = true,
                    Priority = 1
                }
            },
            confidence: 0.9);
    }

    private IReadOnlyList<DiagnosticEvidence> BuildEvidence(
        PCDiag.Inventory.NetworkAdapterInfo active,
        IPAddress gateway,
        int? interfaceMtu,
        PCDiag.Net.PathMtuResult? gatewayPath,
        PCDiag.Net.PathMtuResult? internetPath,
        IPAddress? internetIp,
        PCDiag.Net.PathMtuResult? representativePath,
        IReadOnlyList<string> configuredTargets)
    {
        var evidence = new List<DiagnosticEvidence>
        {
            new()
            {
                Description = "Active Adapter",
                Value = $"{active.Name} ({string.Join(", ", active.IpAddresses)})",
                Source = "NetworkInterface"
            },
            new()
            {
                Description = "Default Gateway",
                Value = gateway.ToString(),
                Source = "Network adapter default gateway"
            },
            new()
            {
                Description = "Interface MTU",
                Value = interfaceMtu?.ToString() ?? "Unknown",
                ExpectedValue = interfaceMtu is not null ? "varies by technology (e.g. 1500, 1492, 9000)" : null,
                Source = "Win32_NetworkAdapterConfiguration.MTU"
            }
        };

        if (gatewayPath is not null)
            evidence.Add(PathEvidence(gateway, gatewayPath, interfaceMtu));
        if (internetPath is not null && internetIp is not null)
            evidence.Add(PathEvidence(internetIp, internetPath, interfaceMtu));

        if (representativePath is not null)
        {
            var indicator = representativePath.SawFragmentationNeeded
                ? "Router reported oversized Don't-Fragment packets (cooperative PMTU discovery)"
                : representativePath.SawBlackHole
                    ? "Oversized Don't-Fragment packets silently dropped with no ICMP reply (possible PMTU black hole)"
                    : "None observed - DF-set packets up to the tested limit passed without fragmentation";
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Fragmentation Indicator",
                Value = indicator,
                Source = "ICMP probe trace"
            });
            evidence.Add(new DiagnosticEvidence
            {
                Description = "Boundary Confirmation",
                Value = representativePath.BoundaryConfirmed ? $"Confirmed ({_options.ConfirmationProbes} probes)" : "Not confirmed",
                Source = "ICMP probe trace"
            });
        }

        evidence.Add(new DiagnosticEvidence
        {
            Description = "Payload Search Range",
            Value = $"{_options.SearchMinPayload} - {representativePath?.PayloadLimitTested ?? _options.DefaultMaxPayload} bytes",
            Source = "pcdiag settings"
        });
        evidence.Add(new DiagnosticEvidence
        {
            Description = "Probe Timeout",
            Value = $"{_options.ProbeTimeout.TotalMilliseconds:F0} ms",
            Source = "pcdiag settings"
        });
        evidence.Add(new DiagnosticEvidence
        {
            Description = "Test Targets",
            Value = $"{gateway} (gateway)" +
                    (configuredTargets.Count > 0 ? $", {string.Join(", ", configuredTargets)}" : ""),
            Source = "pcdiag settings"
        });

        return evidence;
    }

    private static DiagnosticEvidence PathEvidence(IPAddress target, PCDiag.Net.PathMtuResult path, int? interfaceMtu)
    {
        var value = path.DetectedPathMtu is int detected
            ? $"{detected} bytes (largest DF payload {path.MaxPayloadSucceeded} of {path.PayloadLimitTested} tested)"
            : "Could not be measured (no ICMP reply)";

        return new DiagnosticEvidence
        {
            Description = $"Path MTU to {target}",
            Value = value,
            ExpectedValue = interfaceMtu is int mtu ? $">= {mtu} bytes" : "unknown (interface MTU not reported)",
            Source = "ICMP DF probe (binary search)"
        };
    }

    private static string BuildSummary(
        PCDiag.Net.MtuVerdict verdict,
        bool blackHole,
        int? interfaceMtu,
        PCDiag.Net.PathMtuResult? path)
    {
        return verdict switch
        {
            PCDiag.Net.MtuVerdict.ConfirmedMismatch when blackHole =>
                $"Potential MTU/path issue: the measured path MTU ({path?.DetectedPathMtu} bytes) is below the interface MTU ({interfaceMtu} bytes), and oversized Don't-Fragment packets are being silently dropped (possible PMTU black hole).",
            PCDiag.Net.MtuVerdict.ConfirmedMismatch =>
                $"Potential MTU/path issue: the measured path MTU ({path?.DetectedPathMtu} bytes) is below the interface MTU ({interfaceMtu} bytes).",
            PCDiag.Net.MtuVerdict.PotentialIssue when blackHole =>
                "Potential MTU/path issue: packets with the Don't-Fragment bit set are being silently dropped (possible PMTU black hole).",
            PCDiag.Net.MtuVerdict.PotentialIssue =>
                $"Potential MTU/path issue: the measured path MTU ({path?.DetectedPathMtu} bytes) is below the interface MTU ({interfaceMtu} bytes), but the measurement was not fully confirmed.",
            PCDiag.Net.MtuVerdict.InterfaceMtuUnknown =>
                $"The interface MTU could not be determined; the measured path MTU is {path?.DetectedPathMtu} bytes.",
            PCDiag.Net.MtuVerdict.Unmeasurable =>
                "Path MTU could not be measured (the target did not respond to ICMP echo).",
            _ =>
                $"Interface MTU ({interfaceMtu} bytes) and measured path MTU ({path?.DetectedPathMtu} bytes) are consistent; no MTU problem detected."
        };
    }

    private static string BuildDetail(
        PCDiag.Net.MtuVerdict verdict,
        bool blackHole,
        int? interfaceMtu,
        PCDiag.Net.PathMtuResult? path)
    {
        var parts = new List<string>
        {
            "If an IP packet is larger than the path MTU and has the Don't-Fragment bit set, an intermediate router " +
            "must either return an ICMP 'fragmentation needed' error (so the sender can lower its size) or silently " +
            "drop the packet - the latter is a PMTU black hole. This check sends ICMP echo requests with increasing " +
            "payload sizes and the Don't-Fragment bit set, records the largest payload that receives a reply, and " +
            "converts it to a path MTU (payload + 28 bytes of IPv4/ICMP overhead)."
        };

        if (verdict == PCDiag.Net.MtuVerdict.ConfirmedMismatch)
        {
            parts.Add(
                $"The measured path MTU ({path?.DetectedPathMtu} bytes) is below the interface MTU ({interfaceMtu} bytes) and the " +
                "boundary was confirmed by repeated probes. Packets between the two sizes would be fragmented (if the " +
                "Don't-Fragment bit is clear) or dropped (if it is set), which commonly happens across VPNs or PPPoE links.");
        }
        else if (verdict == PCDiag.Net.MtuVerdict.PotentialIssue && blackHole)
        {
            parts.Add(
                "Large Don't-Fragment packets timed out while smaller ones succeeded, meaning oversized packets are being " +
                "silently dropped instead of receiving an ICMP 'fragmentation needed' error. Applications relying on path " +
                "MTU discovery (for example VPNs) can hang or stall on such paths.");
        }
        else if (verdict == PCDiag.Net.MtuVerdict.PotentialIssue)
        {
            parts.Add(
                $"The measured path MTU ({path?.DetectedPathMtu} bytes) is below the interface MTU ({interfaceMtu} bytes), but the " +
                "boundary was not fully confirmed by the confirmation probes, so the finding is reported with reduced confidence.");
        }
        else if (verdict == PCDiag.Net.MtuVerdict.Healthy)
        {
            parts.Add(
                "The interface MTU and the measured path MTU are consistent, so packets at the interface size pass without " +
                "fragmentation on the tested path. A non-1500 MTU is normal for technologies such as PPPoE (1492) or jumbo " +
                "LANs (9000) and is not treated as a problem when the measurement agrees with it.");
        }
        else if (verdict == PCDiag.Net.MtuVerdict.InterfaceMtuUnknown)
        {
            parts.Add(
                "The path MTU was measured, but the interface MTU could not be read from the adapter configuration, so no " +
                "comparison is possible. This is reported as unavailable rather than healthy because the interface value is missing.");
        }
        else
        {
            parts.Add(
                "The target did not reply to any Don't-Fragment probe. This usually means the gateway is unreachable or ICMP " +
                "echo is blocked on the path, so the path MTU cannot be measured. This is reported as unavailable rather than healthy.");
        }

        return string.Join(" ", parts);
    }

    private static IReadOnlyList<DiagnosticRecommendation> BuildRecommendations(
        PCDiag.Net.MtuVerdict verdict,
        bool blackHole,
        int? interfaceMtu,
        PCDiag.Net.PathMtuResult? path)
    {
        return verdict switch
        {
            PCDiag.Net.MtuVerdict.ConfirmedMismatch when blackHole => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = $"Oversized Don't-Fragment packets are being silently dropped on a path whose MTU ({path?.DetectedPathMtu} bytes) is below the interface MTU ({interfaceMtu} bytes) - a possible PMTU black hole. Check for VPN/PPPoE/tunnel overhead and whether intermediate devices suppress ICMP 'fragmentation needed' replies.",
                    RequiresAdmin = false,
                    Priority = 1
                },
                new DiagnosticRecommendation
                {
                    Text = $"If a VPN or PPPoE link is in use, lowering the interface MTU to the measured path MTU ({path?.DetectedPathMtu} bytes) is supported by this evidence.",
                    RequiresAdmin = true,
                    Priority = 2
                }
            },
            PCDiag.Net.MtuVerdict.ConfirmedMismatch => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = $"Investigate tunnel overhead (VPN/PPPoE) on this path. The measured path MTU ({path?.DetectedPathMtu} bytes) is below the interface MTU ({interfaceMtu} bytes); consider lowering the interface MTU to {path?.DetectedPathMtu} or correcting the upstream MTU.",
                    RequiresAdmin = true,
                    Priority = 1
                },
                new DiagnosticRecommendation
                {
                    Text = "Verify the router returns ICMP 'fragmentation needed' errors; some devices silently drop oversized packets, which breaks path MTU discovery.",
                    RequiresAdmin = false,
                    Priority = 2
                }
            },
            PCDiag.Net.MtuVerdict.PotentialIssue when blackHole => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "Oversized Don't-Fragment packets are being silently dropped (possible PMTU black hole). Check for VPN/PPPoE/tunnel overhead and whether intermediate devices suppress ICMP 'fragmentation needed' replies.",
                    RequiresAdmin = false,
                    Priority = 1
                },
                new DiagnosticRecommendation
                {
                    Text = $"If a VPN or PPPoE link is in use, lowering the interface MTU to the measured path MTU ({path?.DetectedPathMtu} bytes) is supported by this evidence.",
                    RequiresAdmin = true,
                    Priority = 2
                }
            },
            PCDiag.Net.MtuVerdict.PotentialIssue => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "The MTU boundary measurement was not fully confirmed. Re-run the MTU check; if the finding persists, investigate VPN/PPPoE overhead and the MTU settings on the interface and router.",
                    RequiresAdmin = false,
                    Priority = 2
                }
            },
            PCDiag.Net.MtuVerdict.InterfaceMtuUnknown => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "The interface MTU could not be read from WMI. Verify the adapter driver reports an MTU so it can be compared against the measured path MTU.",
                    RequiresAdmin = true,
                    Priority = 2
                }
            },
            PCDiag.Net.MtuVerdict.Unmeasurable => new[]
            {
                new DiagnosticRecommendation
                {
                    Text = "The path MTU could not be measured because ICMP echo did not reach the target. Run 'pcdiag check gateway' and 'pcdiag check packet-loss' to diagnose connectivity first.",
                    RequiresAdmin = false,
                    Priority = 2
                }
            },
            _ => Array.Empty<DiagnosticRecommendation>()
        };
    }

    private static IReadOnlyList<string> PossibleCauses(PCDiag.Net.MtuVerdict verdict)
    {
        return verdict switch
        {
            PCDiag.Net.MtuVerdict.ConfirmedMismatch or PCDiag.Net.MtuVerdict.PotentialIssue => new[]
            {
                "VPN tunnel, PPPoE, or other encapsulation overhead reducing the path MTU.",
                "An intermediate router with a lower MTU that drops oversized packets.",
                "PMTU discovery disabled or ICMP 'fragmentation needed' suppressed on the path.",
                "A misconfigured interface MTU on the PC or the router."
            },
            PCDiag.Net.MtuVerdict.Unmeasurable => new[]
            {
                "The default gateway is unreachable.",
                "ICMP echo is blocked by a firewall, router, or ISP.",
                "The network adapter has no valid default gateway."
            },
            _ => Array.Empty<string>()
        };
    }

    private static double ComputeConfidence(PCDiag.Net.MtuVerdict verdict, bool blackHole)
    {
        return verdict switch
        {
            PCDiag.Net.MtuVerdict.ConfirmedMismatch => 0.9,
            PCDiag.Net.MtuVerdict.PotentialIssue when blackHole => 0.7,
            PCDiag.Net.MtuVerdict.PotentialIssue => 0.6,
            PCDiag.Net.MtuVerdict.Healthy => 0.8,
            _ => 0.9
        };
    }

    /// <summary>
    /// Fixes offered for an MTU/path finding. A confirmed or potential mismatch
    /// can be addressed by resetting the interface MTU to the Windows default (1500).
    /// Healthy or unavailable results offer no fixes.
    /// </summary>
    public IReadOnlyList<DiagnosticFix> GetFixes(DiagnosticResult result)
    {
        if (result.Status != DiagnosticStatus.Finding || result.Severity < DiagnosticSeverity.Suspicious)
            return Array.Empty<DiagnosticFix>();

        var adapter = PCDiag.Fixes.NetworkFixHelpers.GetActiveAdapterName(result);
        if (adapter is null)
            return Array.Empty<DiagnosticFix>();

        var problem = result.Severity >= DiagnosticSeverity.Warning
            ? "Interface MTU exceeds the measured path MTU, causing packet loss or black holes."
            : "Interface MTU may exceed the measured path MTU; the measurement was not fully confirmed.";

        return new[] { new MtuAutoFix(problem, adapter) };
    }
}