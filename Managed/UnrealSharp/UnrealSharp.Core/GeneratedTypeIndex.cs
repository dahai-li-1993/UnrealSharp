using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnrealSharp.Core.Attributes;

namespace UnrealSharp.Core;

/// <summary>
/// Resolves generated binding names without rescanning loaded assemblies.
/// Entries follow assembly lifetime, including collectible hot-reload generations.
/// </summary>
public static class GeneratedTypeIndex
{
    private static readonly ConditionalWeakTable<Assembly, Lazy<Dictionary<string, Type>>> Indices = new();
    private static int _buildSequence;

    public static void Prepare(Assembly assembly) => _ = GetIndex(assembly);

    public static Type? FindType(Assembly assembly, string generatedName) =>
        GetIndex(assembly).GetValueOrDefault(generatedName);

    private static Dictionary<string, Type> GetIndex(Assembly assembly) =>
        Indices.GetValue(assembly, static key => new Lazy<Dictionary<string, Type>>(
            () => Build(key), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private static Dictionary<string, Type> Build(Assembly assembly)
    {
        int buildId = Interlocked.Increment(ref _buildSequence);
        long started = Stopwatch.GetTimestamp();
        string assemblyName = assembly.GetName().Name ?? "<unnamed>";
        LogUnrealSharpCore.Log($"[GeneratedTypeIndex] Building #{buildId} for {assemblyName}");

        try
        {
            Dictionary<string, Type> typesByName = new(StringComparer.Ordinal);
            int duplicateCount = 0;
            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                foreach (CustomAttributeData attribute in type.CustomAttributes)
                {
                    if (attribute.AttributeType.FullName != typeof(GeneratedTypeAttribute).FullName ||
                        attribute.ConstructorArguments.Count != 2)
                    {
                        continue;
                    }

                    string? name = (string?)attribute.ConstructorArguments[1].Value;
                    // Null could not match a native string in the previous resolver.
                    if (name != null && !typesByName.TryAdd(name, type))
                    {
                        ++duplicateCount;
                    }
                }
            }

            if (duplicateCount != 0)
            {
                LogUnrealSharpCore.LogWarning($"[GeneratedTypeIndex] {assemblyName} has {duplicateCount} duplicate generated names; preserving the first match.");
            }

            LogUnrealSharpCore.Log($"[GeneratedTypeIndex] Prepared #{buildId} for {assemblyName}: {typesByName.Count} names, {types.Length} types, {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3} ms");
            return typesByName;
        }
        catch (Exception exception)
        {
            // Lazy caches this failure: never publish a partial index or rescan on every miss.
            LogUnrealSharpCore.LogError($"[GeneratedTypeIndex] Failed #{buildId} for {assemblyName}: {exception}");
            throw;
        }
    }
}
