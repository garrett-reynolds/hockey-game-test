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
        const float BlockDepth = 0.1f;
        const float BlockZ = -0.08f;   // centre; block spans z -0.13..-0.03, in front of the Net Face
        const float GlowZ = 0.06f;     // just behind the Net Face
        static readonly Color GoalieColour = new Color(0.15f, 0.25f, 0.6f);
        static readonly Color GlowColour = new Color(0.3f, 1f, 0.3f);

        readonly GameObject[] _blocks = new GameObject[6];
        readonly GameObject[] _glows = new GameObject[6];
        readonly System.Collections.Generic.List<SetBlock>[] _setBlocks = new System.Collections.Generic.List<SetBlock>[6];
        float _set = 1f;
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
                _setBlocks[i] = new System.Collections.Generic.List<SetBlock>();

                for (int column = 0; column < 3; column++)
                for (int row = 0; row < 3; row++)
                {
                    switch (GoaliePoses.CoverAt(pose, column, row))
                    {
                        case ZoneCover.Covered:
                            {
                                NetFaceMapping.ZoneRectangle(column, row, CoveredFraction, out float a, out float b, out float c, out float d);
                                var block = Block(_blocks[i].transform, column, row, a, b, c, d, BlockDepth, BlockZ, GoalieColour, null);
                                _setBlocks[i].Add(new SetBlock(block, GoalieSet.Neutral(column, row), new GoalieSet.Rect(a, b, c, d)));
                            }
                            break;
                        case ZoneCover.Contested:
                            {
                                NetFaceMapping.ContestedRectangle(column, row,
                                    IsOpen(pose, column - 1, row), IsOpen(pose, column + 1, row),
                                    IsOpen(pose, column, row - 1), IsOpen(pose, column, row + 1),
                                    out float a, out float b, out float c, out float d);
                                var block = Block(_blocks[i].transform, column, row, a, b, c, d, BlockDepth, BlockZ, GoalieColour, null);
                                _setBlocks[i].Add(new SetBlock(block, GoalieSet.Neutral(column, row), new GoalieSet.Rect(a, b, c, d)));
                            }
                            break;
                        case ZoneCover.Open:
                            {
                                // Neutral-stance block that shrinks away as the goalie sets; hidden once set.
                                var n = GoalieSet.Neutral(column, row);
                                var settle = Block(_blocks[i].transform, column, row, n.MinX, n.MinY, n.MaxX, n.MaxY, BlockDepth, BlockZ, GoalieColour, null);
                                settle.name += " (setting)";
                                _setBlocks[i].Add(new SetBlock(settle, n, GoalieSet.OpenTarget(column, row)));
                                NetFaceMapping.ZoneRectangle(column, row, 0.96f, out float a, out float b, out float c, out float d);
                                Block(_glows[i].transform, column, row, a, b, c, d, 0.02f, GlowZ, GlowColour, unlit);
                            }
                            break;
                    }
                }
            }
            Apply();
            for (int i = 0; i < _setBlocks.Length; i++) PlaceSetBlocks(i);
        }

        /// <summary>How far the goalie has set into the pose: 0 = neutral stance, 1 = fully set (the default).</summary>
        public void SetProgress(float progress)
        {
            if (Mathf.Approximately(progress, _set)) return;
            _set = Mathf.Clamp01(progress);
            for (int i = 0; i < _setBlocks.Length; i++) PlaceSetBlocks(i);
        }

        void PlaceSetBlocks(int pose)
        {
            foreach (var block in _setBlocks[pose])
            {
                var r = GoalieSet.Lerp(block.Neutral, block.Pose, _set);
                float w = r.MaxX - r.MinX, h = r.MaxY - r.MinY;
                bool show = w > 0.005f && h > 0.005f;
                block.Cube.SetActive(show);
                if (!show) continue;
                block.Cube.transform.position = new Vector3((r.MinX + r.MaxX) / 2f, (r.MinY + r.MaxY) / 2f, BlockZ);
                block.Cube.transform.localScale = new Vector3(w, h, BlockDepth);
            }
        }

        readonly struct SetBlock
        {
            public readonly GameObject Cube;
            public readonly GoalieSet.Rect Neutral, Pose;
            public SetBlock(GameObject cube, GoalieSet.Rect neutral, GoalieSet.Rect pose) { Cube = cube; Neutral = neutral; Pose = pose; }
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

        static bool IsOpen(GoaliePose pose, int column, int row)
        {
            return column >= 0 && column < 3 && row >= 0 && row < 3 && GoaliePoses.CoverAt(pose, column, row) == ZoneCover.Open;
        }

        static GameObject Block(Transform parent, int column, int row, float minX, float minY, float maxX, float maxY, float depth, float z, Color colour, Shader unlit)
        {
            var cube = RinkBuilder.Cube(parent, "Zone " + column + "," + row,
                new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, z),
                new Vector3(maxX - minX, maxY - minY, depth), colour);
            if (unlit != null)
            {
                var material = new Material(unlit);
                material.color = colour;
                cube.GetComponent<Renderer>().sharedMaterial = material;
            }
            return cube;
        }
    }
}
