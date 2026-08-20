# Phase 9 — Memory & Storage Diagnostics

## Status
Approved by user (proceed with defaults).

## Objective
Add read-only memory, pagefile, and storage diagnostics with three CLI commands:
`pcdiag check memory`, `pcdiag check pagefile`, `pcdiag check storage`.
No destructive tests, no full-drive benchmarks, and SSD health is never claimed
"perfect" when SMART/NVMe data is unavailable.

## New checks
| Check ID | Name | CLI | Category |
|---|---|---|---|
| PERF-MEM-001 | Memory Usage & Pressure | `check memory` | Performance |
| PERF-PAG-001 | Pagefile Configuration | `check pagefile` | Performance |
| PERF-DISK-001 | Storage & Disk Health | `check storage` | Performance |

## Files to create
- `src/PCDiag/Memory/MemorySnapshot.cs` — snapshot record + per-source availability flags
- `src/PCDiag/Memory/IMemorySnapshotSource.cs` / `WmiMemorySnapshotSource.cs`
- `src/PCDiag/Memory/PagefileInfo.cs` — PagefileEntry, PagefileConfig, PagefileInfo
- `src/PCDiag/Memory/IPagefileSource.cs` / `WmiPagefileSource.cs`
- `src/PCDiag/Memory/PagefileConfigParser.cs` — pure registry-string parser
- `src/PCDiag/Memory/MemoryOptions.cs` — shared thresholds
- `src/PCDiag/Memory/MemoryClassifier.cs` / `PagefileClassifier.cs` — pure, flag-based
- `src/PCDiag/Storage/StorageSnapshot.cs` — StorageVolume, PhysicalDiskInfo, StorageHealth, DiskLatencySample
- `src/PCDiag/Storage/IStorageInfoSource.cs` / `WmiStorageInfoSource.cs`
- `src/PCDiag/Storage/StorageOptions.cs`
- `src/PCDiag/Storage/StorageClassifier.cs` — pure, flag-based
- `src/PCDiag/Checks/Performance/MemoryCheck.cs`, `PagefileCheck.cs`, `StorageCheck.cs`
- `src/PCDiag/CLI/MemoryCommand.cs`, `PagefileCommand.cs`, `StorageCommand.cs` (mirror EventsCommand)
- `tests/PCDiag.Tests/Memory/*` + `tests/PCDiag.Tests/Storage/*` (~50 tests)
- Edit: `src/PCDiag/Program.cs` (3 cases + unknown list), `src/PCDiag/Infrastructure/CheckRegistry.cs` (10 -> 13)
- Edit: `SPEC.md` (header -> Phase 9 Complete, new Phase 9 section)

## Verified data sources (live machine)
- `Win32_OperatingSystem`: TotalVisibleMemorySize/FreePhysicalMemory/TotalVirtualMemorySize/FreeVirtualMemory/SizeStoredInPagingFiles (KB)
- `Win32_PerfFormattedData_PerfOS_Memory`: CommittedBytes, CommitLimit, AvailableMBytes, PagesPerSec, CacheBytes, PoolNonpagedBytes, PoolPagedBytes
- `Win32_PageFileUsage`: C:\pagefile.sys 1024MB allocated / 85MB current / 93MB peak
- Registry (read-only) `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PagingFiles` = `?:\pagefile.sys` -> system-managed (`Win32_PageFileSetting` is empty on this machine)
- `Win32_LogicalDisk` (DriveType 3): C: 237GB, 161GB free, NTFS, not dirty
- `Win32_DiskDrive`: KIOXIA NVMe 256GB
- `root\microsoft\windows\storage\MSFT_PhysicalDisk`: HealthStatus=0 Healthy, MediaType unknown, OperationalStatus OK
- `MSFT_StorageReliabilityCounter`: 0 rows on this machine -> report "not independently verified"
- `Win32_PerfRawData_PerfDisk_PhysicalDisk`: "0 C:"/_Total, AvgDisksecPerRead+_Base -> safe passive delta sampling
- Event log disk errors: reuse `EventLogAnalyzer` with FocusCategories {Disk, Ntfs, StorageController}

## Thresholds (in MemoryOptions / StorageOptions)
Memory:
- Commit ratio >= 0.70 Suspicious, >= 0.85 Warning (commit limit = RAM + pagefile)
- Available % < 0.15 Suspicious, < 0.05 Warning (Available includes reclaimable standby)
- Absolute floor < 1536 MB -> flag (secondary, never sole driver)
- Pages/sec >= 200 -> HeavyPaging flag (evidence only, never sole driver)

Pagefile:
- System-managed -> Healthy (info row)
- Disabled -> Suspicious (never Critical; deliberate choice)
- Fixed-size: current >= 95% of allocated or peak >= 90% -> Suspicious
- Never recommend disabling the pagefile

Storage:
- Free space < 15% Suspicious, < 5% Warning; VolumeDirty=true -> Warning
- Stack HealthStatus: Healthy -> report; Unknown -> "not verified" Info; Warning -> Suspicious; Unhealthy -> Warning
- Reliability counters present: Wear >= 90% -> Warning; temp > 70C -> Suspicious; uncorrected errors > 0 -> Warning
- Reliability unavailable -> explicit "SMART/NVMe data not available; not independently verified"
- Latency (passive ~1.4s raw-counter delta): idle -> "not meaningful"; active avg >= 30ms Suspicious, >= 100ms Warning
- Event-log disk errors: existing Phase 7 severity/patterns contribute to final verdict

## Guardrails
- Read-only (registry opened read-only); no destructive tests; no full-drive benchmark (explicit limitation)
- Distinguish unavailable from healthy on every source
- Register all 3 checks in CheckRegistry (TUI scan visibility)
- No causality claims; recommendations are evidence-gated and never auto-applied

## Tests (~50 new)
- MemoryClassifierTests, PagefileConfigParserTests, PagefileClassifierTests
- MemoryCheckTests, PagefileCheckTests
- StorageClassifierTests, StorageCheckTests (reuse Events' FakeEventLogSource/Ev.New)
- Fakes: FakeMemorySnapshotSource, FakePagefileSource, FakeStorageInfoSource

## Verification
- dotnet test (all green), smoke test the 3 commands, then commit "Phase 9 — Memory & Storage Diagnostics" and push.