# PCDiag — Master Specification & Architecture Plan

> **Document Status:** Phase 10 Complete  
> **Date:** 2026-08-20  
> **Version:** 1.7  

---

## 1. Project Vision

**PCDiag** is an interactive terminal-based Windows PC diagnostic tool that performs read-only analysis of a system's configuration, network, hardware, performance, and gaming readiness. It detects anomalies, assesses risk, and provides actionable recommendations — all without modifying the system.

### Target Users
- Power users diagnosing PC issues
- IT technicians performing quick system audits
- Gamers optimizing their systems for performance

### Design Principles
1. **Read-only by default** — Checks observe, never act. Fixes are opt-in and only ever applied after explicit user confirmation (Phase 10).
2. **Structured evidence** — Every finding includes evidence and a confidence score so users can evaluate the diagnosis themselves.
3. **Interactive by default** — `pcdiag` opens a simple terminal GUI: press **ENTER** to start the scan, watch live progress, then review results and drill into details.
4. **Self-contained output** — The terminal UI is the primary interface; no external dependencies to view results.
5. **Extensible checks** — Adding a new check means implementing one interface and registering it.

---

## 2. Technology Stack

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **Runtime** | .NET 8.0 (win-x64) | Modern LTS, good Windows integration, fast startup |
| **UI Framework** | Spectre.Console (0.57.0) | Rich interactive terminal UI: widgets, progress, prompts, tables |
| **WMI Access** | System.Management 10.0 | Direct WMI queries for hardware/OS data |
| **Testing** | xUnit 2.5 + Coverlet | Industry standard, good tooling |
| **Language** | C# 12, nullable enabled | Safety, expressiveness |

> **Note:** System.CommandLine was removed in Phase 2.5 — PCDiag is interactive by default (`pcdiag` opens the TUI). Minimal commands exist without launching the TUI: `pcdiag info` (Phase 3) prints the read-only system inventory, `pcdiag check dns` (Phase 4) runs the DNS resolution check, `pcdiag check mtu` / `pcdiag check gateway` / `pcdiag check packet-loss` (Phase 5) run the MTU, gateway, and packet-loss checks, `pcdiag check connections` / `pcdiag check tcp` (Phase 6) run the TCP health checks, `pcdiag check events` / `pcdiag check driver` / `pcdiag check whea` (Phase 7) run the event-log-based checks, and `pcdiag check memory` / `pcdiag check pagefile` / `pcdiag check storage` (Phase 9) run the memory, pagefile, and storage checks.

---

## 3. Current Codebase Assessment

### 3.1 What Exists Today

The project has a working foundation with the following components:

#### Core Domain Model (`src/PCDiag/Core/`)
| File | Purpose | Status |
|------|---------|--------|
| `IDiagnosticCheck.cs` | Contract for all checks (sync + async) | ✅ Complete |
| `DiagnosticCheck.cs` | Abstract base: timing, error isolation, permission/unavailable mapping | ✅ Complete |
| `DiagnosticResult.cs` | Result data contract (CheckId, Severity, Status, Evidence, Errors, etc.) | ✅ Complete |
| `DiagnosticEvidence.cs` | Individual evidence item (Description, Value, Expected, Source) | ✅ Complete |
| `DiagnosticRecommendation.cs` | User action suggestion (Text, Automatable, RequiresAdmin, Priority) | ✅ Complete |
| `DiagnosticError.cs` | Structured error (Code, Message, Exception) | ✅ Complete |
| `DiagnosticCategory.cs` | Enum: Network, Performance, Windows, Gaming, Hardware, Security | ✅ Complete |
| `DiagnosticStatus.cs` | Enum: Passed, Finding, Error, Skipped, Unavailable, PermissionDenied | ✅ Complete |
| `DiagnosticSeverity.cs` | Enum: Healthy(0) → Info(1) → Suspicious(2) → Warning(3) → Critical(4) | ✅ Complete |
| `DiagnosticContext.cs` | Scan context (Mode, IsAdministrator, CancellationToken, DefaultTimeout, Inventory) | ✅ Complete |
| `ScanMode.cs` | Enum: Quick, Standard, Deep | ✅ Complete |
| `Scanner.cs` | Engine: mode filtering, per-check timeout, cancellation, error isolation | ✅ Complete |
| `ScanSummary.cs` | Aggregated status counts, risk score, max severity | ✅ Complete |

#### Infrastructure (`src/PCDiag/Infrastructure/`)
| File | Purpose | Status |
|------|---------|--------|
| `CommandRunner.cs` | Safe external command execution with timeout (includes `CommandResult`) | ✅ Complete |
| `CheckRegistry.cs` | Central check registration + lookup by id/name | ✅ Complete |
| `SystemInfo.cs` | Safe system queries (OS version, admin check, memory) | ✅ Complete |
| `WmiQuery.cs` | Never-throwing WMI/CIM wrapper (Query, GetString, GetInt32/64, GetDateTime) | ✅ Complete |

#### System Inventory (`src/PCDiag/Inventory/`)
| File | Purpose | Status |
|------|---------|--------|
| `SystemInventory.cs` | Records: SystemInventory, OsInfo, HardwareInfo, GpuInfo, StorageDeviceInfo, MotherboardInfo, BiosInfo, NetworkInfo, NetworkAdapterInfo, WindowsInfo | ✅ Complete |
| `SystemInventoryCollector.cs` | Orchestrates all four providers into one `SystemInventory` | ✅ Complete |
| `SystemInfoProvider.cs` | OS info: name, build/UBR (RtlGetVersion P/Invoke), architecture, 64-bit, VM detection | ✅ Complete |
| `HardwareInfoProvider.cs` | CPU, cores, clock, RAM, GPUs + driver versions, storage, motherboard, BIOS/UEFI (WMI `Win32_*`) | ✅ Complete |
| `NetworkAdapterProvider.cs` | Adapters via .NET `NetworkInterface`; active connection prefers the adapter with a default gateway | ✅ Complete |
| `WindowsInfoProvider.cs` | Product name/edition, install date, uptime, boot time (WMI `Win32_OperatingSystem`) | ✅ Complete |
| `VmDetector.cs` | Pure VM-detection heuristic (manufacturer/model/CPU/HypervisorPresent signals → true/false/null) | ✅ Complete |

#### DNS Diagnostics (`src/PCDiag/Dns/`)
| File | Purpose | Status |
|------|---------|--------|
| `DnsMessage.cs` | Minimal RFC 1035 query builder + response parser (ID match, QR, RCODE, answer count) | ✅ Complete |
| `IDnsTransport.cs` | Probe abstraction over UDP DNS lookups (mock seam for tests) | ✅ Complete |
| `UdpDnsTransport.cs` | Real UDP/53 probe with per-probe timeout; timeouts/errors → results, never throws | ✅ Complete |
| `DnsProbeResult.cs` | Single probe outcome: Success / Failed / TimedOut + RTT + RCODE | ✅ Complete |
| `DnsMeasurementStats.cs` | Pure aggregation: attempts, successes, failures, timeouts, avg/min/max latency, rates | ✅ Complete |
| `DnsHealth.cs` | Classifier: Healthy / Slow / Unreliable / Unreachable / NoConfiguration | ✅ Complete |
| `DnsOptions.cs` | Tuning constants (probes/resolver, timeout, thresholds, safe test domains) | ✅ Complete |
| `IDnsServerSource.cs` | DNS server discovery abstraction (mock seam) | ✅ Complete |
| `WmiDnsServerSource.cs` | Reads `Win32_NetworkAdapterConfiguration` DNS servers; parse/dedupe is pure and tested | ✅ Complete |

#### MTU & Path Diagnostics (`src/PCDiag/Net/`)
| File | Purpose | Status |
|------|---------|--------|
| `PingProbeResult.cs` | Single ICMP probe outcome: Success / FragmentationNeeded / Unreachable / TimedOut / Failed + RTT | ✅ Complete |
| `IPingProbe.cs` | Probe abstraction over `System.Net.NetworkInformation.Ping` (mock seam for tests) | ✅ Complete |
| `SystemPingProbe.cs` | Real ICMP probe with DF-bit support; maps `IPStatus.PacketTooBig` → FragmentationNeeded | ✅ Complete |
| `PingMeasurementStats.cs` | Pure aggregation: attempts, successes, failures, timeouts, avg/min/max latency, rates | ✅ Complete |
| `ProbeRunner.cs` | Runs repeated probes with early abort when a target stops replying | ✅ Complete |
| `TargetResolver.cs` | Resolves test targets to IPv4-preferred addresses (fully-qualified `System.Net.Dns`) | ✅ Complete |
| `NetOptions.cs` | Gateway/packet-loss tuning constants (probes, timeouts, loss/latency thresholds, max targets) | ✅ Complete |
| `MtuOptions.cs` | MTU tuning constants (search range, confirmation probes, timeout, IPv4/ICMP overhead = 28) | ✅ Complete |
| `PathMtuSearcher.cs` | Bounded binary search for the largest DF packet + boundary confirmation + black-hole detection | ✅ Complete |
| `GatewayClassifier.cs` | Pure classifier: Healthy / Slow / Lossy / Unreachable | ✅ Complete |
| `PacketLossClassifier.cs` | Pure classifier: Healthy / Elevated / Slow / Lossy / Unreachable / InternetUnreachable | ✅ Complete |
| `MtuClassifier.cs` | Pure verdict: Healthy / PotentialIssue / ConfirmedMismatch / Unmeasurable / InterfaceMtuUnknown | ✅ Complete |
| `IInterfaceMtuSource.cs` | Interface MTU discovery abstraction (mock seam) | ✅ Complete |
| `WmiInterfaceMtuSource.cs` | Reads `Win32_NetworkAdapterConfiguration.MTU` (by IP) with a `MSFT_NetIPInterface.NlMtu` fallback (by name); parse/match logic is pure and tested | ✅ Complete |

