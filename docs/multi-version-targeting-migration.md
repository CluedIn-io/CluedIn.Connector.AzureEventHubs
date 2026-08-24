# Migrating a Connector to Multi-Version Targeting

This document describes the changes made to migrate `CluedIn.Connector.AzureEventHubs` from a single-version build to the multi-version targeting pattern. It is intended as a guide for migrating other connector repos.

---

## Overview

The goal is to produce separate NuGet packages per CluedIn version (e.g. `CluedIn.Connector.AzureEventHub.460`, `.470`, `.480`, `.50`) from a single branch, using the shared `crawler.build.jobs.yml` pipeline template. Each package targets the correct .NET TFM for that CluedIn generation.

### Building locally

To build against a specific CluedIn version locally, pass both properties — mirroring exactly what the pipeline does:

```
dotnet build /p:_CluedIn=4.7.0 /p:CluedInMultiVersionTargetFramework=net6.0
dotnet build /p:_CluedIn=4.8.0 /p:CluedInMultiVersionTargetFramework=net6.0
dotnet build /p:_CluedIn=5.0.0-alpha.676 /p:CluedInMultiVersionTargetFramework=net10.0
```

Without these overrides the defaults in `Packages.props` and `Directory.Build.props` apply (5.0 / net10.0).

---

## Step 1 — Switch the pipeline template

**File: `azure-pipelines.yml`**

The single-version template (`crawler.build.yml`) is a steps-level template. The multi-version template (`crawler.build.jobs.yml`) is a jobs-level template and must be the only `jobs:` entry.

Replace:

```yaml
jobs:
  - template: /templates/crawler.build.yml@templates
    parameters:
      ...
```

With:

```yaml
jobs:
  - template: /templates/crawler.build.jobs.yml@templates
    parameters:
      pipelineTemplateRef: templates
      probeCluedInVersion: true
      multiVersionCluedInTargets:
        - 4.6.0
        - 4.7.0
        - 4.8.0
        - 5.0.0-alpha.*
      ...
```

> **Note:** Use `5.0.0-alpha.*` not `5.0.0-beta.*` — no beta feed exists yet. Check the CluedIn develop feed for the latest pre-release label.

The template injects two MSBuild properties per build leg:
- `_CluedIn=<version>` — the resolved CluedIn package version
- `CluedInMultiVersionTargetFramework=<tfm>` — the target framework (`net6.0`, `net8.0`, `net10.0`, etc.)

---

## Step 2 — Update `Directory.Build.props`

The pipeline sets `CluedInMultiVersionTargetFramework` from the outside. The project needs to honour it when set, and fall back to a sensible default for local development.

```xml
<PropertyGroup>
  <!-- Multi-version targeting: pipeline overrides this per build leg -->
  <TargetFramework Condition="'$(CluedInMultiVersionTargetFramework)' != ''">
    $(CluedInMultiVersionTargetFramework)
  </TargetFramework>
  <TargetFramework Condition="'$(CluedInMultiVersionTargetFramework)' == ''">net10.0</TargetFramework>
</PropertyGroup>
```

---

## Step 3 — Update `Packages.props`

### 3a — Make `_CluedIn` overridable

The pipeline sets `_CluedIn` as an MSBuild property. Add a guard so the pipeline value wins:

```xml
<PropertyGroup>
  <_CluedIn Condition="'$(_CluedIn)' == ''">5.0.0-alpha.*</_CluedIn>
</PropertyGroup>
```

### 3b — Derive `DefineConstants` from the CluedIn version

The `_CluedIn` value may contain a wildcard or pre-release suffix (e.g. `5.0.0-alpha.*`). Strip it to get a comparable version number, then set constants:

