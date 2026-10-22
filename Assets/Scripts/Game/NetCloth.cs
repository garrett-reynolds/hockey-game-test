using System;
using System.Collections.Generic;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The cloth netting that gives on a Goal (#112, spec #107 "Net and netting"): Unity's built-in Cloth on one welded grid
    /// (the sides, back and roof), pinned at the ice, the posts and the crossbar, in the rink's frame (the Net Face at z = 0, the
    /// back at z = +NetDepth). It is looks only: the catch boxes (ShotColliders.Catch) stop the puck and the cloth never pushes
    /// it. The cloth sees the puck through a capsule of two spheres, where the puck was last frame and where it is now (Follow).
    /// The model's own netting is hidden while this one shows.
    /// </summary>
    public sealed class NetCloth : IDisposable
    {
        public const string ObjectName = "Cloth Netting";

        /// <summary>The two spheres the cloth sees the puck by: only for the cloth, the puck never hits them (Track).</summary>
        public const string ProxyName = "Cloth Puck";

        /// <summary>The Hard settings (#102): the netting gives 8 cm (4 cm on the roof, so it does not sag), stiff, damped, 240 Hz.</summary>
        public const float MaxMove = 0.08f, RoofMaxMove = 0.04f, Stretch = 1f, Bend = 1f, Damping = 0.8f, SolverHz = 240f;

        const float W = (float)NetFaceMapping.WidthMetres, H = (float)NetFaceMapping.HeightMetres, D = RinkBuilder.NetDepth;
        const float Cell = 0.06f;      // grid spacing (metres)
        const float HoleSize = 0.05f;  // net holes the texture draws (metres)
        const float PinTolerance = 0.01f;

        readonly List<Renderer> _hidden = new List<Renderer>();
        SphereCollider _was, _now;

        /// <summary>Builds the cloth netting under the rink and hides the model's netting (NetBuilder.FindNetting) if it has one.</summary>
        public NetCloth(Transform rink)
        {
            Transform model = NetBuilder.FindNetting(rink);
            if (model != null)
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                    if (r.enabled) { r.enabled = false; _hidden.Add(r); }

            var go = new GameObject(ObjectName);
            go.transform.SetParent(rink, false);
            var skin = go.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = NetMesh();
            skin.sharedMaterial = NetMaterial();
            skin.updateWhenOffscreen = true; // the cloth moves the vertices, not the bones: the bounds would not follow
            Cloth = go.AddComponent<Cloth>();
            Cloth.useGravity = true;
            Cloth.useTethers = true;
            Cloth.friction = 0.6f;
            Cloth.worldVelocityScale = 0f;
            Cloth.worldAccelerationScale = 0f;
            Cloth.clothSolverFrequency = SolverHz;
            Cloth.stretchingStiffness = Stretch;
            Cloth.bendingStiffness = Bend;
            Cloth.damping = Damping;
            ClothSkinningCoefficient[] co = Cloth.coefficients;
            Vector3[] rest = skin.sharedMesh.vertices; // one cloth vertex per mesh vertex: the grid is welded
            for (int i = 0; i < co.Length && i < rest.Length; i++)
            {
                Vector3 p = rest[i];
                bool pinned = p.y < PinTolerance || p.z < PinTolerance; // the ice, and the front edges at the posts and crossbar
                co[i].maxDistance = pinned ? 0f : p.y > H - PinTolerance ? RoofMaxMove : MaxMove;
                co[i].collisionSphereDistance = float.MaxValue;
            }
            Cloth.coefficients = co;
        }

        /// <summary>The cloth component.</summary>
        public Cloth Cloth { get; }

        /// <summary>Starts watching a puck: its colliders may not touch the cloth's spheres. Call once a Shot, with the puck where it starts.</summary>
        public void Track(Rigidbody puck)
        {
            Release();
            _was = Proxy(puck.position);
            _now = Proxy(puck.position);
            foreach (Collider c in puck.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(_was, c);
                Physics.IgnoreCollision(_now, c);
            }
            Cloth.sphereColliders = new[] { new ClothSphereColliderPair(_was, _now) };
        }

        /// <summary>Call once a frame while the puck is shown: the capsule runs from where the puck was last frame to where it is now. A hidden puck is not followed (a hidden body may read position zero).</summary>
        public void Follow(Vector3 puck)
        {
            if (_now == null) return;
            _was.transform.position = _now.transform.position;
            _now.transform.position = puck;
        }

        /// <summary>The puck's capsule, the two sphere centres (last frame, this frame); null before a puck is tracked.</summary>
        public (Vector3 Was, Vector3 Now)? Capsule => _now == null ? ((Vector3, Vector3)?)null : (_was.transform.position, _now.transform.position);

        void Release()
        {
            if (_was != null) UnityEngine.Object.DestroyImmediate(_was.gameObject);
            if (_now != null) UnityEngine.Object.DestroyImmediate(_now.gameObject);
            _was = _now = null;
            if (Cloth != null) Cloth.sphereColliders = new ClothSphereColliderPair[0];
        }

        const float ProxyRadius = 0.075f; // the netting's give is tuned to this radius

        SphereCollider Proxy(Vector3 at)
        {
            var go = new GameObject(ProxyName);
            go.transform.SetParent(Cloth.transform.parent, false);
            go.transform.position = at;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = ProxyRadius;
            return sphere;
        }

        /// <summary>Removes the cloth and shows the model's netting again.</summary>
        public void Dispose()
        {
            Release();
            foreach (Renderer r in _hidden) if (r != null) r.enabled = true;
            _hidden.Clear();
            if (Cloth == null) return;
            // The mesh, material and "Net holes" texture were made with the cloth (#158): free them with it.
            var skin = Cloth.GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = skin != null ? skin.sharedMesh : null;
            Material material = skin != null ? skin.sharedMaterial : null;
            Texture holes = material != null ? material.mainTexture : null;
            UnityEngine.Object.DestroyImmediate(Cloth.gameObject);
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            if (holes != null) UnityEngine.Object.DestroyImmediate(holes);
        }

        /// <summary>One welded grid: the left side, back and right side as one strip around the base, and the roof sharing its top row.</summary>
        static Mesh NetMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var index = new Dictionary<Vector3Int, int>();

            int Vertex(Vector3 p, Vector2 uv)
            {
                var key = new Vector3Int(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f));
                if (index.TryGetValue(key, out int i)) return i;
                index[key] = verts.Count;
                verts.Add(p);
                uvs.Add(uv / HoleSize);
                return verts.Count - 1;
            }

            void Quad(int a, int b, int c, int d) => tris.AddRange(new[] { a, b, c, a, c, d });

            int nW = Mathf.CeilToInt(W / Cell), nD = Mathf.CeilToInt(D / Cell), nH = Mathf.CeilToInt(H / Cell);
            var path = new List<Vector2>(); // the strip around the base: (-W/2, 0) -> (-W/2, D) -> (W/2, D) -> (W/2, 0)
            for (int i = 0; i <= nD; i++) path.Add(new Vector2(-W / 2f, D * i / nD));
            for (int i = 1; i <= nW; i++) path.Add(new Vector2(-W / 2f + W * i / nW, D));
            for (int i = nD - 1; i >= 0; i--) path.Add(new Vector2(W / 2f, D * i / nD));
            float along = 0f;
            var strip = new int[path.Count, nH + 1];
            for (int u = 0; u < path.Count; u++)
            {
                if (u > 0) along += (path[u] - path[u - 1]).magnitude;
                for (int v = 0; v <= nH; v++)
                    strip[u, v] = Vertex(new Vector3(path[u].x, H * v / nH, path[u].y), new Vector2(along, H * v / nH));
            }
            for (int u = 0; u < path.Count - 1; u++)
                for (int v = 0; v < nH; v++)
                    Quad(strip[u, v], strip[u, v + 1], strip[u + 1, v + 1], strip[u + 1, v]);
            var roof = new int[nW + 1, nD + 1]; // its edges land on the strip's top row: the same vertices
            for (int i = 0; i <= nW; i++)
                for (int k = 0; k <= nD; k++)
                    roof[i, k] = Vertex(new Vector3(-W / 2f + W * i / nW, H, D * k / nD), new Vector2(W * i / nW, D * k / nD));
            for (int i = 0; i < nW; i++)
                for (int k = 0; k < nD; k++)
                    Quad(roof[i, k], roof[i, k + 1], roof[i + 1, k + 1], roof[i + 1, k]);

            var mesh = new Mesh { name = "Cloth Netting" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material NetMaterial()
        {
            const int S = 32, Cord = 6;
            var holes = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "Net holes", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    holes.SetPixel(x, y, x < Cord || y < Cord ? Color.white : new Color(1f, 1f, 1f, 0f));
            holes.Apply();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var material = unlit != null ? new Material(unlit) : new Material(Shader.Find("Sprites/Default"));
            material.name = "Cloth Netting";
            material.mainTexture = holes;
            material.color = new Color(0.38f, 0.42f, 0.46f); // dark cord remains clear against the pale ice in the board replay
            if (material.HasProperty("_Cutoff")) { material.SetFloat("_AlphaClip", 1f); material.SetFloat("_Cutoff", 0.5f); material.EnableKeyword("_ALPHATEST_ON"); }
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f); // both faces
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", holes);
            return material;
        }
    }
}
