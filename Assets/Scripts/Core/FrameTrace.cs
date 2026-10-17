using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace HockeyGame.Core
{
    /// <summary>Opt-in stage diagnostics. Wall time includes waits; thread time is not whole-process CPU time.</summary>
    public sealed class FrameTrace
    {
        public struct Sample
        {
            public long Wall, Cpu;
            public int Gc0, Gc1, Gc2;
            public Sample(long wall, long cpu, int gc0, int gc1, int gc2)
            { Wall = wall; Cpu = cpu; Gc0 = gc0; Gc1 = gc1; Gc2 = gc2; }
        }
        public struct Stage { public double WallMs, CpuMs; }
        public struct Row
        {
            public int Frame, Shot, Phase, Gc0, Gc1, Gc2;
            public bool CrowdOn, Warmup, Settle, Measured, ReplayRendered;
            public Stage Step, Replay, Shooter, Readback;
            public void SetCollections(Sample start, Sample end)
            { Gc0 = end.Gc0 - start.Gc0; Gc1 = end.Gc1 - start.Gc1; Gc2 = end.Gc2 - start.Gc2; }
        }

        readonly List<Row> _rows = new List<Row>(131072);
        static bool _cpuAvailable = Environment.OSVersion.Platform == PlatformID.Win32NT;
        public static FrameTrace Create(bool enabled) => enabled ? new FrameTrace() : null;
        public void Add(Row row) => _rows.Add(row);
        public static Sample Capture() => new Sample(Stopwatch.GetTimestamp(), ThreadTicks(),
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
        // Collection deltas count completed collections; zero does not rule out incremental GC work.
        public static Stage Between(Sample start, Sample end, long frequency) => new Stage
        {
            WallMs = (end.Wall - start.Wall) * 1000.0 / frequency,
            CpuMs = start.Cpu < 0 || end.Cpu < 0 ? -1 : (end.Cpu - start.Cpu) / 10000.0
        };

        // Pseudo-handle: read only, thread-local, and must not be closed.
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetThreadTimes(IntPtr thread, out long created, out long exited, out long kernel, out long user);
        static long ThreadTicks()
        {
            if (!_cpuAvailable) return -1;
            try
            {
                if (GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user)) return kernel + user;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            _cpuAvailable = false;
            return -1;
        }

        public string ToCsv()
        {
            var text = new StringBuilder("frame,shot,phase,crowd_on,warmup,settle,measured,replay_rendered,cpu_supported,step_wall_ms,step_thread_cpu_ms,replay_wall_ms,replay_thread_cpu_ms,shooter_wall_ms,shooter_thread_cpu_ms,readback_wall_ms,readback_thread_cpu_ms,gc0,gc1,gc2\n");
            foreach (Row row in _rows)
            {
                bool cpu = row.Step.CpuMs >= 0 && row.Replay.CpuMs >= 0 && row.Shooter.CpuMs >= 0 && row.Readback.CpuMs >= 0;
                text.Append(row.Frame).Append(',').Append(row.Shot).Append(',').Append(row.Phase).Append(',')
                    .Append(row.CrowdOn ? "true" : "false").Append(',').Append(row.Warmup ? "true" : "false").Append(',')
                    .Append(row.Settle ? "true" : "false").Append(',').Append(row.Measured ? "true" : "false").Append(',')
                    .Append(row.ReplayRendered ? "true" : "false").Append(',').Append(cpu ? "true" : "false");
                Append(text, row.Step, cpu); Append(text, row.Replay, cpu); Append(text, row.Shooter, cpu); Append(text, row.Readback, cpu);
                text.Append(',').Append(row.Gc0).Append(',').Append(row.Gc1).Append(',').Append(row.Gc2).Append('\n');
            }
            return text.ToString();
        }
        static void Append(StringBuilder text, Stage stage, bool cpu)
        {
            text.Append(',').Append(stage.WallMs.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            if (cpu) text.Append(stage.CpuMs.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}