```xml
<PropertyGroup>
  <!-- Strip pre-release suffix for version comparison -->
  <_CluedInVersionOnly Condition="$(_CluedIn.IndexOf('-')) > 0">
    $(_CluedIn.Substring(0, $(_CluedIn.IndexOf('-'))))
  </_CluedInVersionOnly>
  <_CluedInVersionOnly Condition="'$(_CluedInVersionOnly)' == ''">$(_CluedIn)</_CluedInVersionOnly>

  <DefineConstants Condition="$([System.Version]::Parse('$(_CluedInVersionOnly)').CompareTo($([System.Version]::Parse('4.7.0')))) >= 0">
    $(DefineConstants);CLUEDIN_V47
  </DefineConstants>
  <DefineConstants Condition="$([System.Version]::Parse('$(_CluedInVersionOnly)').CompareTo($([System.Version]::Parse('4.8.0')))) >= 0">
    $(DefineConstants);CLUEDIN_V48
  </DefineConstants>
  <DefineConstants Condition="$([System.Version]::Parse('$(_CluedInVersionOnly)').CompareTo($([System.Version]::Parse('5.0.0')))) >= 0">
    $(DefineConstants);CLUEDIN_V50
  </DefineConstants>
</PropertyGroup>
```

These constants can then be used in source code with `#if CLUEDIN_V47` etc.

### 3c — Conditionally override package versions by TFM

Some packages have dropped support for older TFMs. Gate version overrides on the active TFM. For example, Entity Framework Core 8+ requires net8.0+:

```xml
<PackageVersion Update="Microsoft.EntityFrameworkCore" Version="6.0.16"
    Condition="'$(TargetFramework)' == 'net6.0'" />
<PackageVersion Update="Microsoft.EntityFrameworkCore" Version="10.0.7"
    Condition="'$(TargetFramework)' != 'net6.0'" />
```

---

## Step 4 — Fix the NuGet config

### 4a — Add the CluedIn public feed

CluedIn packages for 4.6–4.8 are **not** on nuget.org. They live in a public Azure Artifacts feed. Without it, restore fails with `NU1101: Unable to find package CluedIn.Core`.

Add to `NuGet.Config`:

```xml
<add key="public" value="https://pkgs.dev.azure.com/CluedIn-io/Public/_packaging/Public/nuget/v3/index.json" />
```

### 4b — Check the filename casing

On Linux CI agents the filesystem is case-sensitive. `dotnet restore` requires the file to be named exactly `NuGet.Config` or `nuget.config`. A filename like `Nuget.config` is silently ignored, causing only nuget.org to be searched.

Rename using git to preserve history:

```
git mv Nuget.config NuGet.Config
```

---

## Step 5 — Fix API compatibility breaks

When a connector spans multiple CluedIn major versions you will almost certainly hit breaking API changes. Wrap divergent call sites in `#if` guards using the constants from Step 3b.

### Example — CluedIn 4.7 broke several streaming APIs

| Method | Pre-4.7 | 4.7+ |
|---|---|---|
| `GetAllStreams` | `GetAllStreams()` — sync, no args | `await GetAllStreams(executionContext)` — async |
| `GetStreamMappings` | `GetStreamMappings(stream.Id)` | `GetStreamMappings(executionContext, stream.Id)` |
| `SetupConnector` | `SetupConnector(stream.Id, model, executionContext)` | `SetupConnector(executionContext, stream.Id, model)` |

Guard pattern:

```csharp
#if CLUEDIN_V47
    var streams = await _streamRepository.GetAllStreams(executionContext);
#else
    var streams = _streamRepository.GetAllStreams();
#endif
```

> **Tip:** Start by getting the latest CluedIn version building first. Then add older versions one at a time and add guards as compiler errors surface.

---

## Step 6 — Fix test package compatibility for older TFMs

Several popular test packages have dropped support for older .NET targets. You need to conditionally select versions per TFM.

### Known incompatibilities (as of mid-2026)