#### TCP Health (`src/PCDiag/Net/Tcp/`)
| File | Purpose | Status |
|------|---------|--------|
| `TcpConnectionState.cs` | MIB/TCP state enum (Listen/Established/CloseWait/TimeWait/Bound...) + pure `FromMibState` mapping | ✅ Complete |
| `TcpConnectionRecord.cs` | Single TCP connection/endpoint (state, addresses, ports, owning PID) | ✅ Complete |
| `ITcpConnectionSource.cs` | Connection-table abstraction (mock seam); `WmiTcpConnectionSource` reads `MSFT_NetTCPConnection` | ✅ Complete |
| `TcpStateSummary.cs` | Pure aggregation: per-state counts, per-process CLOSE_WAIT/established, distinct in-range local ports | ✅ Complete |
| `TcpCumulativeStats.cs` | Cumulative stats + contextual ratios (failures/initiations, retransmitted/segments); `ITcpStatsSource` | ✅ Complete |
| `NetTcpStatsSource.cs` | .NET `GetTcpIPv4Statistics` (failures, resets) + `Win32_PerfRawData_Tcpip_TCPv4` perf counters (segments, retransmits, resets) | ✅ Complete |
| `TcpConfiguration.cs` | Read-only TCP config record + `TcpAutotuningLevel` enum + `ITcpConfigSource` | ✅ Complete |
| `WmiTcpConfigSource.cs` | Registry reads (`Tcpip\Parameters`, never writes) + `MSFT_NetTCPSetting` auto-tuning/port range; pure `MapAutotuningLevel` | ✅ Complete |
| `WmiTcpAdapterErrorSource.cs` | `Win32_PerfRawData_Tcpip_NetworkInterface` error counters with normalized-name adapter matching | ✅ Complete |
| `TcpOptions.cs` | Thresholds (documented in Phase 6 section below) | ✅ Complete |
| `TcpConnectionsClassifier.cs` | Contextual connection verdict: TIME_WAIT vs port pool, CLOSE_WAIT clusters, established | ✅ Complete |
| `TcpHealthClassifier.cs` | Contextual health verdict: ratios, auto-tuning, registry tweaks, adapter error rate | ✅ Complete |

#### CLI (`src/PCDiag/CLI/`)
| File | Purpose | Status |
|------|---------|--------|
| `InventoryCommand.cs` | Minimal `pcdiag info`: collect + print inventory, exit | ✅ Complete |
| `DnsCommand.cs` | Minimal `pcdiag check dns`: run DNS check + print detailed report, exit | ✅ Complete |
| `MtuCommand.cs` | Minimal `pcdiag check mtu [target]`: run MTU check + print detailed report, exit | ✅ Complete |
| `GatewayCommand.cs` | Minimal `pcdiag check gateway`: run gateway check + print detailed report, exit | ✅ Complete |
| `PacketLossCommand.cs` | Minimal `pcdiag check packet-loss [target...]`: run packet-loss check + print report, exit | ✅ Complete |
| `TcpCommand.cs` | Minimal `pcdiag check tcp`: run TCP health check + print detailed report, exit | ✅ Complete |
| `ConnectionsCommand.cs` | Minimal `pcdiag check connections`: run connection-states check + print report, exit | ✅ Complete |

#### Interactive UI (`src/PCDiag/Interactive/`)
| File | Purpose | Status |
|------|---------|--------|
| `InteractiveApp.cs` | TUI entry point: title screen, start button (ENTER), progress, results, detail/info/rerun/exit menu | ✅ Complete |
| `SeverityStyling.cs` | Pure helpers: severity → color/markup, risk-score color thresholds | ✅ Complete |
| `ResultsTableBuilder.cs` | Builds the Spectre results table from a `ScanSummary` | ✅ Complete |

#### Reporting (`src/PCDiag/Reporting/`)
| File | Purpose | Status |
|------|---------|--------|
| `TerminalRenderer.cs` | Detailed per-check output (CHECK / STATUS / SEVERITY / EVIDENCE / etc.); reused by the TUI detail view | ✅ Complete |
| `InventoryRenderer.cs` | Plain-text inventory report (used by `pcdiag info` and the TUI "System info" menu item); nulls → "(unavailable)" | ✅ Complete |

#### Checks Implemented
| Check ID | Name | Category | Status |
|----------|------|----------|--------|
| `WIN-ENV-001` | Environment | Windows | ✅ Example (proves scan architecture) |
| `NET-DNS-001` | DNS Resolution | Network | ✅ Real check (multiple probes, per-resolver stats, reliability/reachability) |
| `NET-MTU-001` | Interface & Path MTU | Network | ✅ Real check (interface MTU vs measured path MTU, DF probing, black-hole detection) |
| `NET-GWY-001` | Default Gateway | Network | ✅ Real check (reachability, loss, latency) |
| `NET-LOSS-001` | Packet Loss & Latency | Network | ✅ Real check (gateway + up to two internet endpoints) |
| `NET-CONN-001` | TCP Connection States | Network | ✅ Real check (TIME_WAIT vs port pool, CLOSE_WAIT clusters, established) |
| `NET-TCP-001` | TCP Configuration & Statistics | Network | ✅ Real check (retransmissions, failures, auto-tuning, registry tweaks, adapter errors) |
| *(empty)* | Performance, Hardware, Gaming, Security checks | — | 🔲 Not started |

