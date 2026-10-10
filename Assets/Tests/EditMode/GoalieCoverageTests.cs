using System.Collections.Generic;
using System.Text;
using HockeyGame;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The posed goalie character, seen from the shooter, reads as the Goalie Pose table: a puck shot from the shooter's eye
    /// at points in each Zone of the Net Face touches nothing in Open Zones, touches the goalie at half or more of the points in
    /// Covered Zones and at some points in Contested Zones. The hidden goalie blocks still decide every Shot; the character is a
    /// readable hint, so only Open Zones are strict. Checked from where the camera is when the window opens and from where
    /// the drift ends, 3 m from the net (near), with the goalie squared up to that eye (#39).
    /// </summary>
    public class GoalieCoverageTests
    {
        const int Samples = 6;          // per Zone, in each direction
        // Open and Covered Zones are sampled clear of the Zone edges (the two eyes see an edge up to ~12 cm apart);
        // a Contested Zone is sampled across nearly all of it, since part of it is meant to be covered.
        const float Inset = 0.2f, ContestedInset = 0.05f;
        // A Contested Zone is visibly partly covered; a Covered Zone is mostly covered (at least half its points). The
        // character's arms reach about 0.49 m and the shooter's eye is 1.5 m up, so natural poses cover the far low corners
        // and the post-hug five-hole at about half from one eye (#33).
        const float ContestedLow = 0.05f, ContestedHigh = 0.95f;
        const float CoveredLow = 0.5f;
        const float PuckDiameter = 0.076f; // a real puck

        GameObject _parent;
        bool _backfaces;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("Rink (test)");
            _backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true; // a gap is a gap, not a mesh seen from behind
        }

        [TearDown]
        public void TearDown()
        {
            Physics.queriesHitBackfaces = _backfaces;
            Object.DestroyImmediate(_parent);
        }

        static IEnumerable<GoaliePose> Poses() => (GoaliePose[])System.Enum.GetValues(typeof(GoaliePose));

        [TestCaseSource(nameof(Poses))]
        public void Seen_from_the_shooter_the_posed_goalie_leaves_Open_Zones_open_mostly_fills_Covered_Zones_and_partly_fills_Contested_Zones(GoaliePose pose)
        {
            var character = new GoalieCharacter(_parent.transform);
            Assume.That(character.Present, "no goalie model");
            var goalie = new GoalieView(_parent.transform, character);
            goalie.Show(true, pose, false);

            var report = new StringBuilder();
            bool ok = true;
            foreach (var (view, eye) in Eyes(GoaliePoses.SideOf(pose)))
            {
                // Squared up to the shooter at this eye, fully set: every pose keeps the whole turn (#40).
                float facing = GoalieCharacter.FacingToward(eye);
                goalie.Face(facing, facing);
                goalie.SetProgress(1f);
                var colliders = AddColliders(character);
                report.AppendLine($"  {view}: facing {facing:F1} degrees");
                var hits = new bool[3, 3][,];
                for (int column = 0; column < 3; column++)
                for (int row = 0; row < 3; row++)
                {
                    ZoneCover cover = GoaliePoses.CoverAt(pose, column, row);
                    hits[column, row] = Cast(eye, column, row, cover == ZoneCover.Contested ? ContestedInset : Inset, colliders, out int count);
                    float fraction = count / (float)(Samples * Samples);
                    bool good = cover == ZoneCover.Open ? count == 0
                        : cover == ZoneCover.Covered ? fraction >= CoveredLow
                        : fraction >= ContestedLow && fraction <= ContestedHigh;
                    if (!good)
                    {
                        ok = false;
                        report.AppendLine($"  {view}: Zone {column},{row} is {cover} but {fraction:P0} of rays hit");
                    }
                }
                report.AppendLine($"  {view} hit map (top row first, # = hit):");
                report.Append(Map(hits));
                RemoveColliders(colliders);
            }
            Assert.IsTrue(ok, pose + " does not match the pose table:\n" + report);
        }

        /// <summary>
        /// The shooter's eye while the goalie is fully set: when the shooting window opens (the camera has already drifted
        /// toward the net through the slow-down) and where the drift ends, 3 m from the net. Same drift as BreakawayView:
        /// scaled time (Breakaway.TimeScale) times the skate speed, capped by Breakaway.CappedDrift.
        /// </summary>
        internal static IEnumerable<(string, Vector3)> Eyes(AttackSide side)
        {
            var settings = new RoundSettings();
            double scaled = 0, dt = 0.001;
            for (double t = 0; t < settings.SlowDownSeconds; t += dt)
                scaled += Breakaway.TimeScale(RoundPhase.SlowDown, t) * dt;
            double drift = scaled * Breakaway.SkateSpeed(side, settings);

            Breakaway.SkatePoint(side, 1.0, out double x, out double distance);
            var stop = new Vector3((float)x, 0f, -(float)distance);
            Vector3 toNet = -stop;
            Vector3 open = stop + toNet.normalized * (float)Breakaway.CappedDrift(drift, toNet.magnitude);
            Vector3 near = stop + toNet.normalized * (float)Breakaway.CappedDrift(100.0, toNet.magnitude);
            float eye = (float)Breakaway.EyeHeight;
            yield return ("window opens", new Vector3(open.x, eye, open.z));
            yield return ("near", new Vector3(near.x, eye, near.z));
        }

        static bool[,] Cast(Vector3 eye, int column, int row, float inset, HashSet<Collider> colliders, out int count)
        {
            NetFaceMapping.ZoneRectangle(column, row, 1f, out float minX, out float minY, out float maxX, out float maxY);
            var hits = new bool[Samples, Samples];
            count = 0;
            for (int i = 0; i < Samples; i++)
            for (int j = 0; j < Samples; j++)
            {
                float u = inset + (1f - 2f * inset) * i / (Samples - 1), v = inset + (1f - 2f * inset) * j / (Samples - 1);
                var point = new Vector3(Mathf.Lerp(minX, maxX, u), Mathf.Lerp(minY, maxY, v), 0f);
                if (!PuckTouches(eye, point, colliders)) continue;
                hits[i, j] = true;
                count++;
            }
            return hits;
        }

        /// <summary>
        /// Whether a real puck (7.6 cm) shot from the eye at this Net Face point would touch the goalie: a ray at its centre
        /// and one at each edge (left, right, up, down). A gap narrower than a puck is no gap.
        /// </summary>
        static bool PuckTouches(Vector3 eye, Vector3 point, HashSet<Collider> colliders)
        {
            const float r = PuckDiameter / 2f;
            foreach (Vector3 offset in new[] { Vector3.zero, Vector3.left * r, Vector3.right * r, Vector3.up * r, Vector3.down * r })
            {
                Vector3 ray = point + offset - eye;
                foreach (RaycastHit hit in Physics.RaycastAll(eye, ray.normalized, ray.magnitude))
                    if (colliders.Contains(hit.collider)) return true;
            }
            return false;
        }

        /// <summary>Colliders matching what is drawn: the skinned body and clothes as posed now, and every piece of gear.</summary>
        static HashSet<Collider> AddColliders(GoalieCharacter character)
        {
            var colliders = new HashSet<Collider>();
            foreach (Renderer r in character.Root.GetComponentsInChildren<Renderer>())
            {
                if (r is SkinnedMeshRenderer skinned)
                {
                    var baked = new Mesh();
                    skinned.BakeMesh(baked, true);
                    var holder = new GameObject(r.name + " (collider)");
                    holder.transform.SetParent(character.Root.transform, true);
                    holder.transform.SetPositionAndRotation(skinned.transform.position, skinned.transform.rotation);
                    var collider = holder.AddComponent<MeshCollider>();
                    collider.sharedMesh = baked;
                    colliders.Add(collider);
                }
                else
                {
                    var collider = r.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                    colliders.Add(collider);
                }
            }
            Physics.SyncTransforms();
            return colliders;
        }

        static void RemoveColliders(HashSet<Collider> colliders)
        {
            foreach (Collider c in colliders)
            {
                if (c.name.EndsWith(" (collider)")) Object.DestroyImmediate(c.gameObject);
                else Object.DestroyImmediate(c);
            }
            Physics.SyncTransforms();
        }

        static string Map(bool[,][,] hits)
        {
            var sb = new StringBuilder();
            for (int row = 2; row >= 0; row--)
            for (int j = Samples - 1; j >= 0; j--)
            {
                sb.Append("    ");
                for (int column = 0; column < 3; column++)
                {
                    for (int i = 0; i < Samples; i++) sb.Append(hits[column, row][i, j] ? '#' : '.');
                    sb.Append(' ');
                }
                sb.AppendLine();
                if (j == 0) sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
