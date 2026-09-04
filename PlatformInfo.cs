using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace IngressLoadTest;

public static class PlatformInfo
{
    public static void Print()
    {
        Console.WriteLine("Runtime");
        Console.WriteLine($"  OS           : {RuntimeInformation.OSDescription}");
        Console.WriteLine($"  Framework    : {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"  Process Arch : {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"  OS Arch      : {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"  CPU Count    : {Environment.ProcessorCount}");
        Console.WriteLine($"  Server GC    : {GCSettings.IsServerGC}");
        Console.WriteLine($"  Stopwatch Hz : {Stopwatch.Frequency:N0}");
        Console.WriteLine($"  BaseDirectory: {AppContext.BaseDirectory}");
    }

    public static string ToLogText()
    {
        return
            $"OS={RuntimeInformation.OSDescription}, " +
            $"Framework={RuntimeInformation.FrameworkDescription}, " +
            $"ProcessArch={RuntimeInformation.ProcessArchitecture}, " +
            $"OSArch={RuntimeInformation.OSArchitecture}, " +
            $"CpuCount={Environment.ProcessorCount}, " +
            $"ServerGC={GCSettings.IsServerGC}, " +
            $"StopwatchFrequency={Stopwatch.Frequency}, " +
            $"BaseDirectory={AppContext.BaseDirectory}";
    }
}
