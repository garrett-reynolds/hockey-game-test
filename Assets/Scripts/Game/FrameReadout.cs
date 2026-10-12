using System.Globalization;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The dev-only frame readout (#85): the GPU and CPU (main thread) milliseconds of a recent frame, from the
    /// FrameTimingManager, in the bottom-right screen corner (clear of the Score Box, top right). Only in the Editor and development builds (AddTo); a normal build has none.
    /// Times the platform does not measure (often the GPU in the Editor) show as n/a.
    /// </summary>
    public sealed class FrameReadout : MonoBehaviour
    {
        readonly FrameTiming[] _timing = new FrameTiming[1];
        string _text = Text(double.NaN, double.NaN);
        GUIStyle _style;

        /// <summary>Adds the readout to target in the Editor and development builds (Debug.isDebugBuild); a normal build gets none.</summary>
        public static void AddTo(GameObject target) => AddTo(target, Debug.isDebugBuild);

        /// <summary>Adds the readout to target only when debugBuild (the Editor or a development build).</summary>
        public static void AddTo(GameObject target, bool debugBuild)
        {
            if (debugBuild) target.AddComponent<FrameReadout>();
        }

        /// <summary>The readout's line: each time in ms to one decimal, or n/a when not measured (0, negative or NaN).</summary>
        public static string Text(double cpuMs, double gpuMs) => "CPU " + Ms(cpuMs) + "   GPU " + Ms(gpuMs);

        static string Ms(double ms) => ms > 0 ? ms.ToString("0.0", CultureInfo.InvariantCulture) + " ms" : "n/a";

        void Update()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
                _text = Text(_timing[0].cpuMainThreadFrameTime, _timing[0].gpuFrameTime);
        }

        void OnGUI()
        {
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.LowerRight, normal = { textColor = Color.white } };
            GUI.Label(AreaOn(Screen.width, Screen.height), _text, _style);
        }

        /// <summary>Where the readout is drawn on a screen this size: the bottom-right corner, clear of the Score Box (top right).</summary>
        public static Rect AreaOn(float width, float height) => new Rect(width - 330, height - 40, 320, 30);
    }
}
