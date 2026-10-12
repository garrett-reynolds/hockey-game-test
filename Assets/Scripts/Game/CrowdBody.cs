using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// One crowd fan body at one level of detail, read from a file Tools/make_crowd.py writes (Assets/Resources/Crowd/*.bytes):
    /// the seated mesh with its hair styles (uv0 = (vertex number, Region), uv1.x = hair style, 0 for the body), and one
    /// vertex animation texture (VAT) holding every clip for every body shape: one RGBAHalf texel per vertex (x) per frame
    /// (y) with the vertex's place in that frame; the rows run shape by shape, clip by clip (Clip.FirstRow within a shape).
    /// A far body also has two card pictures (seated, standing) for the far fans' flat cards.
    /// The mesh is built here, not imported, so the vertex numbers match the texture's columns.
    /// </summary>
    public sealed class CrowdBody
    {
        /// <summary>The body's regions, painted per fan by the crowd shader. Tools/make_crowd.py uses the same numbers.</summary>
        public enum Region { Skin = 0, Shirt = 1, Pants = 2, Shoes = 3, Scalp = 4 }

        public sealed class Clip
        {
            public string Name;
            public int Frames, FirstRow;
            public float FramesPerSecond;
            public bool Loops;
            public float Seconds => Frames / FramesPerSecond;
        }

        public Mesh Mesh { get; private set; }
        public Clip[] Clips { get; private set; }
        public int Shapes { get; private set; }
        /// <summary>Rows of the VAT per body shape (all clips' frames).</summary>
        public int RowsPerShape { get; private set; }
        public Texture2D Vat { get; private set; }
        /// <summary>The card pictures (R8: 0 empty, else Region + 1; seated then standing side by side), or null.</summary>
        public Texture2D Cards { get; private set; }
        /// <summary>The card's size in metres (width, height), its bottom on the floor under the hips.</summary>
        public Vector2 CardSize { get; private set; }

        public Clip ClipNamed(string name) => Array.Find(Clips, c => c.Name == name);

        const int Version = 2;

        /// <summary>The body in Assets/Resources/Crowd/fileName.bytes, or null when there is none.</summary>
        public static CrowdBody Load(string fileName)
        {
            var file = Resources.Load<TextAsset>(Crowd.Folder + "/" + fileName);
            if (file == null) return null;
            CrowdBody body = Read(file.bytes, fileName);
            Resources.UnloadAsset(file);
            return body;
        }

        public static CrowdBody Read(byte[] data, string name)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "CRWD" || r.ReadUInt32() != Version)
                    throw new InvalidDataException(name + " is not a version " + Version + " crowd body file.");
                int vertexCount = (int)r.ReadUInt32(), triangleCount = (int)r.ReadUInt32();
                int shapes = (int)r.ReadUInt32(), clipCount = (int)r.ReadUInt32();

                var vertices = new Vector3[vertexCount];
                var normals = new Vector3[vertexCount];
                var uv = new Vector2[vertexCount];
                var hair = new Vector2[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    normals[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    uv[i] = new Vector2(i, r.ReadByte());
                    hair[i] = new Vector2(r.ReadByte(), 0);
                    r.ReadBytes(2);
                }
                var triangles = new int[triangleCount * 3];
                for (int i = 0; i < triangles.Length; i++) triangles[i] = r.ReadUInt16();

                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uv);
                mesh.SetUVs(1, hair);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();

                var clips = new Clip[clipCount];
                int rows = 0;
                for (int c = 0; c < clipCount; c++)
                {
                    string clipName = Encoding.ASCII.GetString(r.ReadBytes(r.ReadByte()));
                    int frames = (int)r.ReadUInt32();
                    float fps = r.ReadSingle();
                    bool loops = r.ReadByte() != 0;
                    clips[c] = new Clip { Name = clipName, Frames = frames, FramesPerSecond = fps, Loops = loops, FirstRow = rows };
                    rows += frames;
                }
                var vat = new Texture2D(vertexCount, rows * shapes, TextureFormat.RGBAHalf, false, true)
                {
                    name = name + " VAT", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                };
                vat.SetPixelData(r.ReadBytes(vertexCount * rows * shapes * 8), 0);
                vat.Apply(false, true);

                var body = new CrowdBody { Mesh = mesh, Clips = clips, Shapes = shapes, RowsPerShape = rows, Vat = vat };
                int cards = (int)r.ReadUInt32();
                if (cards > 0)
                {
                    int w = (int)r.ReadUInt32(), h = (int)r.ReadUInt32();
                    body.CardSize = new Vector2(r.ReadSingle(), r.ReadSingle());
                    byte[] pixels = new byte[w * cards * h];
                    for (int k = 0; k < cards; k++)
                        for (int y = 0; y < h; y++)
                            Array.Copy(r.ReadBytes(w), 0, pixels, y * w * cards + k * w, w);
                    body.Cards = new Texture2D(w * cards, h, TextureFormat.R8, false, true)
                    {
                        name = name + " cards", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                    };
                    body.Cards.SetPixelData(pixels, 0);
                    body.Cards.Apply(false, true);
                }
                if (r.BaseStream.Position != data.Length) throw new InvalidDataException(name + " has bytes left over.");
                return body;
            }
        }

        public void Destroy()
        {
            SceneObjects.DestroySafely(Mesh);
            SceneObjects.DestroySafely(Vat);
            if (Cards != null) SceneObjects.DestroySafely(Cards);
        }
    }
}
