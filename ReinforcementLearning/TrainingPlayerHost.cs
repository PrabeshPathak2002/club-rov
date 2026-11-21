// Windows-only WSL interop launcher. All parallel players share one CPU-limited job.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

internal static class TrainingPlayerHost
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int informationClass, ref CpuLimit value, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int informationClass, ref ExtendedLimits value, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
    [StructLayout(LayoutKind.Sequential)]
    private struct CpuLimit { public uint Flags, Rate; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint Flags;
        public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcesses;
        public UIntPtr Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }

    private static void Check(bool success, string operation)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), operation + " failed");
    }

    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? 2 * slashes + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        result.Append('\\', 2 * slashes);
        return result.Append('"').ToString();
    }

    public static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: TrainingPlayerHost <cpu-percent> <player> [args]"); return 2; }
        IntPtr job = IntPtr.Zero;
        Process player = null;
        try
        {
            uint percent = uint.Parse(args[0]);
            if (percent < 1 || percent > 100) throw new ArgumentOutOfRangeException("cpu-percent", "Use 1..100 percent.");
            job = CreateJobObject(IntPtr.Zero, "Local\\ClubROV-RL-ParallelPlayers");
            Check(job != IntPtr.Zero, "CreateJobObject");
            var lifetime = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } };
            // Close the last host => terminate remaining players, including aborted WSL sessions.
            Check(SetInformationJobObject(job, 9, ref lifetime, (uint)Marshal.SizeOf(typeof(ExtendedLimits))), "Set job lifetime");
            // JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP.
            var limit = new CpuLimit { Flags = 5, Rate = percent * 100 };
            Check(SetInformationJobObject(job, 15, ref limit, (uint)Marshal.SizeOf(typeof(CpuLimit))), "Set CPU rate");
            if (args[1] == "--set-limit") { Console.WriteLine("Training player CPU limit set to " + percent + "%."); return 0; }
            var arguments = new StringBuilder();
            for (int i = 2; i < args.Length; i++) arguments.Append(Quote(args[i])).Append(' ');
            player = Process.Start(new ProcessStartInfo(args[1], arguments.ToString()) { UseShellExecute = false, CreateNoWindow = true });
            Check(AssignProcessToJobObject(job, player.Handle), "Assign training player");
            player.WaitForExit();
            return player.ExitCode;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Training player CPU limit failed: " + error.Message);
            if (player != null && !player.HasExited) player.Kill();
            return 1;
        }
        finally
        {
            if (player != null) player.Dispose();
            if (job != IntPtr.Zero) CloseHandle(job);
        }
    }
}
