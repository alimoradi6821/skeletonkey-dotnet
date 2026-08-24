# Getting Started

## Requirements

- Windows x64 for the verified 0.1.0 production support contract.
- .NET 10 SDK for source builds.
- Playwright Chromium installed before browser workflows are executed.
- An interactive Windows session for FlaUI desktop automation.

## Build

```powershell
dotnet build .\SkeletonKey.sln -c Release
```

## Test

```powershell
dotnet test .\SkeletonKey.sln -c Release
```

## Install the browser runtime

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\install-playwright-browsers.ps1 chromium
```

## Validate a workflow

From source:

```powershell
dotnet run --project .\src\SkeletonKey.Runner\SkeletonKey.Runner.csproj -c Release -- validate --file .\examples\minimal.workflow.json
```

From a published runner:

```powershell
.\artifacts\runner\win-x64-self-contained\skeletonkey.exe validate --file .\examples\minimal.workflow.json
```

## Analyze and plan

```powershell
.\artifacts\runner\win-x64-self-contained\skeletonkey.exe analyze --file .\examples\minimal.workflow.json
.\artifacts\runner\win-x64-self-contained\skeletonkey.exe plan --file .\examples\minimal.workflow.json
```

## Run

```powershell
.\artifacts\runner\win-x64-self-contained\skeletonkey.exe run --file .\examples\minimal.workflow.json
```

Inputs can be supplied inline or from a JSON file:

```powershell
.\artifacts\runner\win-x64-self-contained\skeletonkey.exe run `
  --file .\path\scenario.workflow.json `
  --inputs-file .\path\inputs.json
```

## Resume a durable execution

A workflow must have been started with an execution ID and checkpoint directory before it can be resumed:

```powershell
.\artifacts\runner\win-x64-self-contained\skeletonkey.exe resume `
  --file .\path\scenario.workflow.json `
  --execution-id example-001 `
  --checkpoint-directory .\state\checkpoints
```

Interrupted in-flight handlers are not silently replayed. Resume fails closed when the saved execution state is not safe to continue.

## Runner commands

The 0.1.0 runner exposes:

```text
version
plugins
validate
analyze
plan
run
resume
install-browsers
```

See the generated API Reference and the versioned specifications for detailed host and runtime contracts.
