using System.Linq;
using HockeyGame.Core;
using NUnit.Framework;

namespace HockeyGame.Core.Tests
{
    public class StartShotsTests
    {
        [Test] public void There_are_5_different_shots_and_at_least_1_moves()
        {
            Assert.AreEqual(5, StartShots.All.Length);
            Assert.That(StartShots.All.Count(s => s.Moving), Is.GreaterThanOrEqualTo(1));
            Assert.AreEqual(5, StartShots.All.Select(s => (s.EyeFrom, s.LookFrom)).Distinct().Count());
        }

        [Test] public void Each_shot_holds_6_to_8_seconds_and_the_cycle_is_their_sum()
        {
            Assert.That(StartShots.All.All(s => s.Seconds >= 6 && s.Seconds <= 8));
            Assert.AreEqual(StartShots.All.Sum(s => s.Seconds), StartShots.CycleSeconds, 1e-9);
        }

        [Test] public void The_shot_changes_at_each_boundary_and_loops()
        {
            double t = 0;
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(i, StartShots.At(t + 0.001).Shot);
                t += StartShots.All[i].Seconds;
                Assert.AreEqual(i, StartShots.At(t - 0.001).Shot);
            }
            Assert.AreEqual(0, StartShots.At(StartShots.CycleSeconds + 0.001).Shot);
            Assert.AreEqual(0, StartShots.At(-5).Shot);
        }

        [Test] public void A_moving_shot_starts_at_From_ends_at_To_and_a_fixed_shot_holds_still()
        {
            double start = 0;
            bool sawMoving = false, sawFixed = false;
            for (int i = 0; i < StartShots.All.Length; i++)
            {
                StartShot shot = StartShots.All[i];
                StartShotPose first = StartShots.At(start);
                StartShotPose mid = StartShots.At(start + shot.Seconds / 2);
                StartShotPose last = StartShots.At(start + shot.Seconds - 1e-6);
                Assert.AreEqual(i, first.Shot);
                Assert.AreEqual(i, last.Shot);
                if (shot.Moving)
                {
                    sawMoving = true;
                    Assert.That(RinkPoint.Distance(first.Eye, shot.EyeFrom), Is.LessThan(1e-3));
                    Assert.That(RinkPoint.Distance(first.Look, shot.LookFrom), Is.LessThan(1e-3));
                    Assert.That(RinkPoint.Distance(last.Eye, shot.EyeTo), Is.LessThan(1e-3));
                    Assert.That(RinkPoint.Distance(last.Look, shot.LookTo), Is.LessThan(1e-3));
                }
                else
                {
                    sawFixed = true;
                    Assert.That(RinkPoint.Distance(first.Eye, mid.Eye), Is.LessThan(1e-9));
                    Assert.That(RinkPoint.Distance(mid.Eye, last.Eye), Is.LessThan(1e-9));
                    Assert.That(RinkPoint.Distance(first.Look, last.Look), Is.LessThan(1e-9));
                }
                start += shot.Seconds;
            }
            Assert.That(sawMoving && sawFixed);
        }

        [Test] public void A_moving_shot_eases_in_and_out()
        {
            StartShot shot = StartShots.All.First(s => s.Moving);
            double start = StartShots.All.TakeWhile(s => !s.Equals(shot)).Sum(s => s.Seconds);
            double total = RinkPoint.Distance(shot.EyeFrom, shot.EyeTo) + RinkPoint.Distance(shot.LookFrom, shot.LookTo);
            double early = RinkPoint.Distance(StartShots.At(start + 0.1 * shot.Seconds).Eye, shot.EyeFrom)
                + RinkPoint.Distance(StartShots.At(start + 0.1 * shot.Seconds).Look, shot.LookFrom);
            Assert.That(early, Is.LessThan(0.1 * total * 0.5));
        }

        [Test] public void Every_eye_is_above_the_ice_inside_the_boards_and_the_start_screen_is_zoomed_out()
        {
            double start = 0;
            foreach (StartShot shot in StartShots.All)
            {
                for (int k = 0; k <= 10; k++)
                {
                    StartShotPose pose = StartShots.At(start + shot.Seconds * k / 10.0 - (k == 10 ? 1e-6 : 0));
                    string where = $"{shot.Name} at {k}/10: eye {pose.Eye}";
                    Assert.That(pose.Eye.Y, Is.InRange(2.0, RinkLayout.CentreBoardBottom - 1), where);
                    Assert.That(System.Math.Abs(pose.Eye.X), Is.LessThan(RinkLayout.HalfWidth), where);
                    Assert.That(pose.Eye.Z, Is.InRange(RinkLayout.FarEndBoardsZ + 1e-9, RinkLayout.NearEndBoardsZ - 1e-9), where);
                    Assert.That(pose.FieldOfViewDegrees, Is.GreaterThan(60), where);
                }
                start += shot.Seconds;
            }
        }
    }
}
