using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using UnrealSharp.Core;
using UnrealSharp.Core.Attributes;

Run("matching, aliases, case, duplicate winner, null and empty names", Matching);
Run("concurrent preparation and repeated distinct misses scan once", ConcurrentPreparation);
Run("failed builds are cached and never publish partial results", FailedBuild);
Run("reentrant preparation fails without rescanning", ReentrantPreparation);
Run("same-name assembly generations remain independent", Generations);
Run("collectible load contexts are not rooted by the index", Unload);
if (args.Length != 0)
{
    Run("packaged assembly differential", () => ComparePackage(Path.GetFullPath(args[0])));
}
Console.WriteLine("All generated-type index checks passed.");

static void Run(string name, Action test)
{
    test();
    Console.WriteLine($"PASS: {name}");
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static Type? PreviousResolver(Assembly assembly, string name)
{
    foreach (Type type in assembly.GetTypes())
    foreach (CustomAttributeData attribute in type.CustomAttributes)
    {
        if (attribute.AttributeType.FullName == typeof(GeneratedTypeAttribute).FullName &&
            attribute.ConstructorArguments.Count == 2 &&
            (string?)attribute.ConstructorArguments[1].Value == name)
            return type;
    }
    return null;
}

static void Matching()
{
    Assembly assembly = typeof(First).Assembly;
    foreach (string name in new[] { "Native.Alias", "native.alias", "Missing", "", typeof(First).FullName! })
        Check(GeneratedTypeIndex.FindType(assembly, name) == PreviousResolver(assembly, name), $"Mismatch: {name}");
    Check(GeneratedTypeIndex.FindType(assembly, "Native.Alias") == typeof(First), "First duplicate winner changed");
}

static void ConcurrentPreparation()
{
    CountingAssembly assembly = new(() => [typeof(First)]);
    Parallel.For(0, 64, i =>
    {
        GeneratedTypeIndex.Prepare(assembly);
        Check(GeneratedTypeIndex.FindType(assembly, "Native.Alias") == typeof(First), "Missing known type");
        Check(GeneratedTypeIndex.FindType(assembly, $"Missing.{i}") == null, "False positive");
    });
    Check(assembly.Scans == 1, $"Concurrent requests scanned {assembly.Scans} times");
}

static void FailedBuild()
{
    CountingAssembly assembly = new(() => throw new ReflectionTypeLoadException(
        [typeof(First), null], [new TypeLoadException("Fixture dependency unavailable")]));
    for (int i = 0; i < 3; ++i)
    {
        try
        {
            GeneratedTypeIndex.FindType(assembly, "Native.Alias");
            throw new Exception("Failed index was presented as a valid result");
        }
        catch (ReflectionTypeLoadException) { }
    }
    Check(assembly.Scans == 1, "Failed index was rebuilt");
}

static void ReentrantPreparation()
{
    CountingAssembly? assembly = null;
    assembly = new(() =>
    {
        GeneratedTypeIndex.Prepare(assembly!);
        return [typeof(First)];
    });
    for (int i = 0; i < 2; ++i)
    {
        try
        {
            GeneratedTypeIndex.Prepare(assembly);
            throw new Exception("Reentry unexpectedly succeeded");
        }
        catch (InvalidOperationException) { }
    }
    Check(assembly.Scans == 1, "Reentry rescanned");
}

static Assembly Generation(string generatedName)
{
    AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
        new AssemblyName("ReloadFixture"), AssemblyBuilderAccess.RunAndCollect);
    TypeBuilder type = assembly.DefineDynamicModule("Fixture").DefineType("SameClrName", TypeAttributes.Public);
    type.SetCustomAttribute(new CustomAttributeBuilder(
        typeof(GeneratedTypeAttribute).GetConstructor([typeof(string), typeof(string)])!, ["Engine", generatedName]));
    type.CreateType();
    return assembly;
}

