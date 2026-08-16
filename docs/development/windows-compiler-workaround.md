# Windows compiler workaround

This repository currently includes `build/csc.cmd` because the local .NET 10 SDK installation can restore and run the managed compiler through `dotnet csc.dll`, but the bundled Windows compiler host `csc.exe` fails before compilation with:

```text
Fatal error.
Your Windows doesn't fully support CET. Please install all available Windows updates.
```

`Directory.Build.props` points MSBuild at this wrapper on Windows so builds use the same Roslyn compiler through the `dotnet` host instead of launching `csc.exe` directly.

This is an environment compatibility workaround. It is not part of SkeletonKey's product architecture, workflow model, runtime design, or dependency structure.

Reconsider removing this wrapper when the underlying SDK/compiler-host issue is no longer present on the supported development machines.
