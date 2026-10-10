using UnityEngine;

namespace HockeyGame
{
    /// <summary>Helpers for scene objects that work both in Play mode and in edit mode (the EditMode tests).</summary>
    public static class SceneObjects
    {
        /// <summary>
        /// Destroys o in Play mode (Object.Destroy: gone at the end of the frame) and at once in edit mode, where
        /// Object.Destroy is not allowed (Object.DestroyImmediate).
        /// </summary>
        public static void DestroySafely(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
