# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

FsCatBoost is a thin F# P/Invoke wrapper around CatBoost's **model applier C API** (`catboost/libs/model_interface/c_api.h`) — loading `.cbm` models and running predictions. There is no training: CatBoost's training C API (`train_interface/catboost_api.h`) isn't shipped in CatBoost releases, so it was deliberately removed. `Parameters.fs` (training parameter DUs) is kept, unused by the bindings.

The bundled native libraries are CatBoost 1.2.10 (`CATBOOST_APPLIER_MAJOR/MINOR/FIX` in `Interop.fs`). The upstream C API reference for that version, pinned to the release tag:

- [`c_api.h`](https://github.com/catboost/catboost/blob/v1.2.10/catboost/libs/model_interface/c_api.h) — declarations and doc comments
- [`c_api.cpp`](https://github.com/catboost/catboost/blob/v1.2.10/catboost/libs/model_interface/c_api.cpp) — implementation (ownership/`free` semantics)
- [`calcer.exports`](https://github.com/catboost/catboost/blob/v1.2.10/catboost/libs/model_interface/calcer.exports) — symbols exported by `catboostmodel`

When upgrading, diff these between the old and new tags rather than against `master`.

## Commands

Solution file is `src/FsCatBoost.slnx`; both projects target `net10.0`. `src/global.json` opts `dotnet test` into Microsoft.Testing.Platform (required for xunit.v3 on the .NET 10 SDK), so use the MTP-style arguments:

```powershell
cd src
dotnet build FsCatBoost.slnx
dotnet test --solution FsCatBoost.slnx
dotnet test --project FsCatBoost.Tests --filter-method "*iris*"   # single test, wildcard on member name
dotnet pack FsCatBoost\FsCatBoost.fsproj -o <dir>
```

The `.slnx` marks the test project `NoBuild` in the `Release` configuration, so run tests in Debug.

## Native library packaging

- `src/FsCatBoost/runtimes/<rid>/native/` holds the release binaries for `win-x64`, `linux-x64`, `linux-arm64` and `osx` (universal2 dylib). The folder is **gitignored**: the `DownloadCatBoostNative` target in `FsCatBoost.fsproj` downloads each `CatBoostNative` item from the CatBoost GitHub release before build and fails on a SHA-256 mismatch. A `<file>.sha256` stamp next to each verified file lets later builds skip re-hashing. The first build therefore needs network access.
- They're packed to the same path in the NuGet package, so consumers resolve the right one through `deps.json`.
- `DllImport` uses the extension-less name `"catboostmodel"` so the runtime probes `catboostmodel.dll` / `libcatboostmodel.so` / `libcatboostmodel.dylib`.
- Project references don't get `deps.json` entries for `runtimes/`, so `FsCatBoost.fsproj` also copies the current platform's library (`CatBoostNativeRid`: `$(RuntimeIdentifier)` or the SDK's RID, `osx-*` → `osx`) into the output root. F# has no module initializer, which is why this is done in MSBuild rather than with `NativeLibrary.SetDllImportResolver`.
- To upgrade CatBoost: set `CatBoostVersion` and the four `Sha256` values in `FsCatBoost.fsproj` (the next build re-downloads), update the `CATBOOST_APPLIER_*` literals and the tag in the links above, and diff `c_api.h` between the two tags for added/changed functions.

## Architecture

Compilation order in `FsCatBoost.fsproj` is `Interop.fs` → `Api.fs` → `Parameters.fs`.

- **`Interop.fs`**
  - `module Handle`: handle type aliases (`nativeint`), `SafeHandle` subclasses (`DataWrapperSafeHandle`, `ModelCalcerSafeHandle`) whose `ReleaseHandle` calls the native delete function, and `free` (via `NativeMemory.Free`) for the `malloc`'d buffers some C API functions return.
  - `module Interop`: raw `[<DllImport>]` `extern` declarations, each preceded by the original C signature as a comment. C `bool` returns must be declared `extern [<return: MarshalAs(UnmanagedType.U1)>] bool ...` — the .NET default marshals `bool` as a 4-byte Win32 `BOOL`.
- **`Api.fs` (`module Api`)**: the public F# surface. Each function wraps one C API call, carries the C signature in its XML doc `<code>` block, and follows a fixed pattern:
  - takes the safe handle as the **last** argument so calls pipe (`modelCalcerCreate () |> loadFullModelFromFile path`);
  - `DangerousAddRef` / `DangerousGetHandle` inside `try`, `DangerousRelease` + freeing pinned buffers in `finally`;
  - returns `Result<_, string>`; on native failure calls `showError "<fnName>"`, which reads `GetErrorString()` (UTF-8).
  - Feature matrices are `'a array2d` (float32 for numeric, string for categorical/text) marshalled to `T**` / `char***` by pinning with `GCHandle` (`marshalFeatures`, `marshalStringFeatures`). An empty array2d is passed as a null pointer.
- **`Parameters.fs`**: F# DU models of CatBoost training parameters/loss functions/metrics (`Re`, `Cl`, `MuCl`, `Ra`, … modules).

When adding a native binding: add the `extern` in `Interop.fs`, then the wrapper in `Api.fs` following the pattern above.

## Tests

xUnit v3 + Faqt assertions + FsToolkit `result { }` CE + Deedle for loading CSVs. Tests are ports of upstream suites:

- `CatBoostCatBoostNetTests.fs` — from CatBoost's .NET `CatBoostModelEvaluatorTest.cs`, using models/data in `testbed/`.
- `MireclCatBoostTests.fs` — from mirecl/catboost-cgo `catboost_test.go`, using models in `example/`.
- `CatBoostTutorialsTests.fs` — from CatBoost's Java tutorial (`catboost/tutorials`, `apply_model/java`), using `tutorials/adult/adult.cbm`. The tutorial only prints predictions, so expected raw values were recorded from CatBoost 1.2.10. `tutorials/titanic/` is untracked data with no tests yet.

Test data is resolved from `AppContext.BaseDirectory` (the `example/` and `testbed/` items are copied to output), with paths built by `Path.Combine` segments. Tests are `let`-bound `[<Fact>]` functions in F# modules (no test classes). They end a `result { }` block with `|> assertOk` (`TestHelpers.fs`), which fails the test with the error message; to log, use `writeOutput` (wraps xUnit v3's `TestContext.Current.TestOutputHelper`) — don't name it `log`, which would shadow F#'s `log` used by the boston test.
