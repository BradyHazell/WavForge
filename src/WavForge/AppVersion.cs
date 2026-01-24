using System.Reflection;

namespace WavForge;

internal static class AppVersion
{
    public static string Current
    {
        get
        {
            string raw = Assembly.GetEntryAssembly()?
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion
                ?? Assembly.GetEntryAssembly()?
                    .GetName()
                    .Version?
                    .ToString()
                ?? "Unknown";
            int plusIndex = raw.IndexOf('+');
            return plusIndex >= 0 ? raw[..plusIndex] : raw;
        }
    }
        
}
