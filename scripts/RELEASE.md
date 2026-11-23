# Releasing

Drop-in doc. Copy to the repo root alongside `scripts/release.ps1` and
`scripts/release.settings.psd1`, then delete
the lines that do not apply. The flow below is identical in every repo
regardless of language.

## Once per machine

```powershell
gh auth login
```

## Cutting a release

```powershell
# As you work: add notes under "## Unreleased" in CHANGELOG.md.

.\scripts\release.ps1 -Version 1.2.0 -Apply     # 1. write the version, roll the CHANGELOG
git commit -am "Release 1.2.0."                     # 2. commit and push
git push origin HEAD
.\scripts\release.ps1 -Publish                   # 3. rehearse
.\scripts\release.ps1 -Publish -Apply            # 4. do it
```

`-Version` writes and `-Publish` reads, never in the same run. `-Version` prints
the next commands, so there is nothing to remember past step 1.

To use a release PR, run steps 1 and 2 on a branch, merge it, then run steps 3
and 4 on `main`. The script needs no change for this.

Step 3 is not optional theatre. It runs every check the real thing runs -
authentication, branch, remote sync, tag availability, payload completeness -
and reports *all* the failures rather than stopping at the first, so one
rehearsal tells you everything standing between you and a release.

**Nothing is written, tagged or pushed without `-Apply`.** That is the whole
safety model: any command you can type is harmless until you add one switch.

## The nine steps

Every `release.ps1` runs these in this order:

| # | Step | What it does |
|---|------|--------------|
| 1 | Version | Reads the version from the project's own manifest |
| 2 | Preflight | `gh` auth, correct branch, level with `origin`, tag free, payload resolves, CHANGELOG matches the version |
| 3 | Tests | The project's test suite. `-SkipTests` to skip, which you should not |
| 4 | Build | The one genuinely language-specific step |
| 5 | Stage | Build output plus `ExtraPayload` into a temp tree (zip mode only) |
| 6 | Package | `release/<name>-<version>.zip` plus a `.sha256` sidecar. In artifacts mode (Electron), the build's own installers are uploaded instead |
| 7 | Tag | Annotated `v<version>`, clean tree required |
| 8 | Publish | `git push origin <tag>`, then `gh release create` with every asset and the CHANGELOG section as notes |
| 9 | Done | Path, size, hash, and what to do next |

`-Version` is not one of the nine. It writes, prints the next commands and stops.

## Doing less than a full release

| Command | Stops after |
|---|---|
| `.\scripts\release.ps1 -Apply` | Step 6. An artifact on disk, nothing touched remotely |
| `.\scripts\release.ps1 -Tag -Apply` | Step 7. Tagged locally, not pushed |
| `.\scripts\release.ps1 -Publish -Apply` | Step 8. Live on GitHub |

Add `-Ship` to any of these to also copy the files to
`NasRoot\<name>\<version>\`. `-Ship -Apply` alone copies a build that already
exists for this version, and builds only if there is none.

## When it refuses

| Message | Fix |
|---|---|
| `-Publish needs an authenticated gh` | `gh auth login` |
| `On branch 'x', but releases are cut from 'main'` | `git switch main`, or pass `-ReleaseBranch x` |
| `N commit(s) not pushed` | `git push origin main`. The release is cut from the tagged commit, not your working tree |
| `N commit(s) behind origin/main` | `git pull`. You would otherwise ship stale code |
| `HEAD and origin/main have diverged` | Reconcile before releasing. There is no safe automatic answer |
| `-Tag requires a clean working tree` | Commit or stash. A tag has to mean something |
| `Tag v1.2.0 already exists locally and on the remote` | Pick the next version, or delete both (see below) |
| `Tag v1.2.0 exists locally but not on the remote` | You deleted the release on GitHub. `git tag -d v1.2.0` |
| `<name>-1.2.0.zip already exists` | Bump the version. Two artifacts with one name and different hashes is the exact thing a version number exists to prevent. `-Force` overrides |
| `Required path is missing: X` | An `ExtraPayload` entry does not exist (zip mode only). Fix the path in `release.settings.psd1` |
| `-Version cannot be combined with -Tag or -Publish` | Run `-Version` alone, commit and push, then `-Publish` |
| `CHANGELOG.md newest version is X, but <manifest> says Y` | Update one so they match |
| `CHANGELOG.md has no version section` | `.\scripts\release.ps1 -Version <version> -Apply` |
| `The 1.2.0 section in CHANGELOG.md is empty` | Add the release notes |
| `The Unreleased section in CHANGELOG.md is empty` | Add the release notes before `-Version` |
| `CHANGELOG.md is missing` | Create it with a `## Unreleased` section, or set `ChangelogFile = ''` |
| `-Ship needs a NAS share` | Set `NasRoot` in `release.settings.psd1`, for example `'\\nas\releases'` |
| `Cannot reach \\nas\releases` | The NAS is off, or this account cannot open the share. Use the UNC path, not a mapped drive |
| `...\1.2.0 already exists, so 1.2.0 is already on the NAS` | Bump the version. `-Force` replaces the NAS folder |
| `The copy of X on the NAS does not match its .sha256` | The network copy is damaged. Run again; nothing was left in the version folder |
| `A complete, checked copy is in ...partial-xxxx` | The copy is good but could not be renamed. Rename the folder to the version, or delete it and run again |

## Undoing a release

```powershell
gh release delete v1.2.0 --cleanup-tag --yes   # release and remote tag
git tag -d v1.2.0                              # local tag
```

Both halves matter. `--cleanup-tag` removes the remote tag but leaves your
local copy, which then blocks the next attempt at the same number.

## If it dies halfway

The one non-atomic moment is between pushing the tag and creating the release.
If `gh release create` fails there, the script prints the exact command to
finish by hand - the artifact is already built and the tag is already pushed,
so nothing needs redoing:

```powershell
gh release create v1.2.0 release\thing-1.2.0.zip release\thing-1.2.0.zip.sha256 --title v1.2.0 --notes-file <path it prints>
```

The notes file stays in your temp directory until a publish succeeds.

## Verifying a published release

```powershell
gh release download v1.2.0 --dir .\verify
cd .\verify
(Get-FileHash thing-1.2.0.zip -Algorithm SHA256).Hash.ToLower()
Get-Content thing-1.2.0.zip.sha256
```

The two strings should match.