| Package | Problem | Solution |
|---|---|---|
| `xunit.v3 3.2.2` | Requires net8.0+ | Use `xunit 2.9.3` on net6.0 |
| `AutoFixture.Xunit3` (preview) | Requires net8.0+ | Use `AutoFixture.Xunit2 4.18.0` on net6.0 |
| `Microsoft.NET.Test.Sdk 18.x` | Dropped net6.0 | Use `17.12.0` on net6.0 |

### Approach

In the test `.csproj`, use conditional `ItemGroup`s gated on `$(DefineConstants)`:

```xml
<ItemGroup Condition="$(DefineConstants.Contains('CLUEDIN_V50'))">
  <PackageReference Include="xunit.v3" />
  <PackageReference Include="AutoFixture.Xunit3" />
</ItemGroup>

<ItemGroup Condition="!$(DefineConstants.Contains('CLUEDIN_V50'))">
  <PackageReference Include="xunit" />
  <PackageReference Include="AutoFixture.Xunit2" />
</ItemGroup>
```

In `Packages.props`, pin versions conditionally:

```xml
<PackageVersion Update="Microsoft.NET.Test.Sdk" Version="17.12.0"
    Condition="'$(TargetFramework)' == 'net6.0'" />
<PackageVersion Update="Microsoft.NET.Test.Sdk" Version="18.3.0"
    Condition="'$(TargetFramework)' != 'net6.0'" />
```

### Namespace differences between xunit v2 and v3

If test files use `using AutoFixture.Xunit3;` (or v2 equivalent) directly, those usings will break on the other TFM. Use a `GlobalUsings.cs` file in the test project with conditional compilation instead:

```csharp
// GlobalUsings.cs
#if CLUEDIN_V50
global using AutoFixture.Xunit3;
#else
global using AutoFixture.Xunit2;
#endif
```

Then remove the explicit `using` from individual test files.

---

## Step 7 — Reset the semantic version

Because the CluedIn version is now encoded in the package name suffix (e.g. `.460.`), the connector's own version no longer needs to mirror CluedIn. Reset to `1.0` so versions are meaningful and predictable.

### Problem: old tags block `next-version`

Simply setting `next-version: 1.0` in `GitVersion.yml` is **not enough** if older tags like `4.5.2-beta.38` exist in history. GitVersion sees `4.5.2 > 1.0` and ignores `next-version`.

Adding a `v1.0.0` tag to the feature branch or to `develop` also does not reliably fix PR builds, because the PR merge ref computes the version from the develop base and the old tag may still be reachable and dominant.

### Solution: `ignore.commits-before`

In `GitVersion.yml`, add:

```yaml
next-version: 1.0
ignore:
  commits-before: <timestamp>
```

Set `<timestamp>` to a point in time **after** the last commit that carries an old high-version tag but **before** the first commit of the new work. To find the right value:

```bash
# Find the commit date of the highest old tag
git log --format="%aI %H %D" | grep "tag: 4\."

# Find the first commit on the feature branch
git log origin/develop..HEAD --format="%aI %H" | tail -1
```

Pick a timestamp in ISO 8601 format that sits between those two moments.

This approach means no explicit `v1.0.0` tag is needed, and it works correctly for PR builds because it operates on the commit graph rather than on branch-local tags.

---

## Checklist

- [ ] `azure-pipelines.yml` — switched to `crawler.build.jobs.yml` with `multiVersionCluedInTargets`
- [ ] `Directory.Build.props` — honours `CluedInMultiVersionTargetFramework` with local fallback
- [ ] `Packages.props` — `_CluedIn` guarded; `DefineConstants` derived; package versions gated by TFM
- [ ] `NuGet.Config` — correct filename casing; CluedIn public feed added
- [ ] Source code — `#if` guards for any API that changed between targeted versions
- [ ] Test project — conditional xunit/AutoFixture package selection; `GlobalUsings.cs` for namespace differences
- [ ] `GitVersion.yml` — `next-version: 1.0`; `ignore.commits-before` set to skip old high-version tags
