using System.Globalization;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class FrameTraceTests
    {
        [Test]
        public void Disabled_trace_has_no_buffer()
        {
            Assert.That(FrameTrace.Create(false), Is.Null);
        }

        [Test]
        public void Stage_uses_wall_and_thread_cpu_deltas_and_marks_unsupported()
        {
            var start = new FrameTrace.Sample(100, 200, 1, 2, 3);
            var end = new FrameTrace.Sample(150, 10200, 2, 2, 4);
            var stage = FrameTrace.Between(start, end, 1000);
            Assert.That(stage.WallMs, Is.EqualTo(50));
            Assert.That(stage.CpuMs, Is.EqualTo(1));
            Assert.That(FrameTrace.Between(start, new FrameTrace.Sample(150, -1, 2, 2, 4), 1000).CpuMs, Is.EqualTo(-1));
        }

        [Test]
        public void Supported_cpu_and_both_crowd_modes_keep_their_rows()
        {
            var trace = FrameTrace.Create(true);
            trace.Add(new FrameTrace.Row { Frame = 0, CrowdOn = true,
                Step = new FrameTrace.Stage { WallMs = 4, CpuMs = 1 } });
            trace.Add(new FrameTrace.Row { Frame = 1, Shot = 1, Phase = 2, Measured = true,
                ReplayRendered = true, Readback = new FrameTrace.Stage { WallMs = 9, CpuMs = 0.5 } });
            string csv = trace.ToCsv();
            Assert.That(csv, Does.Contain("0,0,0,true,false,false,false,false,true,4,1"));
            Assert.That(csv, Does.Contain("1,1,2,false,false,false,true,true,true"));
            Assert.That(csv.Split(new[] { '\n' }).Length, Is.EqualTo(4));
        }

        [Test]
        public void Csv_keeps_context_gc_deltas_and_invariant_numbers()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var trace = FrameTrace.Create(true);
                var row = new FrameTrace.Row { Frame = 9, Shot = 2, Phase = 3, CrowdOn = false,
                    Warmup = true, Settle = true, Measured = true, ReplayRendered = false,
                    Step = new FrameTrace.Stage { WallMs = 1.25, CpuMs = -1 },
                    Replay = new FrameTrace.Stage { CpuMs = -1 },
                    Shooter = new FrameTrace.Stage { WallMs = 2.5, CpuMs = -1 },
                    Readback = new FrameTrace.Stage { CpuMs = -1 } };
                row.SetCollections(new FrameTrace.Sample(0, -1, 1, 2, 3), new FrameTrace.Sample(0, -1, 3, 3, 3));
                trace.Add(row);
                string csv = trace.ToCsv();
                Assert.That(csv, Does.Contain("9,2,3,false,true,true,true,false,false,1.25,,0,,2.5,,0,,2,1,0"));
                Assert.That(csv.Split(new[] { '\n' })[0], Does.StartWith("frame,shot,phase,crowd_on"));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
    }
}
