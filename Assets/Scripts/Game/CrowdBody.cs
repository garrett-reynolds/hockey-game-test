using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HockeyGame
{
    /// <summary>
    /// One crowd fan body and its clips, read from a file Tools/make_crowd.py writes (Assets/Resources/Crowd/*.bytes):
    /// the seated mesh (about 1.5k triangles; uv0 = (vertex number, Region)) and, per clip, a vertex animation texture
    /// (VAT): one RGBAHalf texel per vertex (x) per frame (y) holding the vertex's place in that frame.
    /// The mesh is built here, not imported, so the vertex numbers match the texture's columns.
    /// </summary>
    public sealed class CrowdBody
    {
        /// <summary>The body's regions, painted per fan by the crowd shader. Tools/make_crowd.py uses the same numbers.</summary>
        public enum Region { Skin = 0, Shirt = 1, Pants = 2, Shoes = 3, Hair = 4 }

        public sealed class Clip
        {
            public string Name;
            public int Frames;
            public float FramesPerSecond;
            /// <summary>The VAT: width = the body's vertex count, height = Frames.</summary>
            public Texture2D Texture;
            public float Seconds => Frames / FramesPerSecond;
        }

        public Mesh Mesh { get; private set; }
        public Clip[] Clips { get; private set; }
        public Clip ClipNamed(string name) => Array.Find(Clips, c => c.Name == name);

        const int Version = 1;

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

                var vertices = new Vector3[vertexCount];
                var normals = new Vector3[vertexCount];
                var uv = new Vector2[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    normals[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    uv[i] = new Vector2(i, r.ReadByte());
                    r.ReadBytes(3);
                }
                var triangles = new int[triangleCount * 3];
                for (int i = 0; i < triangles.Length; i++) triangles[i] = r.ReadUInt16();

                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();

                var clips = new Clip[r.ReadUInt32()];
                for (int c = 0; c < clips.Length; c++)
                {
                    string clipName = Encoding.ASCII.GetString(r.ReadBytes(r.ReadByte()));
                    int frames = (int)r.ReadUInt32();
                    float fps = r.ReadSingle();
                    var texture = new Texture2D(vertexCount, frames, TextureFormat.RGBAHalf, false, true)
                    {
                        name = name + " " + clipName, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                    };
                    texture.SetPixelData(r.ReadBytes(vertexCount * frames * 8), 0);
                    texture.Apply(false, false);
                    clips[c] = new Clip { Name = clipName, Frames = frames, FramesPerSecond = fps, Texture = texture };
                }
                if (r.BaseStream.Position != data.Length) throw new InvalidDataException(name + " has bytes left over.");
                return new CrowdBody { Mesh = mesh, Clips = clips };
            }
        }

        public void Destroy()
        {
            SceneObjects.DestroySafely(Mesh);
            foreach (Clip c in Clips) SceneObjects.DestroySafely(c.Texture);
        }
    }
}
