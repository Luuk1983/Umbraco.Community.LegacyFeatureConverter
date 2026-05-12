# CLAUDE.md

## Project Overview

**Umbraco.Community.LegacyFeatureConverter** — An Umbraco CMS plugin that helps convert legacy Grid and Macro content to modern BlockGrid/BlockList features in Umbraco 13.

## Tech Stack

- **Language:** C# / .NET 8.0
- **CMS:** Umbraco CMS v13.13.1
- **View Engine:** Razor (CSHTML)
- **Database:** SQL Server (LocalDB for dev)
- **Logging:** Serilog

## Project Structure

```
src/
├── Umbraco.Community.LegacyFeatureConverter/              # Plugin package (Razor SDK, the only NuGet-packable project)
│   ├── Umbraco.Community.LegacyFeatureConverter.csproj
│   └── wwwroot/package.manifest
│
├── Umbraco.Community.LegacyFeatureConverter.Abstractions/  # Interfaces, models, base converters (bundled into main package)
├── Umbraco.Community.LegacyFeatureConverter.Converters/    # Property converter implementations (bundled)
├── Umbraco.Community.LegacyFeatureConverter.Data/          # EF Core context & migrations (bundled)
├── Umbraco.Community.LegacyFeatureConverter.Infrastructure/# Services, background queue (bundled)
├── Umbraco.Community.LegacyFeatureConverter.Tests/         # MSTest unit tests
│
├── Umbraco.Community.LegacyFeatureConverter.TestUmbracoInstance/  # Test Umbraco site
│   ├── Program.cs                                   # Entry point
│   ├── appsettings.json / appsettings.Development.json
│   └── Views/Partials/
│       ├── blockgrid/    # Modern BlockGrid views
│       ├── blocklist/    # Modern BlockList views
│       └── grid/         # Legacy Grid views + editors
│
└── Umbraco.Community.LegacyFeatureConverter.slnx               # Solution file
```

The four supporting libraries are `<IsPackable>false</IsPackable>` and bundled into the main package's `lib/net8.0/` folder via `PrivateAssets="all"` + a custom `CopyProjectReferencesToPackage` MSBuild target. They are not published as separate NuGet packages.

## Build & Run

```bash
# Restore and build
dotnet build src/Umbraco.Community.LegacyFeatureConverter.slnx

# Run the test instance
dotnet run --project src/Umbraco.Community.LegacyFeatureConverter.TestUmbracoInstance

# Pack the NuGet package locally
dotnet pack src/Umbraco.Community.LegacyFeatureConverter/Umbraco.Community.LegacyFeatureConverter.csproj -c Release -o ./artifacts
```

## Versioning & Release

- Version is derived from git tags via MinVer (`v0.1.0-beta.1` → `0.1.0-beta.1`). No manual `<Version>` in csproj.
- CI (`.github/workflows/ci.yml`) runs build/test/pack on every PR.
- Publish (`.github/workflows/publish.yml`) triggers on `v*.*.*` tag push and pushes to NuGet via trusted publishing (OIDC); pre-release tags (containing `-`) are auto-flagged on the GitHub release.

## Key Dependencies

- `Umbraco.Cms.Web.Website` v13.13.1
- `Umbraco.Cms.Web.BackOffice` v13.13.1
