using NUnit.Framework;
using UnityEngine;

namespace HockeyGame.Core.Tests
{
    /// <summary>The goalie's reach shape (GoalieShape).</summary>
    public class GoalieShapeTests
    {
        /// <summary>A square of side 2 m centred on centre, facing `facing`.</summary>
        static void AddSquare(GoalieShape shape, Vector3 centre, Vector3 facing, string piece = null)
        {
            Quaternion q = Quaternion.LookRotation(-facing);
            Vector3 a = centre + q * new Vector3(-1f, -1f, 0f), b = centre + q * new Vector3(1f, -1f, 0f);
            Vector3 c = centre + q * new Vector3(1f, 1f, 0f), d = centre + q * new Vector3(-1f, 1f, 0f);
            shape.AddMesh(new[] { a, b, c, d }, new[] { 0, 1, 2, 0, 2, 3 }, piece);
        }

        [Test]
        public void A_cleared_shape_holds_nothing_until_filled_again()
        {
            var wall = new GoalieShape();
            AddSquare(wall, Vector3.zero, Vector3.back, "Wall");
            var shape = new GoalieShape();
            AddSquare(shape, Vector3.zero, Vector3.back, "Wall");
            shape.AddPlaced(wall, Matrix4x4.Translate(Vector3.forward * 0.5f));
            Vector3 from = new Vector3(0f, 0f, -2f), to = new Vector3(0f, 0f, 2f);
            Assert.IsTrue(shape.Cast(from, to, 0.01f, out _, out _));

            shape.Clear();
            Assert.IsFalse(shape.Cast(from, to, 0.01f, out _, out _), "no mesh or placed shape left");
            Assert.IsFalse(shape.NearAnyMesh(from, to, 0.01f));

            shape.AddPlaced(wall, Matrix4x4.Translate(Vector3.forward * 0.5f));
            Assert.IsTrue(shape.Cast(from, to, 0.01f, out Vector3 at, out _, new[] { "Wall" }));
            Assert.AreEqual(0.49f, at.z, 1e-3f, "only the wall placed again, half a metre in");
        }
    }
}
