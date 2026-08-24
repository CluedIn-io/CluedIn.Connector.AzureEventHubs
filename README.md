# CluedIn.Connector.AzureEventHub

Supports connections to post events to Azure Event Hubs

# About CluedIn
CluedIn is the Cloud-native Master Data Management Platform that brings data teams together enabling them to deliver the foundation of high-quality, trusted data that empowers everyone to make a difference. 

We're different because we use enhanced data management techniques like [Graph](https://www.cluedin.com/graph-versus-relational-databases-which-is-best) and [Zero Upfront Modelling](https://www.cluedin.com/upfront-versus-dynamic-data-modelling) to accelerate the time taken to prepare data to deliver insight by as much as 80%. Installed in as little as 20 minutes from the [Azure Marketplace](https://azuremarketplace.microsoft.com/en-gb/marketplace/apps/cluedin.azure_cluedin?tab=Overview), CluedIn is fully integrated with [Microsoft Purview](https://www.cluedin.com/product/microsoft-purview-mdm-integration?hsCtaTracking=461021ab-7a38-41a3-93dd-cfe2325dfd35%7Cb835efc0-e9b7-4385-a1b6-75cb7632527b) and the full [Microsoft Fabric](https://www.cluedin.com/microsoft-fabric) suite, making it the preferred choice for [Azure customers](https://www.cluedin.com/microsoft-intelligent-data-platform). 

To learn more about CluedIn, [contact the team](https://www.cluedin.com/discovery-call) today.

[https://www.cluedin.com](https://www.cluedin.com)

---

## Development

### Multi-version targeting

This connector is built against multiple CluedIn versions simultaneously using the shared `crawler.build.jobs.yml` pipeline template. Each version produces a separate NuGet package with a CluedIn version suffix, e.g.:

| CluedIn version | Target framework | Example package |
|---|---|---|
| 4.6.0 | net6.0 | `CluedIn.Connector.AzureEventHub.460.1.0.x.nupkg` |
| 4.7.0 | net6.0 | `CluedIn.Connector.AzureEventHub.470.1.0.x.nupkg` |
| 4.8.0 | net6.0 | `CluedIn.Connector.AzureEventHub.480.1.0.x.nupkg` |
| 5.0.0-alpha.* | net10.0 | `CluedIn.Connector.AzureEventHub.50.1.0.x.nupkg` |

#### How it works

- **`azure-pipelines.yml`** — uses `crawler.build.jobs.yml` (job-level template) with a `multiVersionCluedInTargets` list. The pipeline injects `_CluedIn=<version>` and `CluedInMultiVersionTargetFramework=<tfm>` as MSBuild properties for each leg.
- **`Directory.Build.props`** — uses `CluedInMultiVersionTargetFramework` when set by the pipeline, otherwise falls back to `net10.0` for local development.
- **`Packages.props`** — `_CluedIn` has a fallback default (guarded with `Condition="'$(_CluedIn)' == ''"`) so the pipeline override takes effect. It also derives `DefineConstants` (`CLUEDIN_V47`, `CLUEDIN_V48`, `CLUEDIN_V50`) from the resolved version for use in `#if` guards.
- **`NuGet.Config`** — includes the CluedIn public Azure Artifacts feed (`pkgs.dev.azure.com/CluedIn-io/Public/_packaging/Public`) required to resolve CluedIn packages. The filename must be `NuGet.Config` (exact casing) for the Linux CI agents.

#### API compatibility guards

CluedIn introduced breaking API changes in 4.7. Code that differs between versions is wrapped in `#if CLUEDIN_V47` / `#else` blocks:

- `GetAllStreams()` — pre-4.7 is synchronous with no arguments; 4.7+ is async and takes `executionContext`
- `GetStreamMappings(id)` / `GetStreamMappings(ctx, id)` — argument order changed in 4.7
- `SetupConnector(id, model, ctx)` / `SetupConnector(ctx, id, model)` — argument order changed in 4.7

#### Test package compatibility

The net6.0 targets (4.6–4.8) cannot use the latest test packages which require net8.0+. The test project conditionally selects packages based on the `CLUEDIN_V50` constant:

| Package | net6.0 (4.6–4.8) | net10.0 (5.0) |
|---|---|---|
| xunit | 2.9.3 | xunit.v3 3.2.2 |
| AutoFixture | AutoFixture.Xunit2 4.18.0 | AutoFixture.Xunit3 (preview) |
| Microsoft.NET.Test.Sdk | 17.12.0 | 18.3.0 |

`test/unit/Connector.SqlServer.Test/GlobalUsings.cs` provides the correct `global using` for the AutoFixture xunit namespace based on the active constant.

#### Versioning

Package versions start at `1.0` (rather than mirroring the CluedIn version) because the CluedIn version is already encoded in the package name suffix. `GitVersion.yml` sets `next-version: 1.0` and uses `ignore.commits-before: 2026-08-24T03:50:00` to discard all pre-rewrite commit history (and their tags such as `4.5.2-beta.38`) from the version calculation.