using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>
    /// The casts that measure a Gap's room (#134, GoalieCharacter.HitsPuckOrRoom): a thin hit shape (a stick shaft is about 2 to 3 cm
    /// wide) anywhere within the puck's reach plus GapRoomMetres of the puck's line must be found, not slip between the casts (#150).
    /// </summary>
    public class GapRoomRingTests
    {
        const float Shaft = 0.02f; // a thin shaft, metres across
        const float Step = 0.005f;
        // Puck edge (0.075 m) plus its cast (0.025 m) plus the room, less a margin so the exact outer edge is not tested.
        const float Reach = GoalieShape.LinePuckDiameter / 2f + GoalieShape.LinePuckThickness / 2f + GoalieCharacter.GapRoomMetres - 0.015f;

        static readonly Vector3 From = new Vector3(0f, 0f, 3f), To = Vector3.zero;

        /// <summary>A closed box of these half sizes, turned about Z, centred here.</summary>
        static GoalieShape Box(Vector3 centre, Vector3 half, float degreesAboutZ) => Box(Corners(centre, half, Quaternion.Euler(0f, 0f, degreesAboutZ)));

        static Vector3[] Corners(Vector3 centre, Vector3 half, Quaternion turn)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = centre + turn * new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
            return corners;
        }

        /// <summary>How near the box (its edges, sampled every millimetre) comes to the puck's line, which is the Z axis.</summary>
        static float DistanceToLine(Vector3[] corners)
        {
            float best = float.MaxValue;
            for (int i = 0; i < 8; i++)
            for (int bit = 1; bit < 8; bit <<= 1)
            {
                if ((i & bit) != 0) continue;
                Vector3 a = corners[i], b = corners[i | bit];
                int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 0.001f));
                for (int s = 0; s <= steps; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / (float)steps);
                    best = Mathf.Min(best, Mathf.Sqrt(p.x * p.x + p.y * p.y));
                }
            }
            return best;
        }

        /// <summary>The box, turned any way, moved out along `azimuth` until its nearest point is `distance` from the puck's line.</summary>
        static Vector3[] AtDistance(Vector3 half, Quaternion turn, float azimuthDegrees, float distance)
        {
            var direction = new Vector3(Mathf.Cos(azimuthDegrees * Mathf.Deg2Rad), Mathf.Sin(azimuthDegrees * Mathf.Deg2Rad), 0f);
            float s = distance + half.magnitude;
            Vector3[] corners = null;
            for (int i = 0; i < 6; i++)
            {
                corners = Corners(direction * s + new Vector3(0f, 0f, 1.5f), half, turn);
                s += distance - DistanceToLine(corners);
            }
            corners = Corners(direction * s + new Vector3(0f, 0f, 1.5f), half, turn);
            Assert.AreEqual(distance, DistanceToLine(corners), 0.002f, "the box is placed where it was meant to be");
            return corners;
        }

        static readonly Quaternion[] Tilts =
        {
            Quaternion.Euler(30f, 0f, 0f), Quaternion.Euler(0f, 45f, 0f), Quaternion.Euler(60f, 20f, 0f), Quaternion.Euler(25f, 70f, 40f),
            Quaternion.Euler(80f, 0f, 15f), Quaternion.Euler(-45f, -30f, 10f),
        };

        static readonly Vector3[] ThinShapes = { new Vector3(Shaft / 2f, Shaft / 2f, 0.4f), new Vector3(0.15f, Shaft / 2f, Shaft / 2f) };

        [Test]
        public void A_tilted_thin_shaft_or_bar_just_outside_the_room_is_not_found()
        {
            // Nearest point 5 mm beyond the 0.19 m room: the cast's reach is a true sphere, so it must not be found however the shape is tilted.
            const float Beyond = 0.075f + 0.025f + 0.09f + 0.005f;
            var found = new List<string>();
            foreach (Vector3 half in ThinShapes)
            foreach (Quaternion tilt in Tilts)
            for (float azimuth = 0f; azimuth < 360f; azimuth += 22.5f)
                if (GoalieCharacter.HitsPuckOrRoom(Box(AtDistance(half, tilt, azimuth, Beyond)), From, To))
                    found.Add(half + " " + tilt.eulerAngles + " @" + azimuth);
            Assert.AreEqual(0, found.Count, found.Count + " tilted thin shapes outside the room were found: " + string.Join(" | ", found.GetRange(0, Mathf.Min(6, found.Count))));
        }

        [Test]
        public void A_tilted_thin_shaft_or_bar_inside_the_room_is_found()
        {
            const float Within = 0.075f + 0.025f + 0.09f - 0.02f;
            var missed = new List<string>();
            foreach (Vector3 half in ThinShapes)
            foreach (Quaternion tilt in Tilts)
            for (float azimuth = 0f; azimuth < 360f; azimuth += 22.5f)
                if (!GoalieCharacter.HitsPuckOrRoom(Box(AtDistance(half, tilt, azimuth, Within)), From, To))
                    missed.Add(half + " " + tilt.eulerAngles + " @" + azimuth);
            Assert.AreEqual(0, missed.Count, missed.Count + " tilted thin shapes inside the room slipped through: " + string.Join(" | ", missed.GetRange(0, Mathf.Min(6, missed.Count))));
        }

        static GoalieShape Box(Vector3[] corners)
        {
            int[] triangles =
            {
                0, 2, 1, 1, 2, 3, // -z
                4, 5, 6, 5, 7, 6, // +z
                0, 1, 4, 1, 5, 4, // -y
                2, 6, 3, 3, 6, 7, // +y
                0, 4, 2, 2, 4, 6, // -x
                1, 3, 5, 3, 7, 5, // +x
            };
            var shape = new GoalieShape();
            shape.AddMesh(corners, triangles, "Stick");
            return shape;
        }

        [Test]
        public void A_thin_shaft_along_the_puck_s_line_is_found_anywhere_within_the_room()
        {
            var missed = new List<string>();
            int tried = 0;
            for (float x = -Reach; x <= Reach + 1e-4f; x += Step)
            for (float y = -Reach; y <= Reach + 1e-4f; y += Step)
            {
                if (x * x + y * y > Reach * Reach) continue;
                tried++;
                GoalieShape shaft = Box(new Vector3(x, y, 1.5f), new Vector3(Shaft / 2f, Shaft / 2f, 0.5f), 0f);
                if (!GoalieCharacter.HitsPuckOrRoom(shaft, From, To)) missed.Add("(" + x.ToString("0.000") + ", " + y.ToString("0.000") + ")");
            }
            Assert.Greater(tried, 1000);
            Assert.AreEqual(0, missed.Count, missed.Count + " of " + tried + " thin shafts along the line slipped through: " + string.Join(" ", missed.GetRange(0, Mathf.Min(12, missed.Count))));
        }

        [Test]
        public void A_thin_bar_across_the_puck_s_line_is_found_at_any_angle_within_the_room()
        {
            var missed = new List<string>();
            int tried = 0;
            for (float degrees = 0f; degrees < 180f; degrees += 7.5f)
            for (float offset = -Reach; offset <= Reach + 1e-4f; offset += Step)
            {
                tried++;
                Quaternion turn = Quaternion.Euler(0f, 0f, degrees);
                Vector3 centre = turn * new Vector3(0f, offset, 0f) + new Vector3(0f, 0f, 1.5f);
                GoalieShape bar = Box(centre, new Vector3(0.15f, Shaft / 2f, Shaft / 2f), degrees);
                if (!GoalieCharacter.HitsPuckOrRoom(bar, From, To)) missed.Add(degrees.ToString("0.0") + "deg@" + offset.ToString("0.000"));
            }
            Assert.AreEqual(0, missed.Count, missed.Count + " of " + tried + " thin bars slipped through: " + string.Join(" ", missed.GetRange(0, Mathf.Min(12, missed.Count))));
        }

        [Test]
        public void A_thin_shaft_or_bar_just_outside_the_room_is_not_found()
        {
            // The room ends 0.19 m from the line (puck edge 0.075 + its cast 0.025 + GapRoomMetres 0.09): the casts must not reach further (that would shrink the Gaps without a word).
            const float Outside = 0.075f + 0.025f + 0.09f + 0.015f + Shaft / 2f; // the near face 15 mm beyond the room
            for (float degrees = 0f; degrees < 360f; degrees += 45f)
            {
                Quaternion turn = Quaternion.Euler(0f, 0f, degrees);
                Vector3 centre = turn * new Vector3(Outside, 0f, 0f) + new Vector3(0f, 0f, 1.5f);
                GoalieShape shaft = Box(centre, new Vector3(Shaft / 2f, Shaft / 2f, 0.5f), degrees);
                Assert.IsFalse(GoalieCharacter.HitsPuckOrRoom(shaft, From, To), "shaft at " + degrees + " degrees");
                GoalieShape bar = Box(centre, new Vector3(Shaft / 2f, 0.15f, Shaft / 2f), degrees);
                Assert.IsFalse(GoalieCharacter.HitsPuckOrRoom(bar, From, To), "bar at " + degrees + " degrees");
            }
        }

        [Test]
        public void A_thin_shaft_beyond_the_room_is_not_found()
        {
            GoalieShape shaft = Box(new Vector3(0.3f, 0f, 1.5f), new Vector3(Shaft / 2f, Shaft / 2f, 0.5f), 0f);
            Assert.IsFalse(GoalieCharacter.HitsPuckOrRoom(shaft, From, To));
        }
    }
}