#### Tests (`tests/PCDiag.Tests/`)
| File | Purpose | Status |
|------|---------|--------|
| `ModelTests.cs` | Result creation, defaults, severity ordering, structured errors | ✅ Complete (6 tests) |
| `ScannerTests.cs` | Sync/async execution, cancellation, timeout, permission, unavailable, failures | ✅ Complete (11 tests) |
| `ScanSummaryTests.cs` | Status counts, risk score, max severity | ✅ Complete (6 tests) |
| `ScoringTests.cs` | Documented risk score model (max-dominant, confidence-weighted) | ✅ Complete (10 tests) |
| `ReportingTests.cs` | Rich/plain rendering, grouping, detailed sections, progress, version | ✅ Complete (12 tests) |
| `EnvironmentCheckTests.cs` | Example check sync + async paths | ✅ Complete (2 tests) |
| `InteractiveStylingTests.cs` | Severity → color/markup, risk-score color thresholds | ✅ Complete (11 tests) |
| `ResultsTableTests.cs` | Spectre results table construction and rendering | ✅ Complete (4 tests) |
| `InteractiveAppTests.cs` | End-to-end TUI flow: ENTER starts scan + results, ESC exits, System info menu prints inventory; tests inject checks to avoid network access | ✅ Complete (3 tests) |
| `VmDetectorTests.cs` | VM heuristic: strong → true, no → false, weak → null, empty inputs | ✅ Complete (8 tests) |
| `InventoryRendererTests.cs` | Inventory report rendering, "(unavailable)" placeholder, multi-GPU/adapter, active connection | ✅ Complete (6 tests) |
| `SystemInfoProviderTests.cs` | OS info invariants (machine name, arch, 64-bit, build > 0) | ✅ Complete (6 tests) |
| `HardwareInfoProviderTests.cs` | Hardware invariants (processors, collections, GPU/storage names) | ✅ Complete (6 tests) |
| `NetworkAdapterProviderTests.cs` | Adapter invariants (names/status, active connection is an active adapter) | ✅ Complete (4 tests) |
| `WindowsInfoProviderTests.cs` | Uptime/boot invariants; edition extraction logic | ✅ Complete (10 tests) |
| `SystemInventoryCollectorTests.cs` | Aggregate population of all sections | ✅ Complete (3 tests) |
| `DnsMessageTests.cs` | Query build (ID, flags, labels) + response parse (mismatch ID, RCODE, truncated, compressed names) | ✅ Complete (11 tests) |
| `DnsMeasurementStatsTests.cs` | Avg/min/max, failure/timeout counts, success/failure rates, empty input | ✅ Complete (6 tests) |
| `DnsClassifierTests.cs` | Per-resolver + overall classification; latency-only never triggers DNS-change | ✅ Complete (14 tests) |
| `DnsServerSourceTests.cs` | Parse/normalize/dedupe of WMI DNS server strings (IPv4/IPv6) | ✅ Complete (6 tests) |
| `DnsDiagnosticsCheckTests.cs` | End-to-end check with fakes: healthy/slow/unreliable/unreachable/no-config/partial + recommendations | ✅ Complete (7 tests) |
| `Net/NetFakes.cs` | Shared fake infra: `FakePingProbe` (records targets), `FakeMtuSource`, `NetInventory`, `PathSimulator` (cooperative/black-hole/dead/gateway-then behaviors) | ✅ Complete |
| `Net/PingMeasurementStatsTests.cs` | Aggregation math + `SystemPingProbe.Map` IPStatus mapping | ✅ Complete (9 tests) |
| `Net/PathMtuSearcherTests.cs` | Binary search: full/PPPoE/reduced MTU, black hole, dead target, bounded probe count, custom max payload, unconfirmed boundary | ✅ Complete (8 tests) |
| `Net/ClassifierAndLookupTests.cs` | Gateway/PacketLoss/Mtu classifiers + `WmiInterfaceMtuSource` lookup (by IP and by interface name) | ✅ Complete (37 tests) |
| `Net/MtuDiagnosticsCheckTests.cs` | End-to-end MTU check: 1500/1492/9000 healthy, confirmed mismatch, black hole, interface-unknown, no gateway, dead target, gateway fallback, configurable target | ✅ Complete (11 tests) |
| `Net/GatewayCheckTests.cs` | End-to-end gateway check: healthy, unreachable, lossy, slow, no gateway, no connection | ✅ Complete (6 tests) |
| `Net/PacketLossCheckTests.cs` | End-to-end packet-loss check: healthy, internet loss, gateway unreachable, internet unreachable, cancellation, configurable targets | ✅ Complete (10 tests) |
| `Net/Tcp/TcpFakes.cs` | Shared fake infra: `FakeTcpConnectionSource`, `FakeTcpStatsSource`, `FakeTcpConfigSource`, `FakeAdapterErrorSource`, `TcpConn` builders, `TcpInventory` (active adapter + uptime) | ✅ Complete |
| `Net/Tcp/TcpStateMappingTests.cs` | MIB-state mapping, adapter-name normalization, auto-tuning level mapping | ✅ Complete (18 tests) |
| `Net/Tcp/TcpStateSummaryTests.cs` | State counts, per-process breakdowns, dynamic-range port counting | ✅ Complete (6 tests) |
| `Net/Tcp/TcpConnectionsClassifierTests.cs` | TIME_WAIT vs port pool (small pool share never flagged), CLOSE_WAIT clusters/leaks, established, worst-wins | ✅ Complete (11 tests) |
| `Net/Tcp/TcpHealthClassifierTests.cs` | Retransmission/failure ratio bands, auto-tuning states, group policy, registry tweaks, adapter error rates, worst-wins | ✅ Complete (33 tests) |
| `Net/Tcp/TcpConnectionsCheckTests.cs` | End-to-end connection check: healthy, high TIME_WAIT contextualized (not "leak"), CLOSE_WAIT cluster, port range | ✅ Complete (5 tests) |
| `Net/Tcp/TcpHealthCheckTests.cs` | End-to-end TCP health check: healthy, high retransmission, disabled auto-tuning, registry evidence, adapter matching/rates, unavailable counters | ✅ Complete (9 tests) |

### 3.2 Gaps & Issues Identified

1. **No JSON/file output** — Results are terminal-only; no export capability.
2. **No `--json` flag** — Cannot pipe output to other tools or save reports. `pcdiag info` (Phase 3) prints a text inventory report, but no structured export exists yet.
3. **Empty check categories** — Performance, Hardware, Gaming, and Security have no checks.
4. **Six real network checks so far** — `NET-DNS-001` (DNS Resolution), `NET-MTU-001` (Interface & Path MTU), `NET-GWY-001` (Default Gateway), `NET-LOSS-001` (Packet Loss & Latency), `NET-CONN-001` (TCP Connection States), and `NET-TCP-001` (TCP Configuration & Statistics) are the real diagnostics; performance, hardware, gaming, and security checks arrive in later phases.
5. **No configuration/options file** — Cannot persist user preferences (e.g., default scan depth).
6. **No cross-reference between checks** — Each check runs independently; no correlation engine.

---

## 4. Architecture

### 4.1 High-Level Architecture

```
┌─────────────────────────────────────────────────┐
│              Interactive UI Layer               │
│  InteractiveApp (Spectre.Console TUI)            │
│  Title │ Start │ Progress │ Results │ Menu        │
│   (+ System info view via InventoryRenderer)    │
└──────────────────────┬──────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────┐
│            System Inventory Layer               │
│  SystemInventoryCollector (gathered pre-scan)   │
│  SystemInfo/Hardware/Network/Windows providers  │
│  → DiagnosticContext.Inventory                  │
└──────────────────────┬──────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────┐
│               Diagnostic Scanner               │
│  Scanner: ScanAsync / RunAsync + ScanSummary   │
│  CheckRegistry: discovers IDiagnosticCheck impls │
└──────────────────────┬──────────────────────────┘
                       │
         ┌──────────────┼──────────────────┐
         ▼              ▼                  ▼
    ┌─────────┐  ┌───────────┐  ┌────────────────┐
    │ Network │  │Performance│  │    Windows      │
    │ Checks  │  │  Checks   │  │    Checks       │
    └────┬────┘  └─────┬─────┘  └───────┬────────┘
         │             │                │
         ▼             ▼                ▼
    ┌─────────┐  ┌───────────┐  ┌────────────────┐
    │Hardware │  │  Gaming   │  │   Security     │
    │ Checks  │  │  Checks   │  │   Checks       │
    └─────────┘  └───────────┘  └────────────────┘
         │             │                │
         └──────────────┼────────────────┘
                        ▼
┌──────────────────────────────────────────────────┐
│              Infrastructure Layer                │
│  CommandRunner │ SystemInfo │ WmiQuery (safe)     │
└──────────────────────┬───────────────────────────┘
                       ▼
┌──────────────────────────────────────────────────┐
│              Reporting Layer                     │
│  TerminalRenderer │ InventoryRenderer │ Json      │
│  (planned)                                       │
└──────────────────────────────────────────────────┘
```

### 4.2 Data Flow

```
User launches pcdiag
        │
        ▼
InteractiveApp shows title + START SCAN button
        │  (ENTER pressed)
        ▼
SystemInventoryCollector gathers read-only inventory
        │  (SystemInfo / HardwareInfo / NetworkAdapter / WindowsInfo providers)
        ▼
Scanner runs checks with live progress bar
        │   each check receives DiagnosticContext.Inventory
        ├──▶ For each IDiagnosticCheck:
        │      check.ExecuteAsync()
        │        ├── Query system (CommandRunner / WMI / .NET APIs)
        │        ├── Analyze results
        │        ├── Collect Evidence items
        │        └── Return DiagnosticResult
        │
        ▼
Scanner aggregates results into ScanSummary
        │
        ▼
InteractiveApp renders results table + risk score
        ├── Severity-colored table of all checks
        ├── Risk score (0-100)
        ├── Counts + duration footer
        └── Menu: view check details / system info / re-run / exit
```

### 4.3 Check Naming Convention

All checks follow a strict ID pattern:

```
{CATEGORY}-{SUBSYSTEM}-{NNN}
```

| Prefix | Category | Example |
|--------|----------|---------|
| `NET-` | Network | `NET-DNS-001`, `NET-MTU-001` |
| `WIN-` | Windows | `WIN-SYS-001`, `WIN-UPD-001` |
| `PERF-` | Performance | `PERF-DPC-001`, `PERF-CPU-001` |
| `HW-` | Hardware | `HW-WHEA-001`, `HW-DISK-001` |
| `GAME-` | Gaming | `GAME-GPU-001`, `GAME-OPT-001` |
| `SEC-` | Security | `SEC-FW-001`, `SEC-UAC-001` |

### 4.4 Severity Assessment Rules

Each check must evaluate findings against these severity thresholds:

| Severity | Meaning | Risk Weight |
|----------|---------|-------------|
| **Healthy** (0) | System operating normally | 0 |
| **Info** (1) | Notable but not problematic | 1 |
| **Suspicious** (2) | Unusual; worth investigating | 2 |
| **Warning** (3) | Likely impacting performance/stability | 3 |
| **Critical** (4) | Serious system issue | 4 |

### 4.5 Risk Score Model (Documented)

The risk score (0–100) is intentionally **max-dominant**: the worst finding drives the
score, and healthy checks contribute nothing, so a single catastrophic result can never
be diluted by dozens of healthy ones.

**Severity weights:**

| Severity | Weight |
|----------|--------|
| Healthy | 0 |
| Info | 15 |
| Suspicious | 35 |
| Warning | 60 |
| Critical | 85 |

**Per-result contribution:** `weight × confidence` (confidence clamped to 0.0–1.0).
A less-certain diagnosis carries proportionally less risk. Healthy checks always
contribute 0 and therefore never increase risk.

