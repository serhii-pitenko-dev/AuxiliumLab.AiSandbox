# AuxiliumLab.AiSandbox.Statistics (AuxiliumLab.Statistics)

**Onion layer: Infrastructure**  
Persistence and reporting layer for simulation run data.  
Depends on: `SharedBaseTypes`, `AuxiliumLab.AiSandbox.Common`.

> **Folder:** `AuxiliumLab.Statistics/` (project name: `AuxiliumLab.AiSandbox.Statistics`)

## Purpose
Records outcomes from batch simulation runs, exports them to CSV, and provides the data structures used by `MassRunner` to summarise and compare runs across different configurations.

## Folder Structure
```
AuxiliumLab.Statistics/
├── Converters/         TableConverter — converts summary objects to CSV rows
├── Preconditions/      Settings objects for incremental sweep configuration
├── Result/             Domain result DTOs (shared with ApplicationServices.Domain.Statistics)
└── StatisticDataManager/ IStatisticFileDataManager, StatisticFileDataManager
```

## Key Types

### Result DTOs (`Result/`)

| Class | Description |
|---|---|
| `ParticularRun` | Outcome of a single simulation: playground ID, turns, enemy count, win/loss reason |
| `BatchSummary` | Aggregated stats for a batch: total runs, wins, average turns, batch ID |
| `IncrementalRunSummary` | Summary for one step of an incremental sweep: property name, step value, nested `BatchSummary` list |
| `MassRunSummary` | Top-level summary of a full `MassRunner` execution: batch count, elapsed time, swept properties |
| `GeneralBatchRunInformation` | Metadata about the batch: timestamp, configuration snapshot, map settings |
| `SandboxRunResult` | Low-level per-run data used before aggregation |

### `IStatisticFileDataManager` / `StatisticFileDataManager`
Specialised file manager for statistics output.

| Method | Description |
|---|---|
| `SaveBatchInfoAsync(info)` | Writes `GeneralBatchRunInformation` JSON to disk |
| `AppendRunToCsvAsync(fileName, run)` | Appends a single `ParticularRun` row to a CSV file |
| `AppendBatchSummaryToCsvAsync(fileName, summary)` | Appends a `BatchSummary` aggregate row to CSV |
| `AppendIncrementalSummaryAsync(fileName, summary)` | Appends all rows of an incremental sweep result |

CSV files are named with the `batchRunId` GUID so multiple batch runs never overwrite each other.

### `TableConverter`
Maps result objects to ordered string arrays (CSV rows).  
Used exclusively by `StatisticFileDataManager`.

### Preconditions (`Preconditions/`)

| Class | Description |
|---|---|
| `SimulationStartupSettings` | Top-level sweep settings: which properties to sweep and how many simulations per step |
| `SimulationIncrementalPropertiesSettings` | List of `RangeSettings` for each property to sweep |
| `RangeSettings` | `PropertyName`, `Min`, `Max`, `Step` |
| `SimulationSandBoxSettings` | Fork of `SandBoxConfiguration` used during incremental runs to override individual values |

## Output Files
`MassRunner` writes its output to the path configured in `InfrastructureSettings.FilesPath`:
```
<FilesPath>/
└── <batchRunId>.csv          ← combined CSV: standard batch + all incremental sweeps
    <batchRunId>.json         ← GeneralBatchRunInformation metadata
```

## Adding a New Statistic Column
1. Add the property to the relevant Result DTO (`ParticularRun`, `BatchSummary`, etc.).
2. Update `TableConverter` to include the new value in the row array.
3. Update `MassRunner` to populate the new property when constructing the DTO.

## Adding a New Incremental Property
1. Add the property name constant to `IncrementalPropertyNames` (in `ApplicationServices/Runner/MassRunner/`).
2. Update `MassRunner.RunIncrementalSweepPhaseAsync` to handle the new property name by overriding the appropriate value in the cloned `SandBoxConfiguration`.
