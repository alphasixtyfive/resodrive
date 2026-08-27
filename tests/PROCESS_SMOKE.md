# Process smoke coverage

`process-smoke.ps1` stages and launches only a supplied build output. It assigns
launched processes and their children to a kill-on-close Windows job and uses a
unique `RDRIVE_DATA_DIR`, so it neither reads user state nor leaves a host or tray
process running after the test.

The process layer covers behavior that is deterministic without user interaction:

- simultaneous cold background launches elect one primary instance;
- a tray/background primary acknowledges `--show` only after its window is ready;
- supported host shutdown is followed by automatic host recovery;
- the owned process tree can be terminated and successfully relaunched.

Unsafe or nondeterministic operating-system boundaries stay in injectable tests:

| Scenario | Deterministic coverage |
| --- | --- |
| UAC cancellation | `ApplicationUpdateHandoffTests.CompleteAsync_RecordsUacCancellationAndRestoresReadyApplication` |
| MSI failure | `ApplicationUpdateHandoffTests.CompleteAsync_RecordsInstallerFailureAndRelaunchesApplication` |
| Slow/offline network | `RcloneUpdateServiceTests.CheckAsync_ReturnsTransientFailureOnTimeout` and `CheckAsync_ReturnsTransientFailureWhenOffline` |
| Corrupt settings | `AtomicSettingsStoreTests.LoadAsync_FallsBackToSemanticallyValidBackup` |

Run locally after publishing a framework-dependent smoke output. This deliberately
includes the runtime so the harness never opens a .NET installation prompt:

```powershell
dotnet publish src/ResoDrive.App/ResoDrive.App.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=false -o artifacts/process-smoke
./tests/process-smoke.ps1 -AppPath artifacts/process-smoke/resodrive.exe
```