**Aggregation:**

```
maxContribution   = max(contribution) over countable results
bonus             = 15% of (sum of contributions − maxContribution)
Score             = min(100, maxContribution + bonus)
```

Additional findings add a capped bonus so a cluster of issues still registers, but the
worst finding dominates. Countable results are those whose status is `Passed` or
`Finding`; `Error`, `Skipped`, `Unavailable`, and `PermissionDenied` results are
excluded (they reflect operational failure, not assessed risk).

---

## 5. Proposed Project Structure

### 5.1 Current Structure (Validated)

```
PCDiag.sln
├── src/PCDiag/
│   ├── PCDiag.csproj
│   ├── Program.cs                          # Entry point: `pcdiag info` / `pcdiag check dns`, else InteractiveApp
│   ├── Interactive/
│   │   ├── InteractiveApp.cs               # TUI loop: title, start, progress, results, menu (incl. System info)
│   │   ├── SeverityStyling.cs              # Severity/risk → color + markup helpers
│   │   └── ResultsTableBuilder.cs          # Spectre results table from ScanSummary
│   ├── CLI/
│   │   ├── InventoryCommand.cs             # `pcdiag info` — collect + print inventory
│   │   ├── DnsCommand.cs                   # `pcdiag check dns` — run DNS check + print report
│   │   ├── MtuCommand.cs                   # `pcdiag check mtu [target]` — MTU check + report
│   │   ├── GatewayCommand.cs               # `pcdiag check gateway` — gateway check + report
│   │   ├── PacketLossCommand.cs            # `pcdiag check packet-loss [target...]` — check + report
│   │   ├── TcpCommand.cs                   # `pcdiag check tcp` — TCP health check + report
│   │   └── ConnectionsCommand.cs           # `pcdiag check connections` — connection states + report
│   ├── Core/
│   │   ├── DiagnosticCategory.cs           # Category enum
│   │   ├── DiagnosticStatus.cs             # Status enum (incl. Unavailable, PermissionDenied)
│   │   ├── DiagnosticSeverity.cs           # Severity enum
│   │   ├── ScanMode.cs                     # Quick / Standard / Deep
│   │   ├── DiagnosticCheck.cs              # Abstract base class
│   │   ├── DiagnosticCheckException.cs     # Structured failure signal
│   │   ├── DiagnosticContext.cs            # Scan context (incl. Inventory)
│   │   ├── DiagnosticError.cs              # Structured error (Code, Message, Exception)
│   │   ├── DiagnosticEvidence.cs           # Evidence model
│   │   ├── DiagnosticRecommendation.cs     # Recommendation model
│   │   ├── DiagnosticResult.cs             # Result model (incl. PossibleCauses, Limitations)
│   │   ├── IDiagnosticCheck.cs             # Check interface (sync + async)
│   │   ├── Scanner.cs                      # Scan engine (progress callback, timeouts, isolation)
│   │   └── ScanSummary.cs                  # Aggregated results + documented risk score
│   ├── Inventory/
│   │   ├── SystemInventory.cs              # Inventory record models
│   │   ├── SystemInventoryCollector.cs     # Orchestrates the four providers
│   │   ├── SystemInfoProvider.cs           # OS info + build/UBR (RtlGetVersion) + VM detection
│   │   ├── HardwareInfoProvider.cs         # CPU/RAM/GPU/storage/motherboard/BIOS via WMI
│   │   ├── NetworkAdapterProvider.cs       # Network adapters via NetworkInterface
│   │   ├── WindowsInfoProvider.cs          # Product/edition/uptime via Win32_OperatingSystem
│   │   └── VmDetector.cs                   # Pure VM-detection heuristic
│   ├── Dns/
│   │   ├── DnsMessage.cs                   # RFC 1035 query build + response parse
│   │   ├── IDnsTransport.cs                # Probe abstraction (mock seam)
│   │   ├── UdpDnsTransport.cs              # Real UDP/53 probe with timeout
│   │   ├── DnsProbeResult.cs               # Single probe outcome
│   │   ├── DnsMeasurementStats.cs          # Probe aggregation (avg/min/max, failures, timeouts)
│   │   ├── DnsHealth.cs                    # Healthy/Slow/Unreliable/Unreachable/NoConfiguration
│   │   ├── DnsOptions.cs                   # Measurement tuning constants + safe test domains
│   │   ├── IDnsServerSource.cs             # DNS server discovery abstraction
│   │   └── WmiDnsServerSource.cs           # WMI-based server discovery + parse/dedupe
│   ├── Net/
│   │   ├── PingProbeResult.cs              # Probe outcome enum + result record
│   │   ├── IPingProbe.cs                   # Probe abstraction (mock seam)
│   │   ├── SystemPingProbe.cs              # Real ICMP probe with DF bit support
│   │   ├── PingMeasurementStats.cs         # Probe aggregation (avg/min/max, rates)
│   │   ├── ProbeRunner.cs                  # Repeated probes with early abort
│   │   ├── TargetResolver.cs               # IPv4-preferred target resolution
│   │   ├── NetOptions.cs                   # Gateway/packet-loss tuning constants
│   │   ├── MtuOptions.cs                   # MTU search tuning constants
│   │   ├── PathMtuSearcher.cs              # Bounded binary-search path MTU measurement
│   │   ├── GatewayClassifier.cs            # Healthy/Slow/Lossy/Unreachable
│   │   ├── PacketLossClassifier.cs         # Healthy/Elevated/Slow/Lossy/Unreachable/InternetUnreachable
│   │   ├── MtuClassifier.cs                # Healthy/PotentialIssue/ConfirmedMismatch/Unmeasurable/InterfaceMtuUnknown
│   │   ├── IInterfaceMtuSource.cs          # Interface MTU discovery abstraction
│   │   └── WmiInterfaceMtuSource.cs        # MTU via WMI adapter config + NetIPInterface fallback
│   │   └── Tcp/
│   │       ├── TcpConnectionState.cs       # MIB/TCP state enum + FromMibState mapping
│   │       ├── TcpConnectionRecord.cs      # Single connection/endpoint record
│   │       ├── ITcpConnectionSource.cs     # Connection-table abstraction
│   │       ├── WmiTcpConnectionSource.cs   # MSFT_NetTCPConnection table reader
│   │       ├── TcpStateSummary.cs          # State + per-process aggregation
│   │       ├── TcpCumulativeStats.cs       # Cumulative stats + ratios; ITcpStatsSource
│   │       ├── NetTcpStatsSource.cs        # .NET TcpStatistics + TCPv4 perf counters
│   │       ├── TcpConfiguration.cs         # Read-only TCP config record; ITcpConfigSource
│   │       ├── WmiTcpConfigSource.cs       # Registry + MSFT_NetTCPSetting reader (never writes)
│   │       ├── WmiTcpAdapterErrorSource.cs # NetworkInterface perf error counters
│   │       ├── TcpOptions.cs               # TCP threshold constants
│   │       ├── TcpConnectionsClassifier.cs # Connection-state verdict (contextual)
│   │       └── TcpHealthClassifier.cs      # TCP health verdict (ratios, tuning, tweaks)
│   ├── Checks/
│   │   ├── Windows/
│   │   │   └── EnvironmentCheck.cs         # ✅ WIN-ENV-001 (example)
│   │   └── Network/
│   │       ├── DnsDiagnosticsCheck.cs      # ✅ NET-DNS-001 (DNS Resolution)
│   │       ├── MtuDiagnosticsCheck.cs      # ✅ NET-MTU-001 (Interface & Path MTU)
│   │       ├── GatewayCheck.cs             # ✅ NET-GWY-001 (Default Gateway)
│   │       ├── PacketLossCheck.cs          # ✅ NET-LOSS-001 (Packet Loss & Latency)
│   │       ├── TcpConnectionsCheck.cs      # ✅ NET-CONN-001 (TCP Connection States)
│   │       └── TcpHealthCheck.cs           # ✅ NET-TCP-001 (TCP Configuration & Statistics)
│   ├── Infrastructure/
│   │   ├── CheckRegistry.cs                # Check registration + lookup by id/name
│   │   ├── CommandRunner.cs                # Command execution (incl. CommandResult)
│   │   ├── SystemInfo.cs                   # System info helpers
│   │   └── WmiQuery.cs                     # Never-throwing WMI wrapper
│   └── Reporting/
│       ├── TerminalRenderer.cs             # Detailed per-check view (reused by TUI)
│       └── InventoryRenderer.cs            # Plain-text inventory report
└── tests/PCDiag.Tests/
    ├── PCDiag.Tests.csproj
    ├── ModelTests.cs                       # Model tests
    ├── ScannerTests.cs                     # Scanner tests
    ├── ScanSummaryTests.cs                 # Summary counts + risk score
    ├── ScoringTests.cs                     # Documented risk score model tests
    ├── ReportingTests.cs                   # Rendering tests (rich/plain, sections)
    ├── InteractiveStylingTests.cs          # Severity/risk styling helpers
    ├── ResultsTableTests.cs                # Spectre results table
    ├── InteractiveAppTests.cs              # End-to-end TUI flow tests
    ├── EnvironmentCheckTests.cs            # Example check tests
    ├── TestChecks.cs                       # Mock checks for tests
    └── Inventory/
        ├── VmDetectorTests.cs              # VM heuristic tests
        ├── InventoryRendererTests.cs       # Inventory report tests
        ├── SystemInfoProviderTests.cs      # OS info invariants
        ├── HardwareInfoProviderTests.cs    # Hardware invariants
        ├── NetworkAdapterProviderTests.cs  # Adapter invariants
        ├── WindowsInfoProviderTests.cs     # Windows invariants + edition logic
        └── SystemInventoryCollectorTests.cs# Aggregate population tests
    └── Dns/
        ├── DnsFakes.cs                     # Fake transport/server source + probe helpers
        ├── DnsMessageTests.cs              # Query build + response parse
        ├── DnsMeasurementStatsTests.cs     # Aggregation math
        ├── DnsClassifierTests.cs           # Classification + recommendation logic
        ├── DnsServerSourceTests.cs         # WMI string parse/dedupe
        └── DnsDiagnosticsCheckTests.cs     # End-to-end check with fakes
    └── Net/
        ├── NetFakes.cs                     # FakePingProbe/FakeMtuSource/NetInventory/PathSimulator
        ├── PingMeasurementStatsTests.cs    # Aggregation math + IPStatus mapping
        ├── PathMtuSearcherTests.cs         # Binary search, black hole, dead target, bounds
        ├── ClassifierAndLookupTests.cs     # Gateway/PacketLoss/Mtu classifiers + MTU lookup
        ├── MtuDiagnosticsCheckTests.cs     # End-to-end MTU check
        ├── GatewayCheckTests.cs            # End-to-end gateway check
        └── PacketLossCheckTests.cs         # End-to-end packet-loss check
    └── Net/Tcp/
        ├── TcpFakes.cs                     # Fake sources/builders + TcpInventory
        ├── TcpStateMappingTests.cs         # State/normalization/autotuning mapping
        ├── TcpStateSummaryTests.cs         # Aggregation + per-process breakdowns
        ├── TcpConnectionsClassifierTests.cs# Connection verdict (contextual)
        ├── TcpHealthClassifierTests.cs     # Health verdict (ratios, tuning, tweaks)
        ├── TcpConnectionsCheckTests.cs     # End-to-end connection check
        └── TcpHealthCheckTests.cs          # End-to-end TCP health check
```

