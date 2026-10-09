using HockeyGame.Core;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// Block goalie and Easy mode glow. All six Goalie Poses are built once from the pose table
    /// (Covered Zone = block filling the Zone, Contested Zone = smaller block, Open Zone = nothing);
    /// showing a pose enables its group and disables the rest. No colliders.
    /// </summary>
    public sealed class GoalieView
    {
        const float CoveredFraction = 1f;
        const float ContestedFraction = 0.55f;
        const float BlockDepth = 0.1f;
        const float BlockZ = -0.08f;   // centre; block spans z -0.13..-0.03, in front of the Net Face
        const float GlowZ = 0.06f;     // just behind the Net Face
        static readonly Color GoalieColour = new Color(0.15f, 0.25f, 0.6f);
        static readonly Color GlowColour = new Color(0.3f, 1f, 0.3f);

        readonly GameObject[] _blocks = new GameObject[6];
        readonly GameObject[] _glows = new GameObject[6];
        bool _glowOn;
        int _shown = -1;
        bool _visible;

        public GoalieView(Transform parent)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            foreach (GoaliePose pose in System.Enum.GetValues(typeof(GoaliePose)))
            {
                int i = (int)pose;
                _blocks[i] = new GameObject("Goalie " + pose);
                _blocks[i].transform.SetParent(parent);
                _glows[i] = new GameObject("Glow " + pose);
                _glows[i].transform.SetParent(parent);

                for (int column = 0; column < 3; column++)
                for (int row = 0; row < 3; row++)
                {
                    switch (GoaliePoses.CoverAt(pose, column, row))
                    {
                        case ZoneCover.Covered:
                            Block(_blocks[i].transform, column, row, CoveredFraction, BlockDepth, BlockZ, GoalieColour, null);
                            break;
                        case ZoneCover.Contested:
                            Block(_blocks[i].transform, column, row, ContestedFraction, BlockDepth, BlockZ, GoalieColour, null);
                            break;
                        case ZoneCover.Open:
                            Block(_glows[i].transform, column, row, 0.96f, 0.02f, GlowZ, GlowColour, unlit);
                            break;
                    }
                }
            }
            Apply();
        }

        /// <summary>Show the goalie in the given pose (or hide it when visible is false) and set the Easy mode glow.</summary>
        public void Show(bool visible, GoaliePose pose, bool easyMode)
        {
            if (visible == _visible && (int)pose == _shown && easyMode == _glowOn) return;
            _visible = visible;
            _shown = (int)pose;
            _glowOn = easyMode;
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < _blocks.Length; i++)
            {
                _blocks[i].SetActive(_visible && i == _shown);
                _glows[i].SetActive(_visible && _glowOn && i == _shown);
            }
        }

        static void Block(Transform parent, int column, int row, float fraction, float depth, float z, Color colour, Shader unlit)
        {
            NetFaceMapping.ZoneRectangle(column, row, fraction, out float minX, out float minY, out float maxX, out float maxY);
            var cube = RinkBuilder.Cube(parent, "Zone " + column + "," + row,
                new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, z),
                new Vector3(maxX - minX, maxY - minY, depth), colour);
            if (unlit != null)
            {
                var material = new Material(unlit);
                material.color = colour;
                cube.GetComponent<Renderer>().sharedMaterial = material;
            }
        }
    }
}
