using UnityEngine;
using UnityEngine.UIElements;

namespace HockeyGame
{
    /// <summary>
    /// A slanted flat shape for look C (UI Toolkit USS cannot skew or rotate a box): a parallelogram that leans like italic
    /// type, or a long stripe turned by an angle about a pivot (the page's header stripes, as in the #137 prototype).
    /// Its colour is the element's USS `color`, so the colours stay in Resources/UI/LookC.uss. Text goes in a sibling label,
    /// so it stays upright.
    /// </summary>
    public sealed class SlantShape : VisualElement
    {
        readonly bool _stripe;
        readonly float _slant;                 // parallelogram: how far the top edge sits right of the bottom edge, px
        readonly float _y, _thickness, _degrees; // stripe: top edge, thickness, turn (negative = right end up)
        readonly Vector2 _pivot;

        SlantShape(string classes, bool stripe, float slant, float y, float thickness, float degrees, Vector2 pivot)
        {
            _stripe = stripe;
            _slant = slant;
            _y = y;
            _thickness = thickness;
            _degrees = degrees;
            _pivot = pivot;
            foreach (var c in classes.Split(' ')) AddToClassList(c);
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            generateVisualContent += Draw;
        }

        /// <summary>A parallelogram filling the element's box, leaning right by slantPx at the top.</summary>
        public static SlantShape Parallelogram(string classes, float slantPx) =>
            new SlantShape(classes, false, slantPx, 0, 0, 0, Vector2.zero);

        /// <summary>A stripe across the element's box: from 200 px left of it to 200 px right of its width, turned about the pivot.</summary>
        public static SlantShape Stripe(string classes, float y, float thickness, float degrees, Vector2 pivot) =>
            new SlantShape(classes, true, 0, y, thickness, degrees, pivot);

        /// <summary>The corners of a stripe: a box turned by degrees about the pivot (screen y points down, so a negative turn lifts the right end).</summary>
        public static Vector2[] StripeCorners(float left, float right, float y, float thickness, float degrees, Vector2 pivot)
        {
            float rad = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            Vector2 Turn(float x, float yy)
            {
                float dx = x - pivot.x, dy = yy - pivot.y;
                return new Vector2(pivot.x + dx * cos - dy * sin, pivot.y + dx * sin + dy * cos);
            }
            return new[] { Turn(left, y), Turn(right, y), Turn(right, y + thickness), Turn(left, y + thickness) };
        }

        /// <summary>The corners of a parallelogram in a w x h box, leaning right by slant at the top.</summary>
        public static Vector2[] ParallelogramCorners(float w, float h, float slant) =>
            new[] { new Vector2(slant, 0), new Vector2(w, 0), new Vector2(w - slant, h), new Vector2(0, h) };

        void Draw(MeshGenerationContext mgc)
        {
            Rect r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return;
            Vector2[] corners = _stripe
                ? StripeCorners(-200f, r.width + 200f, _y, _thickness, _degrees, _pivot)
                : ParallelogramCorners(r.width, r.height, _slant);
            var painter = mgc.painter2D;
            painter.fillColor = resolvedStyle.color;
            painter.BeginPath();
            painter.MoveTo(corners[0]);
            for (int i = 1; i < corners.Length; i++) painter.LineTo(corners[i]);
            painter.ClosePath();
            painter.Fill();
        }
    }
}