### 5.2 Target Structure (After All Phases)

```
PCDiag.sln
├── src/PCDiag/
│   ├── PCDiag.csproj
│   ├── Program.cs
│   ├── Interactive/
│   │   ├── InteractiveApp.cs                # TUI loop
│   │   ├── SeverityStyling.cs
│   │   └── ResultsTableBuilder.cs
│   ├── Core/
│   │   ├── CheckCategory.cs
│   │   ├── CheckStatus.cs
│   │   ├── DiagnosticEngine.cs
│   │   ├── DiagnosticResult.cs
│   │   ├── Evidence.cs
│   │   ├── IDiagnosticCheck.cs
│   │   ├── Recommendation.cs
│   │   ├── Severity.cs
│   │   └── IReportFormatter.cs             # 🆕 Output abstraction
│   ├── Checks/
│   │   ├── Network/                        # 3 checks ✅
│   │   ├── Windows/                        # ~5 checks planned
│   │   ├── Performance/                    # ~4 checks planned
│   │   ├── Hardware/                       # ~3 checks planned
│   │   ├── Gaming/                         # ~3 checks planned
│   │   └── Security/                       # 🆕 ~3 checks planned
│   ├── Infrastructure/
│   │   ├── CheckRegistry.cs
│   │   ├── CommandRunner.cs
│   │   └── SystemInfo.cs
│   └── Reporting/
│       ├── TerminalRenderer.cs             # Detailed per-check view
│       ├── JsonReporter.cs                 # 🆕 JSON export
│       └── ReportFormatter.cs              # 🆕 Orchestrates output
└── tests/PCDiag.Tests/
    ├── PCDiag.Tests.csproj
    ├── Checks/
    │   ├── Network/                        # Check-specific tests
    │   └── ...
    ├── DiagnosticEngineTests.cs
    ├── ReportingTests.cs
    └── ModelTests.cs                       # Renamed from UnitTest1.cs
```

---

## 6. Core Architectural Decisions

### Decision 1: Interface-Based Check System
**Choice:** Every check implements `IDiagnosticCheck`.  
**Rationale:** Clean separation of concerns; each check is self-contained and independently testable. New categories or checks require zero changes to the engine.  
**Tradeoff:** Slightly more boilerplate per check vs. attribute-based discovery.

### Decision 2: Result-As-Data, Not Side-Effects
**Choice:** Checks return `DiagnosticResult` objects; reporting is separate.  
**Rationale:** Enables multiple output formats (console, JSON, CSV) from the same engine. Makes testing trivial.  
**Tradeoff:** Results must carry enough metadata for any renderer.

### Decision 3: Spectre.Console Interactive TUI
**Choice:** Spectre.Console (0.57.0) drives the whole terminal interface; System.CommandLine was removed.
**Rationale:** A simple, polished interactive experience: title banner, a START SCAN button activated by ENTER, live progress bars, a severity-colored results table, and a drill-down menu. Spectre.Console handles ANSI/color detection, terminal sizing, and cross-platform rendering.
**Tradeoff:** No general argument-driven scripting interface. Piped/redirected stdin still auto-starts the scan and prints results (useful for CI); `pcdiag info` prints the read-only inventory and `pcdiag check dns` runs the DNS check, both without launching the TUI. Structured export (JSON/CSV) is deferred.

### Decision 7: Read-Only System Inventory
**Choice:** Inventory is collected once per run by `SystemInventoryCollector` into an immutable `SystemInventory`, passed to checks via `DiagnosticContext.Inventory`, and exposed through `pcdiag info` and the TUI "System info" menu.
**Rationale:** Checks can reference rich, consistent system data without each re-querying WMI; the inventory is read-only, never modifies the system, and degrades gracefully (null/empty → "(unavailable)") on VMs, missing data, or permission limits.
**Tradeoff:** Inventory is a snapshot taken before the scan; it may not reflect mid-scan changes (acceptable for diagnostics).

### Decision 8: Hand-Rolled UDP DNS Client with Multiple Measurements
**Choice:** `PCDiag.Dns` implements a minimal RFC 1035 query builder/parser and probes each resolver over UDP/53 multiple times (default 5) against safe IANA-reserved domains, with per-probe timeouts and early-abort for unreachable resolvers.
**Rationale:** No external DNS library is needed; the wire parsing and statistics are pure and unit-testable; multiple measurements (not a single query) drive classification so transient blips don't mislead. Latency alone can only produce "Slow" (Suspicious) — reliability or reachability evidence is required before a DNS-change recommendation is offered.
**Tradeoff:** End-to-end latency includes recursion and cache state; the client only tests A records over UDP (no DoH/DoT/TCP); a single run is point-in-time.

### Decision 9: Bounded Binary-Search Path MTU with Conservative Network Use
**Choice:** `PCDiag.Net` measures the largest Don't-Fragment packet that traverses a path using a bounded binary search over payload sizes (default 68–1472 bytes), then confirms the boundary with repeated probes and records whether oversized packets get an ICMP "fragmentation needed" reply (cooperative PMTU discovery) or are silently dropped (possible PMTU black hole). Reachability checks probe the gateway 4 times (1 s timeout) and up to two internet endpoints 5 times (2 s timeout, early abort after 2 consecutive timeouts); the MTU search probes each path ~10–13 times plus 2 confirmation probes (1 s timeout).
**Rationale:** Multiple measurements drive classification so transient blips don't mislead; the probe budget is logarithmic and fixed, so the tool never floods the network; worst-case runtimes stay well under the 30 s per-check scanner timeout (MTU ≈ 26 s, packet-loss ≈ 24 s, gateway ≈ 4 s). Different MTUs (1500, 1492 PPPoE, 9000 jumbo) are all treated as healthy when the measurement agrees with the interface; findings always use "Potential MTU/path issue" wording unless the boundary is confirmed. The internet path is capped at 1500 bytes so large packets are never sent to the WAN; for jumbo interfaces the full-range gateway measurement drives the verdict.
**Tradeoff:** Path MTU can differ per destination and is measured only on the ICMP echo path; networks that block ICMP echo or suppress fragmentation-needed errors yield Unmeasurable/black-hole results rather than a wrong MTU.

