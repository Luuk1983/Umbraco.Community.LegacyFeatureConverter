# Umbraco.Community.LegacyFeatureConverter

[![NuGet](https://img.shields.io/nuget/v/Umbraco.Community.LegacyFeatureConverter?logo=nuget)](https://www.nuget.org/packages/Umbraco.Community.LegacyFeatureConverter)
[![NuGet downloads](https://img.shields.io/nuget/dt/Umbraco.Community.LegacyFeatureConverter?logo=nuget)](https://www.nuget.org/packages/Umbraco.Community.LegacyFeatureConverter)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![GitHub release](https://img.shields.io/github/v/release/Luuk1983/Umbraco.Community.LegacyFeatureConverter?include_prereleases)](https://github.com/Luuk1983/Umbraco.Community.LegacyFeatureConverter/releases)

> **Beta software.** This package is under active development. Always back up your database before running conversions on production content.

A backoffice tool that converts legacy **Grid** and **Macro** content in Umbraco 13 sites into modern **BlockGrid** and **BlockList** structures, so you can retire the deprecated Grid editor and Macro infrastructure before upgrading to Umbraco 14+.

## Why?

Umbraco 14 removes the legacy Grid editor and rewrites the macro pipeline. Sites built on Umbraco 7–13 often have hundreds of nodes using these features, and the standard upgrade path leaves their content unreachable. This package gives you a controlled, repeatable way to migrate that content while still on v13 — *before* the cliff.

## Features

- **Grid → BlockGrid / BlockList conversion** for the most common legacy Grid editors (rich text, media picker, nested content blocks).
- **Pluggable property-converter pipeline** — register your own `IPropertyConverter` to handle bespoke legacy editors.
- **Wizard UI** in the backoffice that walks you through: pick document types, preview the impact, choose conversion approach, run.
- **Queued background conversion** — long-running conversions don't block the backoffice; an in-memory queue runs them on a hosted service.
- **Real-time progress** via SignalR — the wizard shows a live progress bar with per-node status while the conversion is running.
- **Conversion history** — every run is persisted to a database table (SQL Server or SQLite) so you can audit what changed and re-run failed nodes.

## Getting started

### Prerequisites

- Umbraco CMS 13.13.1 or later (Umbraco 13 LTS line)
- .NET 8

### Installation

```bash
dotnet add package Umbraco.Community.LegacyFeatureConverter --prerelease
```

The package self-registers via an Umbraco composer — no `Startup.cs` changes required. On first run it applies its own EF Core migration to create the conversion-history tables.

After installation, restart your site and open the backoffice. A new **Legacy Feature Converter** section is available; the wizard lives under its tree.

### Configuration

No configuration is required for the default conversion paths. To register a custom property converter, implement `IPropertyConverter` (from the bundled `Umbraco.Community.LegacyFeatureConverter.Abstractions` namespace) and register it in DI — it will be picked up automatically.

## Feedback

Bug reports, feature requests, and converter contributions are welcome on the [GitHub issue tracker](https://github.com/Luuk1983/Umbraco.Community.LegacyFeatureConverter/issues).

## License

[MIT](LICENSE.txt) — Copyright (c) Luuk Peters
