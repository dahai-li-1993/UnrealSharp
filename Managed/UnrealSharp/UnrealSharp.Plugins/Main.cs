
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
using Microsoft.Build.Locator;
using UnrealSharp.Binds;
using UnrealSharp.Core;

#if !PACKAGE
using Microsoft.Build.Locator;
#endif

namespace UnrealSharp.Plugins;

public static class Main
{
    [UnmanagedCallersOnly]
    private static unsafe NativeBool InitializeUnrealSharp(char* workingDirectoryPath, nint assemblyPath, PluginsCallbacks* pluginCallbacks, IntPtr bindsCallbacks, IntPtr managedCallbacks)
    {
        try
        {
            #if WITH_EDITOR
            IEnumerable<VisualStudioInstance> instances = MSBuildLocator.QueryVisualStudioInstances();
            VisualStudioInstance? visualStudioInstance = instances.OrderByDescending(i => i.Version).FirstOrDefault();
            
            if (visualStudioInstance is not null)
            {
                MSBuildLocator.RegisterInstance(visualStudioInstance);
            }
            else
            {
                MSBuildLocator.RegisterDefaults();
            }
            #endif
            
            AppDomain.CurrentDomain.SetData("APP_CONTEXT_BASE_DIRECTORY", new string(workingDirectoryPath));
            
            PluginsCallbacks.Initialize(pluginCallbacks);
            ManagedCallbacks.Initialize(managedCallbacks);
            NativeBinds.Initialize(bindsCallbacks);

            Console.WriteLine("UnrealSharp initialized successfully.");
            return NativeBool.True;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            WriteCrashLog(new string(workingDirectoryPath), exception);
            return NativeBool.False;
        }
    }

    private static void WriteCrashLog(string workingDirectory, Exception exception)
    {
        try
        {
            string logPath = Path.Combine(workingDirectory, "UnrealSharpInitError.log");
            File.WriteAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{exception}{Environment.NewLine}");
        }
        catch
        {
            // Best-effort only: if the working directory isn't writable, there's nothing further we can do here.
        }
    }
}