### Decision 10: TCP Diagnostics Are Read-Only and Contextual
**Choice:** `PCDiag.Net.Tcp` reads TCP state, statistics, configuration, and adapter error counters from WMI, .NET `TcpStatistics`, perf counters, and the registry — and **never writes any registry value**. Verdicts are contextual: TIME_WAIT is judged as a fraction of the dynamic port pool (never "bad" on its own), connection failures and retransmissions are ratios against initiations/segments, adapter errors are a rate against uptime, and auto-tuning/registry tweaks are flagged as "non-default" rather than alarmist.
**Rationale:** TCP behavior is only meaningful in context; bare counts mislead (a busy browser generates many short-lived connections and some failed attempts). The user explicitly required no TCP registry tweaks, so the tool diagnoses but never "fixes" TCP by writing settings. Unavailable counters are reported as "Not available"/"unavailable" — never fabricated or mistaken for healthy.
**Tradeoff:** A single snapshot cannot measure growth or rates over time; the checks tell the user to re-run to see whether a pattern persists, and one-directional failure counts are not available from the .NET API (only the combined `FailedConnectionAttempts`), so direction is omitted from evidence.

### Decision 4: Sequential Execution (Currently)
**Choice:** Checks run one at a time, not in parallel.  
**Rationale:** Simpler error handling; avoids WMI contention; deterministic output order.  
**Tradeoff:** Slower for many checks. Could add parallel execution in a future optimization pass.

### Decision 5: No External DI Container
**Choice:** Manual dependency injection via `CheckRegistry` constructor calls.  
**Rationale:** This is a small terminal tool. A DI container (Autofac, etc.) adds unnecessary complexity. The engine receives checks via constructor injection; the registry resolves them.  
**Tradeoff:** Less flexible for plugin scenarios. Acceptable at this scale.

### Decision 6: Interactive Terminal as Primary Output
**Choice:** The Spectre.Console TUI is the primary UX; `TerminalRenderer` provides the detailed per-check view.
**Rationale:** Target users run this from a terminal. JSON/CSV export is secondary and opt-in via a future flag.
**Tradeoff:** Requires careful color/width handling on different terminals; Spectre.Console handles most of this automatically.

---

## 7. Check Design Contract

Every diagnostic check must follow this contract:

### Execution Rules
1. **NEVER modify the system** — read-only queries only (registry reads, WMI queries, process inspection, network pings).
2. **Handle all exceptions gracefully** — wrap in try/catch, return a result with `Status = Error` rather than throwing.
3. **Respect cancellation** — check `cancellationToken` and throw `OperationCanceledException` when cancelled.
4. **Set reasonable timeouts** — use `CommandRunner` with explicit timeouts (10-30 seconds).
5. **Collect comprehensive evidence** — every data point used in the diagnosis should be an `Evidence` item.
6. **Assign confidence** — rate your diagnosis confidence from 0.0 to 1.0.

### Output Structure
```
DiagnosticResult
├── CheckId          (e.g., "NET-DNS-001")
├── Name             (e.g., "DNS Resolution")
├── Category         (e.g., Network)
├── Severity         (Healthy | Info | Suspicious | Warning | Critical)
├── Status           (Passed | Finding | Error | Skipped)
├── Summary          (one-line human-readable)
├── Detail           (why-it-matters explanation)
├── Confidence       (0.0 - 1.0)
├── Evidence[]       (collected data points)
├── Recommendations[] (actionable next steps)
├── Duration         (execution time)
└── Errors[]         (if any)
```

---

## 8. Phased Implementation Plan

### Phase 0 — Architecture & Foundation ✅ (This Document)
- Define specification, architecture, and conventions
- Validate existing codebase against the plan
- Establish check naming, severity, and evidence standards
- Document all architectural decisions

### Phase 1 — Project Foundation ✅ Complete

**Scope delivered:**
- Core abstractions: `IDiagnosticCheck`, `DiagnosticCheck`, `DiagnosticResult`, `DiagnosticEvidence`, `DiagnosticRecommendation`, `DiagnosticError`, `DiagnosticSeverity`, `DiagnosticStatus`, `DiagnosticCategory`, `DiagnosticContext`, `Scanner`, `ScanSummary`
- Supports synchronous and asynchronous checks, cancellation, per-check timeouts, unavailable checks, permission failures, and structured errors
- Example check `WIN-ENV-001` (Environment) proves the scan architecture
- 25 unit tests covering result creation, severity, scanner behavior, failed and cancelled checks

### Phase 2 — CLI & Reporting ✅ Complete

**Scope delivered:**
- Commands: `scan [--quick|--deep|--plain|--group-by]`, `check <name>`, `list-checks`, `version`
- Rich terminal renderer (`TerminalRenderer`) with unicode symbols ✓ ℹ ⚠ ✕ and a plain ASCII mode (`--plain`)
- Category and severity grouping, live scan progress, elapsed time, final summary, risk score
- Documented max-dominant risk score model (see §4.5) — healthy checks never add risk; a single catastrophic result cannot be hidden by dozens of healthy results
- Detailed per-check output: CHECK / STATUS / SEVERITY / CONFIDENCE / WHAT WAS DETECTED / EVIDENCE / WHY IT MATTERS / POSSIBLE CAUSES / RECOMMENDED ACTIONS / LIMITATIONS
- Result model extended with `PossibleCauses` and `Limitations`
- 22 new reporting/scoring tests

### Phase 2.5 — Interactive Terminal UI ✅ Complete

**Scope delivered:**
- Replaced the CLI subcommands with a Spectre.Console interactive TUI — running `pcdiag` always opens the UI
- Title screen with START SCAN button: press **ENTER** to run the scan, **ESC** to exit
- Live progress bar during the scan (via the existing `Scanner.ScanAsync` progress callback)
- Severity-colored results table + risk score + counts + duration footer
- Menu after results: view a check's full detailed report (reuses `TerminalRenderer`), re-run the scan, or exit
- Ctrl+C cancellation support
- Redirected stdin auto-starts the scan and prints results (testable/smoke-testable without a TTY)
- Removed System.CommandLine dependency; added Spectre.Console 0.57.0
- New tests: styling helpers (11), results table (4), end-to-end TUI flow (2) — 64 total

### Phase 3 — Windows System Inventory ✅ Complete

**Scope delivered:**
- Read-only system inventory (`PCDiag.Inventory`) collected before the scan and exposed via `DiagnosticContext.Inventory` (never modified the system)
- `pcdiag info` command prints the full inventory as plain text; the TUI menu gains a "System info" option
- System info: machine name, OS version, exact build/UBR via `RtlGetVersion` P/Invoke, architecture, 64-bit, VM detection
- Hardware info via WMI `Win32_*`: CPU model/cores/clock, RAM, GPUs + driver versions, storage devices, motherboard, BIOS/UEFI + release date
- Network info via .NET `NetworkInterface`: all adapters (name, type, speed, MAC, status, IPs); active connection prefers the adapter with a default gateway
- Windows info: product name/edition, install date, uptime, boot time
- Missing/VM/permission-limited data degrades to null/empty and renders as "(unavailable)"
- `WmiQuery` never-throwing wrapper; `VmDetector` pure heuristic (manufacturer/model/CPU/HypervisorPresent signals)
- New tests: 43 inventory tests (VmDetector 8, InventoryRenderer 6, providers, collector) + 1 TUI System-info test — 108 total

### Phase 4 — DNS Diagnostics ✅ Complete

**Scope delivered:**
- `NET-DNS-001` (DNS Resolution) check probes each configured resolver multiple times (default 5) against the IANA-reserved safe test domains `example.com` / `example.org`, with a 1.5 s per-probe timeout and early-abort for unreachable resolvers (worst case ≈ 18 s < the 30 s scanner timeout)
- Active DNS servers discovered via WMI `Win32_NetworkAdapterConfiguration` (`IPEnabled`, `DNSServerSearchOrder`), deduplicated and capped at 3 resolvers
- Hand-rolled minimal RFC 1035 DNS client (`PCDiag.Dns`): `DnsMessage` build/parse, `UdpDnsTransport` (never throws), behind `IDnsTransport` / `IDnsServerSource` seams for mocking
- Per-resolver statistics exposed as evidence: resolver, attempts, successes, failures, timeouts, average/minimum/maximum latency, success rate
- Classification per resolver and overall: **Healthy / Slow / Unreliable / Unreachable / NoConfiguration**
  - No active DNS configuration → Status Unavailable, Severity Info (never hidden)
  - Slow (reliable but ≥ 500 ms avg) → Severity Suspicious; latency alone **never** triggers a DNS-change recommendation
  - Unreliable (failure+timeout rate ≥ 40%, or a partially unreachable resolver set) → Severity Warning
  - All resolvers unreachable → Severity Critical
- Recommendations are evidence-gated: DNS changes are only offered for Unreliable/Unreachable; Slow recommends checking local congestion/VPN instead; Healthy has no recommendations
- `pcdiag check dns` command (exact-match) prints the detailed report via `TerminalRenderer`; the check also runs in the interactive TUI scan
- InteractiveApp accepts an injected check list so TUI tests never touch the network
- New tests: 44 DNS tests (message 11, stats 6, classifier 14, server source 6, end-to-end 7) — 152 total

### Phase 5 — MTU & Path Diagnostics ✅ Complete

