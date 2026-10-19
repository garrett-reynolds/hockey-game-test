using System.Collections.Generic;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// The goalie's cloth. The jersey is its own model (Resources/Models/goalie-jersey.fbx, Tools/make_goalie_jersey.py): a boxy
    /// goalie cut with real UVs, bound to the same bones as goalie-dressing.fbx by bone name. Its colour and print (stripes,
    /// collar, crest, number 31) are painted once into one texture. No frame work and no extra draw pass.
    /// Texture layout (1024 px, 650 px per metre), the same numbers as the Blender script:
    /// torso U 0..1 round the body (front centre .25, back .75), V = (z - 0.90) / 0.70 * 0.4375;
    /// left sleeve U 0..0.40, right sleeve U 0.5..0.90, V = 0.5 + d / 0.535 * 0.34 (d = metres from the sleeve's inner end).
    /// </summary>
    static class GoalieClothSurface
    {
        // The jersey: classic pattern (two hem stripes, two sleeve stripes, white yoke, laced V collar), medium weave and folds.
        const float WeaveNormal = 1.0f;     // detail (weave) normal scale
        const float WeaveAlbedo = 0.8f;     // detail (weave) albedo scale
        const float FoldShadow = 0.45f;     // fold shadow (AO) strength
        const float FoldSlope = 8f;         // exaggerates the fold height for the normal map only
        const int FoldSize = 512;            // fold normal map (the folds are broad, so half the albedo resolution is enough)
        const float WeavePitch = 0.005f;     // metres between pinholes
        const int WeaveCells = 16;           // pinholes across the 256 px detail tile
        const float MetresPerUv = 1.6f;      // the jersey UV layout: 650 px per metre on a 1024 map

        // Play mode keeps statics (no domain reload) but destroys runtime-made objects: a cached entry can be a dead Unity object.
        // Look-ups go through CachedMaterial so a dead entry is rebuilt (the magenta jersey of #220).
        static readonly Dictionary<(Material, bool), Material> Materials = new Dictionary<(Material, bool), Material>();
        static Texture2D DetailAlbedo, DetailNormal, DetailMask;
        const int Size = 1024;
        const float Circumference = 1.56f;   // metres round the torso, for the print
        const float HemZ = 0.90f;

        public static void Apply(SkinnedMeshRenderer[] body, Animator animator)
        {
            foreach (var r in body)
            {
                if (r.name != "Jersey" && r.name != "Pants" && r.name != "Socks") continue;
                bool jersey = r.name == "Jersey";
                var assigned = r.sharedMaterials;
                if (jersey)
                {
                    if (!ReplaceJerseyMesh(r, animator)) continue;
                    Color white = assigned.Length > 1 && assigned[1] != null ? assigned[1].color : new Color(0.93f, 0.93f, 0.92f);
                    assigned = new[] { JerseyMaterial(assigned[0], white) };
                }
                else
                {
                    for (int i = 0; i < assigned.Length; i++) assigned[i] = Matte(assigned[i]);
                }
                r.sharedMaterials = assigned;
            }
        }

        /// <summary>Swaps the imported jersey for the goalie-cut jersey: same bone names, so the skin follows the same rig.</summary>
        static bool ReplaceJerseyMesh(SkinnedMeshRenderer r, Animator animator)
        {
            var asset = Resources.Load<GameObject>("Models/goalie-jersey");
            SkinnedMeshRenderer source = asset == null ? null : asset.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (source == null || source.sharedMesh == null) throw new System.InvalidOperationException("Models/goalie-jersey is missing or has no skinned mesh.");
            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in animator.GetComponentsInChildren<Transform>(true)) if (!byName.ContainsKey(t.name)) byName.Add(t.name, t);
            Transform[] sourceBones = source.bones;
            var bones = new Transform[sourceBones.Length];
            for (int i = 0; i < bones.Length; i++)
                if (sourceBones[i] == null || !byName.TryGetValue(sourceBones[i].name, out bones[i]))
                    throw new System.InvalidOperationException("The goalie rig has no bone '" + (sourceBones[i] == null ? "?" : sourceBones[i].name) + "' for the jersey.");
            r.sharedMesh = source.sharedMesh;
            r.bones = bones;
            Bounds bounds = source.sharedMesh.bounds;
            bounds.Expand(0.4f);
            r.localBounds = bounds;
            return true;
        }

        static Material Matte(Material original)
        {
            if (Materials.TryGetValue((original, false), out Material cached) && cached != null) return cached;   // a dead entry (null) is rebuilt
            var cloth = new Material(original) { name = original.name + " (matte cloth)" };
            if (cloth.HasProperty("_Smoothness")) cloth.SetFloat("_Smoothness", 0f);
            Materials[(original, false)] = cloth;
            return cloth;
        }

        static Material JerseyMaterial(Material blue, Color white)
        {
            if (Materials.TryGetValue((blue, true), out Material cached) && cached != null) return cached;   // a dead entry (null) is rebuilt
            var cloth = new Material(blue) { name = blue.name + " (goalie jersey)" };
            // Matte cloth: no plastic highlight; a little smoothness only gives the soft grazing-angle sheen from the environment.
            if (cloth.HasProperty("_Smoothness")) cloth.SetFloat("_Smoothness", 0.06f);
            if (cloth.HasProperty("_Metallic")) cloth.SetFloat("_Metallic", 0f);
            if (cloth.HasProperty("_SpecularHighlights")) cloth.SetFloat("_SpecularHighlights", 0f);
            cloth.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            // The print uses the existing lit base-map path, so player builds need no new shader variant.
            Material normalVariant = Resources.Load<Material>("Lighting/GoalieJerseyCloth");
            if (normalVariant == null || !normalVariant.IsKeywordEnabled("_NORMALMAP"))
                throw new System.InvalidOperationException("The jersey normal-map material variant is missing.");
            cloth.shader = normalVariant.shader;
            cloth.EnableKeyword("_NORMALMAP");
            Texture2D foldNormal;
            Texture2D baked = Bake(blue.color, white, out foldNormal);
            cloth.mainTexture = baked;
            cloth.color = Color.white;
            cloth.mainTextureScale = Vector2.one;
            cloth.mainTextureOffset = Vector2.zero;
            cloth.SetTexture("_BumpMap", foldNormal);          // baked fold normals (drape, elbow and armpit bunching, hem sag)
            cloth.SetFloat("_BumpScale", 1f);
            // The weave: a tiling pinhole air-knit detail map (normal + albedo), 5 mm between holes.
            cloth.EnableKeyword("_DETAIL_SCALED");   // the scaled path: _DetailAlbedoMapScale only works with it
            cloth.SetTexture("_DetailAlbedoMap", WeaveAlbedoMap());
            cloth.SetTexture("_DetailNormalMap", WeaveNormalMap());
            float tiling = MetresPerUv / (WeavePitch * WeaveCells);
            cloth.SetTextureScale("_DetailAlbedoMap", new Vector2(tiling, tiling));
            cloth.SetTextureScale("_DetailNormalMap", new Vector2(tiling, tiling));
            cloth.SetTexture("_DetailMask", WeaveMask());
            cloth.SetFloat("_DetailAlbedoMapScale", WeaveAlbedo);
            cloth.SetFloat("_DetailNormalMapScale", WeaveNormal);
            // Fill light: the forward-leaning chest gets almost no light in the arena. A small emission of the same painted texture
            // lifts the shadows (and the crest) without a brighter blue and without an extra pass.
            cloth.EnableKeyword("_EMISSION");
            cloth.SetTexture("_EmissionMap", baked);
            cloth.SetColor("_EmissionColor", Color.white * 0.16f);
            Materials[(blue, true)] = cloth;
            return cloth;
        }

        /// <summary>Pinhole air-knit height field on a 256 px tile: 16 x 16 cells, rows staggered, a pinhole in each, a raised rim, a fine knit grain.</summary>
        static float WeaveHeight(int x, int y, out float hole)
        {
            const int cell = 16;
            x = ((x % 256) + 256) % 256; y = ((y % 256) + 256) % 256;
            int row = y / cell;
            float cx = ((x + ((row & 1) == 1 ? cell / 2 : 0)) % cell) - (cell - 1) * 0.5f;
            float cy = (y % cell) - (cell - 1) * 0.5f;
            float d = Mathf.Sqrt((cx / 4.4f) * (cx / 4.4f) + (cy / 3.1f) * (cy / 3.1f));   // 1 = edge of the pinhole
            hole = 1f - Mathf.SmoothStep(0.65f, 1.05f, d);
            float rim = Mathf.Exp(-(d - 1.25f) * (d - 1.25f) * 6f);                          // raised yarn round the hole
            float grain = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (x + y) / 8f);              // diagonal knit ridges
            float ridge = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (x - y) / 16f);
            return -1.2f * hole + 0.5f * rim + 0.28f * grain + 0.12f * ridge;
        }

        /// <summary>Detail mask (alpha): 1 on the fabric, fading to 0 over the shoulder slope and the neck, where the torso rings pinch
        /// and the UV layout would stretch the weave into rings.</summary>
        static Texture2D WeaveMask()
        {
            if (DetailMask != null) return DetailMask;
            const int side = 256;
            var pixels = new byte[side * side];
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float v = (y + 0.5f) / side;
                float m = 1f;
                if (v < 0.48f)   // the torso island incl. the neck stand (V up to about 0.455)
                {
                    float z = HemZ + v / 0.4375f * 0.70f;
                    m = 1f -Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.545f, 1.575f, z));
                }
                pixels[y * side + x] = (byte)Mathf.RoundToInt(m * 255f);
            }
            DetailMask = new Texture2D(side, side, TextureFormat.Alpha8, true, true)
            {
                name = "Goalie jersey weave mask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            DetailMask.SetPixelData(pixels, 0);
            DetailMask.Apply(true, true);
            return DetailMask;
        }

        static Texture2D WeaveNormalMap()
        {
            if (DetailNormal != null) return DetailNormal;
            const int side = 256;
            var pixels = new Color32[side * side];
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float hole;
                float dx = WeaveHeight(x + 1, y, out hole) - WeaveHeight(x - 1, y, out hole);
                float dy = WeaveHeight(x, y + 1, out hole) - WeaveHeight(x, y - 1, out hole);
                Vector3 n = new Vector3(-dx * 0.9f, -dy * 0.9f, 1f).normalized;
                pixels[y * side + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            DetailNormal = new Texture2D(side, side, TextureFormat.RGBA32, true, true)
            {
                name = "Goalie jersey weave normal", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8
            };
            DetailNormal.SetPixels32(pixels);
            DetailNormal.Apply(true, true);
            return DetailNormal;
        }

        static Texture2D WeaveAlbedoMap()
        {
            if (DetailAlbedo != null) return DetailAlbedo;
            const int side = 256;
            var pixels = new Color32[side * side];
            var values = new float[side * side];
            float mean = 0f;
            for (int y = 0; y < side; y++)
            for (int x = 0; x < side; x++)
            {
                float hole;
                float h = WeaveHeight(x, y, out hole);
                float g = 0.50f + 0.09f * h - 0.30f * hole;          // pinholes show the dark undershirt
                values[y * side + x] = g;
                mean += g;
            }
            mean /= values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                // The average of the tile is 0.5 (unchanged colour with the x2 detail), so the jersey keeps its team blue at a distance.
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(values[i] - mean + 0.5f) * 255f);
                pixels[i] = new Color32(b, b, b, 255);
            }
            DetailAlbedo = new Texture2D(side, side, TextureFormat.RGBA32, true, false)
            {
                name = "Goalie jersey weave albedo", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8
            };
            DetailAlbedo.SetPixels32(pixels);
            DetailAlbedo.Apply(true, true);
            return DetailAlbedo;
        }

        /// <summary>Soft fabric fold height in metres at a texture position (0 outside the jersey islands). Torso: vertical drape folds
        /// (strongest at the hem), a sag ripple at the hem, creases radiating from each armpit. Sleeves: bunching at the elbow,
        /// a lengthwise drape, a cuff bunch and a pull at the shoulder cap. Everything is periodic round the body / sleeve.</summary>
        static float FoldH(float u, float v, out float armpit)
        {
            armpit = 0f;
            const float tau = 2f * Mathf.PI;
            if (v < 0.4375f)
            {
                float z = HemZ + v / 0.4375f * 0.70f;
                float amp = (0.55f + 0.45f * Mathf.Clamp01((1.55f - z) / 0.65f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.50f, 1.57f, z)));   // none on the pinched shoulder slope
                float warp = 0.9f * Mathf.Sin(z * 6f + u * tau * 3f);
                // Broad soft drape folds (3 to 9 round the body), a few millimetres deep.
                float drape = 0.50f * Mathf.Sin(tau * 5f * u + 1.3f + warp) + 0.30f * Mathf.Sin(tau * 9f * u + 0.4f + 0.7f * Mathf.Sin(z * 9f)) + 0.35f * Mathf.Sin(tau * 3f * u + 2.1f);
                float hh = 0.0075f * amp * drape;
                float h = z - HemZ;
                hh += 0.0022f * Mathf.Exp(-h / 0.08f) * Mathf.Sin(tau * 19f * u + 3f * Mathf.Sin(tau * 2f * u));   // the hem sags in small ripples
                for (int side = 0; side < 2; side++)                                                             // the two armpits, at the body's sides
                {
                    float du = u - 0.5f * side;
                    if (du > 0.5f) du -= 1f;
                    float dx = du * Circumference, dz = z - 1.40f;
                    float r = Mathf.Sqrt(dx * dx + dz * dz);
                    if (r < 0.3f)
                    {
                        float ang = Mathf.Atan2(dz, dx);
                        float k = Mathf.Exp(-r / 0.11f);
                        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.46f, 1.54f, z));   // not on the shoulder slope
                        hh += 0.0050f * k * Mathf.Sin(ang * 9f + r * 14f) * Mathf.Clamp01(r / 0.03f) * fade;
                        armpit = Mathf.Max(armpit, Mathf.Exp(-r / 0.06f));
                    }
                }
                return hh;
            }
            if (v > 0.49f && v < 0.84f)
            {
                float local;
                if (u < 0.40f) local = u / 0.40f;
                else if (u > 0.5f && u < 0.90f) local = (u - 0.5f) / 0.40f;
                else return 0f;
                float d = (v - 0.5f) / 0.34f * 0.535f;
                float angle = tau * local;
                float elbow = Mathf.Exp(-((d - 0.30f) / 0.085f) * ((d - 0.30f) / 0.085f));
                float hh = 0.0040f * elbow * Mathf.Sin(tau * d / 0.034f + 2.2f * Mathf.Sin(angle * 2f + d * 10f) + angle * 1.5f) * (0.65f + 0.35f * Mathf.Sin(angle - 1.2f));
                hh += 0.0020f * (0.5f + d) * Mathf.Sin(tau * 5f * local + 1.1f + 3f * d) + 0.0012f * Mathf.Sin(tau * 9f * local + 4f * d);
                float cuff = Mathf.Exp(-((d - 0.52f) / 0.03f) * ((d - 0.52f) / 0.03f));
                hh += 0.0030f * cuff * Mathf.Sin(angle * 6f);
                hh += 0.0030f * Mathf.Exp(-d / 0.06f) * Mathf.Sin(angle * 5f + 1f);
                armpit = Mathf.Exp(-d / 0.05f) * (0.5f + 0.5f * Mathf.Sin(angle - 1.57f));   // the shoulder joint of the sleeve is in shade
                return hh;
            }
            return 0f;
        }

        static Texture2D Bake(Color blue, Color white, out Texture2D foldNormal)
        {
            // 1. Fold heights on the half-resolution grid (drives the fold normals and the baked shade).
            var height = new float[FoldSize * FoldSize];
            var armpitShade = new float[FoldSize * FoldSize];
            for (int y = 0; y < FoldSize; y++)
            for (int x = 0; x < FoldSize; x++)
            {
                float u = (x + 0.5f) / FoldSize, v = (y + 0.5f) / FoldSize, a;
                float hh = FoldH(u, v, out a);
                // The stitched crest and number stand a little proud of the fabric.
                if (v < 0.4375f)
                {
                    float wc = 0f, dc = 0f, tc = 0f;
                    Torso(u, v, ref wc, ref dc, ref tc);
                    hh += 0.0011f * tc;
                }
                height[y * FoldSize + x] = hh;
                armpitShade[y * FoldSize + x] = a;
            }
            // 2. Fold normal map.
            var normals = new Color32[FoldSize * FoldSize];
            float step = MetresPerUv / FoldSize;   // metres between neighbouring height samples
            for (int y = 0; y < FoldSize; y++)
            for (int x = 0; x < FoldSize; x++)
            {
                int xm = Mathf.Max(0, x - 1), xp = Mathf.Min(FoldSize - 1, x + 1), ym = Mathf.Max(0, y - 1), yp = Mathf.Min(FoldSize - 1, y + 1);
                float dx = (height[y * FoldSize + xp] - height[y * FoldSize + xm]) / ((xp - xm) * step);
                float dy = (height[yp * FoldSize + x] - height[ym * FoldSize + x]) / ((yp - ym) * step);
                var n = new Vector3(-dx * FoldSlope, -dy * FoldSlope, 1f).normalized;
                normals[y * FoldSize + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            foldNormal = new Texture2D(FoldSize, FoldSize, TextureFormat.RGBA32, true, true)
            {
                name = "Goalie jersey fold normals", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
            foldNormal.SetPixels32(normals);
            foldNormal.Apply(true, true);
            // 3. Painted colour with the baked fold shade (ambient occlusion: valleys dark, ridges a touch light).
            var pixels = new Color32[Size * Size];
            Color undershirt = new Color(0.05f, 0.08f, 0.18f, 1f);
            Color thread = new Color(1f, 0.91f, 0.65f);   // warm light thread for the crest and the number
            Color outline = new Color(0.015f, 0.03f, 0.08f);
            float shadow = FoldShadow;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                int fi = (y / 2) * FoldSize + (x / 2);
                float hh = height[fi];
                float valley = Mathf.Clamp01(-hh / 0.006f), ridge = Mathf.Clamp01(hh / 0.006f);
                float shade = 1f - shadow * valley + 0.05f * ridge - 0.22f * armpitShade[fi];
                Color c = blue;
                float whiteCover = 0f, darkCover = 0f, threadCover = 0f, outlineCover = 0f;
                if (v < 0.47f) Torso(u, v, ref whiteCover, ref darkCover, ref threadCover);
                else if (v > 0.49f && v < 0.84f) Sleeve(u, v, ref whiteCover);
                if (v < 0.4375f && threadCover < 0.5f) outlineCover = Outline(u, v);
                c = Color.Lerp(c, white, whiteCover);
                c = Color.Lerp(c, undershirt, darkCover);
                c *= shade;
                c = Color.Lerp(c, outline, outlineCover);
                c = Color.Lerp(c, thread * Mathf.Lerp(1f, shade, 0.5f), threadCover);   // the thread stays lighter in the folds
                c.a = 1f;
                pixels[y * Size + x] = c;
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "Goalie jersey - stripes, crest, 31, fold shade", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>A dark stitched border (3 mm) round the crest and the number, so the thread reads against the blue.</summary>
        static float Outline(float u, float v)
        {
            float z = HemZ + v / 0.4375f * 0.70f;
            float front = (u - 0.25f) * Circumference;
            if (front > Circumference * 0.5f) front -= Circumference;
            if (front < -Circumference * 0.5f) front += Circumference;
            float back = (u - 0.75f) * Circumference;
            if (back > Circumference * 0.5f) back -= Circumference;
            if (back < -Circumference * 0.5f) back += Circumference;
            bool nearCrest = Mathf.Abs(front) < 0.13f && z > 1.19f && z < 1.48f;
            bool nearNumber = Mathf.Abs(back) < 0.13f && z > 1.14f && z < 1.50f;
            if (!nearCrest && !nearNumber) return 0f;
            const float r = 0.0035f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                float dx = Mathf.Cos(a) * r, dz = Mathf.Sin(a) * r;
                if (Mathf.Max(Crest(front + dx, z + dz), Number31(back + dx, z + dz)) > 0.5f) return 1f;
            }
            return 0f;
        }

        const float Edge = 0.0016f;   // one pixel, in metres
        static float Cover(float signedDistance) { return Mathf.Clamp01(0.5f + signedDistance / Edge); }
        static float Band(float x, float lo, float hi) { return Cover(Mathf.Min(x - lo, hi - x)); }

        static void Torso(float u, float v, ref float white, ref float dark, ref float thread)
        {
            float z = HemZ + v / 0.4375f * 0.70f;
            float h = z - HemZ;
            float front = (u - 0.25f) * Circumference;               // metres sideways of the front centre (to the viewer's right)
            if (front > Circumference * 0.5f) front -= Circumference;
            if (front < -Circumference * 0.5f) front += Circumference;
            float back = (u - 0.75f) * Circumference;
            if (back > Circumference * 0.5f) back -= Circumference;
            if (back < -Circumference * 0.5f) back += Circumference;
            // The horizontal edges lie exactly on rings of the mesh (make_goalie_jersey.py PRINT_EDGE_Z): 0.940, 0.975, 1.005, 1.040, 1.578.
            white = Mathf.Max(Band(h, 0.040f, 0.075f), Band(h, 0.105f, 0.140f));           // two hem stripes
            white = Mathf.Max(white, Cover(z - 1.578f));                                     // the narrow white yoke band across the top of the shoulders
            float w = (z - 1.49f) * 0.85f;                                                  // half width of the V
            float inside = Mathf.Min(w - Mathf.Abs(front), z - 1.49f) * 0.85f;
            float opening = z > 1.49f && Mathf.Abs(front) < w ? Cover(inside) : 0f;
            float trim = Mathf.Abs(Mathf.Abs(front) - w) < 0.015f && z > 1.478f && z < 1.62f ? Cover(0.0075f - Mathf.Abs(Mathf.Abs(front) - w)) : 0f;
            dark = Mathf.Max(opening * (1f - trim), 0f);
            white = Mathf.Max(white * (1f - opening), trim);
            for (int lace = 0; lace < 3; lace++)                                            // the laces: short white bars across the V
            {
                float lz = 1.522f + 0.026f * lace;
                white = Mathf.Max(white, Mathf.Min(Band(z, lz - 0.004f, lz + 0.004f), Cover(w - 0.006f - Mathf.Abs(front))));
            }
            thread = Mathf.Max(Crest(front, z), Number31(back, z));
        }

        static void Sleeve(float u, float v, ref float white)
        {
            float local;
            if (u < 0.40f) local = u / 0.40f;
            else if (u > 0.5f && u < 0.90f) local = (u - 0.5f) / 0.40f;
            else return;
            float d = (v - 0.5f) / 0.34f * 0.535f;
            // The edges lie exactly on rings of the sleeve mesh (PRINT_EDGE_X in make_goalie_jersey.py): d 0.235, 0.270, 0.296, 0.331.
            // The shoulder cap stays blue: a white cap met the blue torso shoulder along the surface intersection and drew a hook.
            white = Mathf.Max(Band(d, 0.235f, 0.270f), Band(d, 0.296f, 0.331f));              // two stripes at the elbow
        }

        // Original compass shield. No club or league mark. Metres from the front centre (x) and the height z.
        static float Crest(float x, float z)
        {
            float fx = Mathf.Abs(x) / 0.086f, y = (z - 1.34f) / 0.115f;
            float width = y < 0f ? 1f + y * 0.65f : 1f;
            bool shield = y > -1f && y < 1f && fx < width && (fx > width - 0.13f || y > 0.86f);
            bool star = Mathf.Abs(y) < 0.62f && fx < 0.14f || Mathf.Abs(y) < 0.12f && fx < 0.65f;
            return shield || star ? 1f : 0f;
        }

        // The number 31 on the back, metres from the back centre (to the viewer's right when seen from behind; checked in Play).
        static float Number31(float x, float z)
        {
            float nx = 0.5f + x / 0.20f, y = (z - 1.18f) / 0.28f;
            if (nx < 0f || nx > 1f || y < 0f || y > 1f) return 0f;
            // Broad stitched block digits, with a clear gap between them.
            bool three = nx < 0.48f && (nx > 0.34f || y < 0.13f || y > 0.87f || Mathf.Abs(y - 0.5f) < 0.065f);
            bool one = nx > 0.66f && nx < 0.83f || y < 0.13f && nx > 0.55f || y > 0.80f && nx > 0.55f && nx < 0.83f;
            return three || one ? 1f : 0f;
        }
    }
}
