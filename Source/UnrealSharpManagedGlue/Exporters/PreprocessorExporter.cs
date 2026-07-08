using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EpicGames.Core;
using UnrealSharpManagedGlue.SourceGeneration;
using UnrealSharpManagedGlue.Utilities;
using UnrealBuildTool;

namespace UnrealSharpManagedGlue.Exporters;

public static class PreprocessorExporter
{
    public static void ExportBuildDefines()
    {
        GenerateMSBuildProps(ParseBuildRulesProject(GeneratorStatics.EngineDirectory));
    }
    
    private static HashSet<string> ParseBuildRulesProject(string engineDirectory)
    {
        HashSet<string> definesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        string csproj = Path.Combine(engineDirectory, "Intermediate", "Build", "BuildRulesProjects", "UE5Rules", "UE5Rules.csproj");
        if (!File.Exists(csproj))
        {
            AddEngineVersionDefines(engineDirectory, definesSet);
            return definesSet;
        }

        XDocument document;
        try 
        { 
            document = XDocument.Load(csproj); 
        }
        catch 
        {
            AddEngineVersionDefines(engineDirectory, definesSet);
            return definesSet; 
        }

        IEnumerable<string> values = document.Descendants("DefineConstants").Select(x => x.Value);
        foreach (string value in values)
        {
            foreach (string raw in value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string s = raw.Trim();
                
                if (s.Length == 0)
                {
                    continue;
                }

                if (s.StartsWith("$(", StringComparison.Ordinal))
                {
                    continue;
                }

                definesSet.Add(s);
            }
        }

        AddEngineVersionDefines(engineDirectory, definesSet);

        return definesSet;
    }

    private static void AddEngineVersionDefines(string engineDirectory, HashSet<string> definesSet)
    {
        FileReference versionFile = new FileReference(Path.Combine(engineDirectory, "Build", "Build.version"));
        if (!BuildVersion.TryRead(versionFile, out BuildVersion? version))
        {
            return;
        }

        for (int minorVersion = 17; minorVersion <= 30; ++minorVersion)
        {
            definesSet.Add($"UE_4_{minorVersion}_OR_LATER");
        }

        if (version.MajorVersion < 5)
        {
            return;
        }

        for (int minorVersion = 0; minorVersion <= version.MinorVersion; ++minorVersion)
        {
            definesSet.Add($"UE_5_{minorVersion}_OR_LATER");
        }
    }

    private static void GenerateMSBuildProps(HashSet<string> defines)
    {
        IOrderedEnumerable<string> ordered = defines
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s);

        string joined = string.Join(";", ordered);

        GeneratorStringBuilder stringBuilder = new GeneratorStringBuilder();

        stringBuilder.AppendLine("<Project>");
        stringBuilder.Indent();
        stringBuilder.AppendLine("<PropertyGroup>");
        stringBuilder.Indent();
        stringBuilder.AppendLine($"<DefineConstants>$(DefineConstants);{joined}</DefineConstants>");
        stringBuilder.UnIndent();
        stringBuilder.AppendLine("</PropertyGroup>");
        stringBuilder.UnIndent();
        stringBuilder.AppendLine("</Project>");

        string propsPath = Path.Combine(GeneratorStatics.PluginModuleInfo.Module.GetUHTBaseDirectory(), "UE5Rules.Defines.props");
        File.WriteAllText(propsPath, stringBuilder.ToString());
    }
}