static void Generations()
{
    Assembly oldAssembly = Generation("Old.Name");
    Assembly newAssembly = Generation("New.Name");
    Check(GeneratedTypeIndex.FindType(oldAssembly, "Old.Name") != null, "Old generation missing");
    Check(GeneratedTypeIndex.FindType(newAssembly, "Old.Name") == null, "Old type leaked into new generation");
    Check(GeneratedTypeIndex.FindType(oldAssembly, "New.Name") == null, "New type leaked into old generation");
    Check(GeneratedTypeIndex.FindType(newAssembly, "New.Name") != null, "New generation missing");
}

[MethodImpl(MethodImplOptions.NoInlining)]
static WeakReference CreateCollectibleIndex()
{
    AssemblyLoadContext context = new("GeneratedTypeIndex fixture", isCollectible: true);
    Assembly assembly = context.LoadFromAssemblyPath(typeof(First).Assembly.Location);
    Type type = GeneratedTypeIndex.FindType(assembly, "Native.Alias")!;
    Check(type.Assembly == assembly, "Type came from the wrong load context");
    WeakReference weak = new(context);
    context.Unload();
    return weak;
}

static void Unload()
{
    WeakReference weak = CreateCollectibleIndex();
    for (int i = 0; weak.IsAlive && i < 20; ++i)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
    Check(!weak.IsAlive, "Index rooted collectible load context");
}

static void ComparePackage(string directory)
{
    AssemblyLoadContext context = new("Packaged binding comparison", isCollectible: true);
    Dictionary<string, string> files = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories)
        .GroupBy(Path.GetFileNameWithoutExtension).ToDictionary(group => group.Key!, group => group.First());
    HashSet<string> included = ["UnrealSharp", "ManagedTankSurvival"];
    foreach (string orderFile in Directory.GetFiles(directory, "*LoadOrder*.json"))
    {
        using JsonDocument order = JsonDocument.Parse(File.ReadAllText(orderFile));
        foreach (JsonElement name in order.RootElement.GetProperty("LoadOrder").EnumerateArray())
            included.Add(name.GetString()!);
    }
    context.Resolving += (alc, name) => files.TryGetValue(name.Name!, out string? path) ? alc.LoadFromAssemblyPath(path) : null;
    int assemblyCount = 0, nameCount = 0;
    foreach (string file in files.Values)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        if (!included.Contains(name)) continue;
        Assembly assembly = context.LoadFromAssemblyPath(file);
        Type[] types = assembly.GetTypes();
        Dictionary<string, Type> expected = new(StringComparer.Ordinal);
        foreach (Type type in types)
        foreach (CustomAttributeData attribute in type.CustomAttributes)
        {
            if (attribute.AttributeType.FullName == typeof(GeneratedTypeAttribute).FullName &&
                attribute.ConstructorArguments.Count == 2 && attribute.ConstructorArguments[1].Value is string key)
                expected.TryAdd(key, type);
        }
        GeneratedTypeIndex.Prepare(assembly);
        foreach ((string key, Type type) in expected)
            Check(GeneratedTypeIndex.FindType(assembly, key) == type, $"Packaged mismatch: {name}/{key}");
        Check(GeneratedTypeIndex.FindType(assembly, "__GeneratedTypeIndexMissing__") == null, "Packaged false positive");
        Console.WriteLine($"  {name}: {expected.Count} generated names match");
        ++assemblyCount;
        nameCount += expected.Count;
    }
    Check(assemblyCount >= 2 && nameCount > 100, "Insufficient packaged assembly coverage");
    context.Unload();
}

[GeneratedType("FirstEngineName", "Native.Alias")]
class First;
[GeneratedType("SecondEngineName", "Native.Alias")]
class Second;
[GeneratedType("Null", null!)]
class NullName;
[GeneratedType("Empty", "")]
class EmptyName;

class CountingAssembly(Func<Type[]> getTypes) : Assembly
{
    public int Scans;
    public override AssemblyName GetName(bool copiedName) => new("CountingFixture");
    public override Type[] GetTypes()
    {
        Interlocked.Increment(ref Scans);
        Thread.Sleep(10);
        return getTypes();
    }
}

namespace UnrealSharp.Core
{
    // The resolver is linked unchanged; only the native logging bridge is substituted.
    internal static class LogUnrealSharpCore
    {
        public static void Log(string message) { }
        public static void LogWarning(string message) { }
        public static void LogError(string message) { }
    }
}
