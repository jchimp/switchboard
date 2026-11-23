# Building

<!-- TEMPLATE: replace the TODO markers, delete what does not apply. -->

## Prerequisites

| | |
|---|---|
| .NET SDK | TODO: e.g. 10.0. Check with `dotnet --version` |

The **SDK**, not the runtime. `dotnet --version` reports the SDK and errors out
if only a runtime is installed; `dotnet --list-runtimes` will happily list
several while `dotnet build` still refuses to run. If the SDK for the pinned
`TargetFramework` is not installed, the failure is a restore error naming a
framework rather than anything about a missing SDK.

TODO: if `global.json` pins an SDK version, say so - it turns "wrong SDK" from
a confusing build error into a clear one.

## Dev loop

```powershell
dotnet build
dotnet test
dotnet run --project src\MyApp        # TODO: real project path
```

TODO: if startup time matters for this app, note that `dotnet run` adds build
and launch overhead and measure against `bin\Release\...\MyApp.exe` instead.

## Where the version lives

The `<Version>` element in the csproj. **It is the source of truth.** Bump it
and commit before releasing.

The release script patches that one element's text rather than loading and
re-saving the XML, which would reformat the whole csproj - self-closing tags,
attribute order, the XML declaration - and bury a one-line bump in noise.

`-p:Version=` is also passed on the publish command line, so the assembly always
carries the version the release is named after.

## Releasing

See `RELEASE.md`. Short version:

```powershell
.\scripts\release.ps1 -Version 1.2.0 -Apply     # write the version, roll the CHANGELOG
git commit -am "Release 1.2.0."
git push origin HEAD
.\scripts\release.ps1 -Publish                   # rehearse
.\scripts\release.ps1 -Publish -Apply            # do it
```

Release publishes are **self-contained** even though the csproj is
framework-dependent. That is deliberate: `SelfContained` in the csproj would
make every `dotnet build` copy the whole runtime into `bin/` and destroy the
fast dev loop, so the release path sets it on the command line instead.

The cost is size - expect 70-90 MB against about 5 MB framework-dependent, and
roughly half that after compression. The benefit is that the zip runs on a
machine with no .NET installed, which is the whole point of shipping it to
anyone but yourself. Set `SelfContained = $false` if every target machine is
known to have the right runtime.

`BuildDir` is wiped before each publish. `dotnet publish` merges into an
existing output directory, so a file you renamed or deleted would otherwise
survive into the zip.

## Publish settings, and the ones left out

Set in `PublishProps`:

- **`PublishSingleFile`** - one exe to ship and one to run.
- **`DebugType=embedded`** - symbols inside the assembly. Without it a loose
  `.pdb` ships in the zip; with `DebugType=none` a crash report comes back with
  no line numbers at all.

Not set, on purpose - TODO: keep the ones that apply, delete the rest:

- **`PublishTrimmed`** - anything resolving types reflectively (JSON
  polymorphism, DI by convention, plugin loading, TextMate grammars) is
  invisible to the trimmer, which strips it. The failure surfaces much later as
  a null at runtime, not as a build error.
- **`EnableCompressionInSingleFile`** - roughly halves the zip, but it
  decompresses on every launch. TODO: state this app's startup budget.
- **`PublishAot`** - TODO: decide and record why.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Restore error naming a framework | SDK for that `TargetFramework` not installed | Install the matching SDK, not just the runtime |
| `dotnet publish produced no .exe` | Project is a library, or `OutputType` is wrong | Point `VersionFile` at the executable project |
| Zip is 80 MB and you expected 5 | Self-contained, as designed | Set `SelfContained = $false` if the runtime is guaranteed |
| App starts slowly after a change | `EnableCompressionInSingleFile` was added | Remove it, or accept the cost knowingly |
| Null reference only in the released build | `PublishTrimmed` stripped a reflective type | Remove trimming, or add a trimming descriptor |
| A loose `.pdb` in the zip | `DebugType=embedded` was removed | Put it back |
| Packaging retries then fails | Antivirus or Dropbox holding the exe open | Exclude the output directory from indexing |