**Scope delivered:**
- Three new network checks, all registered in `CheckRegistry` so they run in the interactive TUI scan and support exact-match CLI commands:
  - `NET-MTU-001` (Interface & Path MTU): reads the interface MTU (WMI `Win32_NetworkAdapterConfiguration.MTU` matched by IP, with a `MSFT_NetIPInterface.NlMtu` fallback matched by name for adapters that report no MTU), then measures the largest Don't-Fragment packet to the gateway and an internet endpoint using a bounded binary search with boundary confirmation
  - `NET-GWY-001` (Default Gateway): reachability, packet loss, and latency to the default gateway (4 probes, 1 s timeout)
  - `NET-LOSS-001` (Packet Loss & Latency): gateway plus up to two internet endpoints (default 1.1.1.1 / 8.8.8.8; 5 probes, 2 s timeout, early abort after 2 consecutive timeouts)
- `PCDiag.Net` probe infrastructure behind mock seams (`IPingProbe`, `IInterfaceMtuSource`): `SystemPingProbe` (real ICMP, maps `IPStatus.PacketTooBig` → FragmentationNeeded), `ProbeRunner`, `TargetResolver` (IPv4-preferred), pure classifiers for gateway / packet-loss / MTU verdicts, and `PathMtuSearcher` (bounded binary search + boundary confirmation + black-hole detection; ~10–13 probes per path, worst case ≈ 26 s)
- MTU verdicts: **Healthy / PotentialIssue / ConfirmedMismatch / Unmeasurable / InterfaceMtuUnknown**
  - Different technologies are never flagged on their own: 1492 (PPPoE) and 9000 (jumbo) are Healthy when the measurement agrees with the interface
  - A measured path MTU below the interface MTU with a confirmed boundary → Warning (ConfirmedMismatch); unconfirmed → Suspicious (PotentialIssue)
  - Oversized DF packets silently dropped (no ICMP reply) → possible PMTU black hole, reported with the "Potential MTU/path issue" wording
  - Gateway dead or ICMP blocked → Unavailable (Info), never hidden as healthy
- Internet paths are capped at 1500 bytes so large packets are never sent to the WAN; a jumbo interface is judged against the full-range gateway measurement
- `pcdiag check mtu [target]`, `pcdiag check gateway`, `pcdiag check packet-loss [target...]` print detailed reports via `TerminalRenderer` and exit; unknown `pcdiag check` names print the available list
- New tests: 81 (stats/mapping 9, path MTU searcher 8, classifiers + MTU lookup 37, MTU check 11, gateway check 6, packet-loss check 10) — 233 total

### Phase 6 — TCP Health ✅ Complete

**Scope delivered:**
- Two new network checks, both registered in `CheckRegistry` (run in the interactive TUI scan) and exposed as exact-match CLI commands:
  - `NET-CONN-001` (TCP Connection States) — `pcdiag check connections`: reads the live connection table from `MSFT_NetTCPConnection` (state is a **Byte**: 2=Listen, 5=Established, 8=CloseWait, 11=TimeWait, 100=Bound) and aggregates per-state counts, distinct in-range local ports, and per-process CLOSE_WAIT/established breakdowns
  - `NET-TCP-001` (TCP Configuration & Statistics) — `pcdiag check tcp`: combines `.NET GetTcpIPv4Statistics` (failures = `FailedConnectionAttempts`, resets, cumulative connections), `Win32_PerfRawData_Tcpip_TCPv4` perf counters (segments sent/received/retransmitted, `ConnectionsReset`), `MSFT_NetTCPSetting` (auto-tuning level, dynamic port range), `Win32_PerfRawData_Tcpip_NetworkInterface` (adapter errors/discards), and read-only registry reads under `Tcpip\Parameters`
- **Interpretation is contextual (never bare counts):**
  - TIME_WAIT is judged as a fraction of the dynamic port pool and is **never labeled bad on its own**; only at ≥ 25% of the pool it is Elevated, ≥ 60% it is a Warning (pool exhaustion risk). A busy browser generates many TIME_WAIT sockets — normal.
  - CLOSE_WAIT is both a total-socket concern and a per-process concern: > 10 sockets Suspicious, > 50 Warning, or a single process owning > 25 → likely socket leak.
  - Established > 1000 Elevated, > 5000 Warning (possible runaway/P2P).
  - Retransmission ratio (retransmitted ÷ sent+received): ≥ 1% Suspicious, ≥ 5% Warning.
  - Connection failure ratio (failures ÷ initiations): ≥ 10% Suspicious, ≥ 30% Warning. Failures to dead hosts/blocked ports are partly normal; ratio keeps it contextual.
  - Adapter error **rate** (errors ÷ uptime): ≥ 0.01/s Suspicious, ≥ 0.10/s Warning.
  - Auto-tuning: Normal is expected; Disabled/Restricted/HighlyRestricted/Experimental are flagged "non-default" (never alarmist). Group-policy override (254 sentinel = "not configured") is surfaced only when it differs from the effective level.
  - Registry tweaks: `MaxUserPort < 5000` → Warning; `TcpTimedWaitDelay < 30` → Suspicious; `TcpWindowSize`/`GlobalMaxTcpWindowSize` set → Suspicious (they disable auto-tuning). Unset values are reported as "Windows default".
- **Threshold rationale** (why these values): the default dynamic port pool is 49152–65535 (16384 ports). 25% ≈ 4096 TIME_WAIT sockets — browsers/HTTP clients with keep-alive rarely exceed a few hundred, so 25% marks a large accumulation and 60% ≈ 9830 is the point where new outbound connections start risking port exhaustion. CLOSE_WAIT is inherently abnormal (the local app failed to close); > 10 indicates a pattern and > 50 or one process > 25 is a leak. Desktop PCs rarely hold more than ~1000 established connections; > 5000 is runaway territory. Retransmission ≥ 1% of segments is well above healthy Wi-Fi/LAN (< 0.1–0.5%) and ≥ 5% points to real loss. Failure ratio ≥ 10% exceeds normal dead-host/blocked-port noise (single digits %) and ≥ 30% means the link is broadly failing. Adapter error rates above 0.01/s (≈ 1 error/minute) and 0.10/s (≈ 1 error/10 s) bracket healthy hardware (often 0) and problematic links.
- **Read-only guarantee:** `Tcpip\Parameters` is opened for reads only; no registry value is ever written, and the checks state this in their limitations. Auto-tuning "fixes" are offered as optional manual commands in the recommendation text, never applied.
- New tests: 82 (mapping/normalization/autotuning 18, state summary 6, connections classifier 11, health classifier 33, connections check 5, health check 9) — 315 total
- Real-machine smoke test: `check connections` → Healthy (17 established, 18 TIME_WAIT, 0 CLOSE_WAIT, 0% of port pool); `check tcp` → Suspicious with evidence (24% connection failures, 1% retransmissions, autotuning Normal, all registry values default, adapter error counters matched via normalized name); TUI scan lists all 7 checks with correct verdicts.

### Phase 7 — Performance & Hardware Checks
*After Phase 6 approval.*

**Scope:**
- `PERF-DPC-001` — DPC latency check
- `PERF-CPU-001` — CPU throttling/thermal check
- `PERF-MEM-001` — Memory pressure / pagefile check
- `PERF-DISK-001` — Disk health (SMART) check
- `HW-WHEA-001` — WHEA hardware error check
- `HW-GPU-001` — GPU driver and temperature check
- Admin-elevated checks support

### Phase 8 — Gaming Checks & Advanced Features
*After Phase 7 approval.*

**Scope:**
- `GAME-OPT-001` — Game mode / power plan check
- `GAME-GPU-001` — GPU settings optimization
- `GAME-NET-001` — Network latency for gaming
- `pcdiag fix` command (opt-in automated fixes)
- Configuration file support (`pcdiag.json`)
- Summary history / trend tracking

### Phase 9 — Memory & Storage Diagnostics ✅ Complete

**Delivered:**
- `PERF-MEM-001` — Memory Usage & Pressure (`pcdiag check memory`)
- `PERF-PAG-001` — Pagefile Configuration (`pcdiag check pagefile`)
- `PERF-DISK-001` — Storage & Disk Health (`pcdiag check storage`)

**Memory sources (`WmiMemorySnapshotSource`):**
- `Win32_OperatingSystem` (KB units): installed RAM (TotalVisibleMemorySize).
- `Win32_PerfFormattedData_PerfOS_Memory`: committed bytes vs. commit limit, available bytes, Pages/sec, nonpaged/paged kernel pools.
- `Win32_PageFileUsage`: current/peak/allocated pagefile usage (informational within the memory check; the pagefile check uses its own source).

**Pagefile configuration (`WmiPagefileSource` + pure `PagefileConfigParser`):**
- `Win32_PageFileSetting` is unreliable/empty in practice, so the config is read read-only from `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PagingFiles`. A bare path or `path 0 0` means system-managed; whitespace-only/empty means no pagefile. Nothing is ever written.

