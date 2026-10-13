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
        static GoalieShape Box(Vector3 centre, Vector3 half, float degreesAboutZ)
        {
            Quaternion turn = Quaternion.Euler(0f, 0f, degreesAboutZ);
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = centre + turn * new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
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
