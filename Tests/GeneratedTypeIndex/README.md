# Generated binding index checks

Run with .NET 10:

```powershell
dotnet run --project Plugins/UnrealSharp/Tests/GeneratedTypeIndex/GeneratedTypeIndex.Tests.csproj -c Release
```

An optional argument points to a packaged `Binaries/Managed/net10.0` directory. It compares every generated binding name in the engine assembly and assemblies listed in the package's load-order files against the previous resolver's first-match semantics:

```powershell
dotnet run --project Plugins/UnrealSharp/Tests/GeneratedTypeIndex/GeneratedTypeIndex.Tests.csproj -c Release -- <packaged-managed-directory>
```

The harness links the production index source and attribute definition unchanged; only native logging is substituted. Checks cover exact names versus CLR aliases, case, duplicate winners, null/empty names, concurrent preparation, distinct misses, cached failures, reentry, same-name replacement generations, and collection of an actual collectible load context. No external test packages are required.

This does not test native handle ownership or the complete Unreal Editor hot-reload pipeline. Packaged execution and CPU traces validate callback integration separately; see `docs/performance/2026-09-16-managed-type-index-results.md` in the TankSurvival project.