**Storage sources (`WmiStorageInfoSource`):**
- `Win32_LogicalDisk`: fixed volumes, capacity/free space, filesystem, dirty bit (`GetBool("VolumeDirty")`).
- `Win32_DiskDrive`: model, size, interface; matched to `MSFT_PhysicalDisk` (scope `root\microsoft\windows\storage`) by DeviceId (a string, unlike Win32's UInt32 Index) with a model/size fallback.
- `MSFT_PhysicalDisk`: HealthStatus (0 Healthy, 1 Warning, 2/3 Unhealthy, else Unknown), MediaType (HDD/SSD/SCM/Unknown).
- `MSFT_StorageReliabilityCounter`: wear %, temperature, uncorrected/corrected read+write errors. Often returns 0 rows on NVMe — when unavailable, the check explicitly reports "not independently verified" and never claims the drive is healthy on missing data.
- Latency: passive sample of `Win32_PerfRawData_PerfDisk_PhysicalDisk` deltas over a ~700ms window (100ns counter units ×1e-7 = seconds). Idle disks are reported as idle, never judged; an active disk is judged only against latency thresholds.

**Thresholds (`MemoryOptions`/`StorageOptions`):**
- Memory: commit ratio ≥ 70% Suspicious / ≥ 85% Warning; available < 15% Suspicious / < 5% Warning of installed, or < 1.5 GB absolute; Pages/sec ≥ 200 flagged as heavy paging but never judged alone. Available memory includes reclaimable standby cache, so low available memory is pressure (a symptom), not a diagnosis.
- Pagefile: system-managed → Healthy (never flagged for high peak — Windows grows it); disabled → Suspicious, never Critical; fixed-size with current ≥ 95% of allocated or peak ≥ 90% → Suspicious (it cannot grow). Never recommends disabling the pagefile.
- Storage: free space < 15% Suspicious / < 5% Warning; dirty volume bit → Warning; stack health Warning → Suspicious, Unhealthy → Warning, Unknown → flag only; wear ≥ 90% Warning, temperature ≥ 70°C Suspicious, uncorrected errors > 0 Warning; active latency ≥ 30ms Suspicious / ≥ 100ms Warning. Disk event-log categories (Disk/Ntfs/StorageController) are analyzed by the Phase 7 event engine; final severity = max(storage verdict, event-log severity).

**Guardrails:** all three checks are read-only; no destructive tests, no full-drive benchmarks, no repairs applied. Findings describe observed state, never a root cause. Check durations: memory 45s, pagefile default, storage 60s CLI timeouts.

**Tests:** +67 new (Memory classifier/parser/checks + Storage classifier/checks, reusing `FakeEventLogSource`/`Ev.New` and new `FakeMemorySnapshotSource`/`FakePagefileSource`/`FakeStorageInfoSource`) — 476 total.

**Real-machine smoke test:** `check memory` → Suspicious (commit 13.2/16.7 GB = 79%); `check pagefile` → Healthy (system-managed, `?:\pagefile.sys`, 85 MB of 1.0 GB); `check storage` → Healthy (C: 161/237 GB free, KIOXIA NVMe stack health Healthy, SMART/NVMe reliability unavailable → reported "not independently verified", latency idle during sample).

---

### Phase 10 — Automatic Fixes (opt-in, user-confirmed) ✅ Complete

**Objective:** make findings actionable by applying real, reversible fixes directly from the TUI. Nothing is ever applied automatically — every fix requires explicit user confirmation. The reusable fix engine means any diagnostic can offer its own remediation.

**Fix engine (`src/PCDiag/Fixes/`):**
- `DiagnosticFix` (abstract): Id, Title, Problem, Effect, Risk (Low/Medium/High), RequiresAdmin, `ApplyAsync`, optional targeted `VerifyAsync`.
- `FixExecutor`: orchestrates apply-then-verify. Guards admin-required fixes (never attempted when not elevated), applies the fix, then verifies either via a fix-specific check or by re-running the owning diagnostic. Resolved = recheck countable and (Passed or severity dropped below the original finding).
- `FixModels.cs`: `FixRisk`, `FixApplyOutcome` (Applied/Failed/NotApplicable), `FixApplyResult`, `FixExecutionResult`.
- `IFixableCheck` (in `Core/`): a check that implements this interface and returns fixes via `GetFixes(DiagnosticResult)`.

**Real fixes implemented (all with injectable command runners for tests):**
| Fix | Command | Risk | Admin | Wired to |
|---|---|---|---|---|
| `DnsCacheFlushFix` | `ipconfig /flushdns` | Low | no | DNS Resolution |
| `WinsockResetFix` | `netsh winsock reset` | Medium | yes | Gateway, Packet Loss, TCP |
| `TcpIpStackResetFix` | `netsh int ip reset` | High (reboot) | yes | Gateway, Packet Loss (unreachable) |
| `RestartNetworkAdapterFix` | PowerShell `Restart-NetAdapter` | Medium | yes | Gateway, Packet Loss, TCP |
| `DhcpRenewFix` | `ipconfig /release` + `/renew` | Medium | yes | Gateway, Packet Loss (unreachable) |
| `AutotuningRestoreFix` | `netsh interface tcp set global autotuninglevel=normal` | Medium | yes | TCP (auto-tuning non-default) |

**Fixable checks:** `DnsDiagnosticsCheck`, `GatewayCheck`, `PacketLossCheck`, `TcpHealthCheck` now implement `IFixableCheck`. Fix selection is evidence-driven (adapter name and auto-tuning level are read from the result's evidence via `NetworkFixHelpers`), so fixes are only offered when the relevant condition was actually detected.

**TUI integration:**
- Results table gains a `FIX` column rendering a `[ FIX ]` button on each fixable finding row (`ResultsTableBuilder.Build(summary, isFixable)`).
- The "What next?" menu lists **per-finding fix buttons** (`[ FIX ] <name>`) plus **Fix all problems (N)** when any finding is fixable.
- `ShowFixFlow`: one summary panel listing every chosen fix (problem / fix / effect / severity / risk) with a single **Apply / Cancel** confirmation, then applies each fix in sequence with live `✓ FIX APPLIED` / `✕ FIX NOT APPLIED` outcomes. The natural scan loop re-runs afterward to show refreshed results.
- The per-check details view also offers the applicable fix inline.

**Guardrails:** no automatic fixes; one confirmation covers the whole batch; every fix states its effect and what it does *not* change; admin-required fixes are surfaced as "run as administrator" instead of being attempted; fixes only ever touch the described state (registry, Winsock catalog, TCP/IP stack, adapter, DHCP lease, DNS cache).

**Tests:** +26 new (per-fix success/failure/admin unit tests, check-fix wiring tests, interactive fix-flow tests incl. confirm / cancel / failed / per-finding button / no-fixable-findings) — 522 total.

---

## 9. Testing Strategy

| Layer | Tool | Coverage Target |
|-------|------|-----------------|
| Unit (models) | xUnit | 100% — all properties, defaults, edge cases |
| Unit (engine) | xUnit + Mock checks | Core paths: run all, run category, run single, error handling |
| Unit (reporting) | xUnit | Risk score formula, formatting, edge cases |
| Unit (inventory) | xUnit | Pure logic: VmDetector heuristics, edition extraction, renderer output/placeholders |
| Integration (inventory) | xUnit | Provider invariants against current machine: OS arch/64-bit, processor count, adapter statuses, uptime; degrade to null/empty, never throw |
| Unit (DNS) | xUnit | DNS wire parsing, stats math, classification, recommendation logic — no network |
| Check (DNS) | xUnit + fakes | End-to-end check against `FakeDnsTransport`/`FakeDnsServerSource`: healthy/slow/unreliable/unreachable/no-config scenarios; real DNS probing is verified by manual smoke test |
| Unit (Net) | xUnit | Stats math, IPStatus mapping, path-MTU binary search, gateway/packet-loss/MTU classifiers, MTU source lookup — no network |
| Check (Net) | xUnit + fakes | End-to-end MTU/gateway/packet-loss checks against `FakePingProbe`/`FakeMtuSource`/`PathSimulator`: healthy, mismatch, black hole, unreachable, unavailable, cancellation, configurable targets; real probing is verified by manual smoke test |
| Integration (checks) | xUnit | Each real check against current machine (may need `[Trait("Integration")]`) |
| Interactive UI | xUnit + `TestConsole` | End-to-end flow: ENTER starts scan, ESC exits, results render, System info menu, redirected-stdin auto-start |

---

## 10. Open Questions (For Future Phases)

1. Should fix suggestions be exposed in non-interactive/CLI output (e.g., `pcdiag check dns --fix`)?
2. Should we support plugin checks (external DLLs implementing `IDiagnosticCheck`)?
3. Should we support PowerShell Core on Windows (cross-platform potential)?
4. Should checks have configurable thresholds (e.g., "warn if uptime > 30 days" adjustable)?
5. Should the interactive UI let users pick a scan depth (Quick/Standard/Deep) before starting?

---

*This document is the authoritative source of truth for the PCDiag architecture. Update it as decisions evolve.*
