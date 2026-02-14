using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VToyEditor.Parsers
{
    public struct Color
    {
        public float r, g, b, a;

        public Color(float R, float G, float B, float A)
        {
            r = R; g = G; b = B; a = A;
        }
    }

    public class SubMesh : IDisposable
    {
        public Vertex[] Vertices;
        public ushort[] Indices;
        public uint Vao, Vbo, Ebo;
        public int IndexCount;
        public int MaterialIndex;
        private GL _gl;

        public unsafe void Upload(GL gl)
        {
            _gl = gl;
            Vao = _gl.GenVertexArray();
            _gl.BindVertexArray(Vao);

            Vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
            fixed (void* v = Vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(Vertices.Length * sizeof(Vertex)), v, BufferUsageARB.StaticDraw);

            Ebo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
            fixed (void* i = Indices)
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(Indices.Length * sizeof(ushort)), i, BufferUsageARB.StaticDraw);

            // Attributes: 0 = Pos, 1 = Normal, 2 = UV
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)0);

            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)12);

            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, (uint)sizeof(Vertex), (void*)24);

            IndexCount = Indices.Length;
            _gl.BindVertexArray(0); // Unbind
        }

        public void Dispose()
        {
            _gl?.DeleteBuffer(Vbo);
            _gl?.DeleteBuffer(Ebo);
            _gl?.DeleteVertexArray(Vao);
        }
    }

    public class Material
    {
        public string TextureName;
        public float UnknownValue;
        public bool IsTwoSided;
    }

    public class StaticMeshAsset
    {
        public List<SubMesh> SubMeshes = new List<SubMesh>();
        public Matrix4x4 WorldTransform;
        public List<Material> Materials = new List<Material>();
        public Vector3[] Corners;
    }

    public class PropObject
    {
        public int MeshIndex;
        public Matrix4x4 Transform;
        public Vector3[] Corners;
    }

    public class DecalObject
    {
        public string Name;
        public Vector3 Position;
        public float ScaleX;
        public float ScaleY;
        public float ZOffset; // Used to prevent Z-Fighting
    }

    [StructLayout(LayoutKind.Explicit)]
    public class LightObject
    {
        public enum LightType
        {
            POINT_LIGHT = 0,
            SPOT_LIGHT = 1,
            DIRECTIONAL_LIGHT = 2,
            AMBIENT_LIGHT = 3
        }

        public struct PointLight
        {
            public bool UsesRadius;
            public Vector3 Position;
            public float Start;
            public float End;
            public float Radius;
        }

        public struct SpotLight
        {
            public Vector3 Position;
            public Vector3 Direction;
            public float ConeRadius;
        }

        public struct DirectionalLight
        {
            public Vector3 Direction;
        }

        public struct AmbientLight
        {
        }

        [FieldOffset(0)]
        public PointLight Point;

        [FieldOffset(0)]
        public SpotLight Spot;

        [FieldOffset(0)]
        public DirectionalLight Directional;

        [FieldOffset(0)]
        public AmbientLight Ambient;

        [FieldOffset(32)]
        public LightType Type;

        [FieldOffset(36)]
        public Color DiffuseColor;

        [FieldOffset(52)]
        public Color AmbientColor;

        [FieldOffset(68)]
        public Color SpecularColor;
    }

    public class CollisionBox
    {
        public Matrix4x4 Transform;
        public Vector3 HalfExtents;
    }

    public class VTOPTParser
    {
        public string MapFile { get; private set; }

        public List<StaticMeshAsset> StaticMeshes = new List<StaticMeshAsset>();
        public List<PropObject> Props = new List<PropObject>();
        public List<DecalObject> Decals = new List<DecalObject>();
        public List<LightObject> Lights = new List<LightObject>();
        public List<CollisionBox> CollisionBoxes = new List<CollisionBox>();
        private bool _isOldFormat = false;

        public void Parse(string path)
        {
            MapFile = path;

            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs);

            // Magic
            var magicBytes = reader.ReadBytes(4);
            string magic = System.Text.Encoding.UTF8.GetString(magicBytes);

            if (magic == "VTOP") _isOldFormat = true;
            else if (magic == "VTO1") _isOldFormat = false;
            else throw new Exception($"Unknown Magic: {magic}");

            uint sceneObjCount = reader.ReadUInt32();
            uint staticMeshCount = reader.ReadUInt32();
            uint propCount = reader.ReadUInt32();
            uint decalCount = reader.ReadUInt32();
            uint lightCount = reader.ReadUInt32();
            uint hullsCount = reader.ReadUInt32();
            uint colBoxCount = _isOldFormat ? 0 : reader.ReadUInt32();
            uint unused = reader.ReadUInt32();

            Console.WriteLine($"Parsing {staticMeshCount} meshes and {propCount} props...");

            // Parse Meshes
            for (int i = 0; i < staticMeshCount; i++)
                StaticMeshes.Add(ParseMesh(reader));

            // Parse Props
            for (int i = 0; i < propCount; i++)
            {
                uint meshIndex = reader.ReadUInt32();
                Matrix4x4 propTransform = Helpers.ReadMatrix(reader);

                Vector3[] corners = new Vector3[8];
                for (int c = 0; c < 8; c++)
                {
                    corners[c] = Helpers.ReadVector3(reader);
                }

                Props.Add(new PropObject { MeshIndex = (int)meshIndex, Transform = propTransform, Corners = corners });

                if (_isOldFormat)
                {
                    uint obbCount = reader.ReadUInt32();
                    for (int j = 0; j < obbCount; j++)
                    {
                        Matrix4x4 transform = Helpers.ReadMatrix(reader);
                        Vector3 halfExtents = Helpers.ReadVector3(reader);

                        CollisionBoxes.Add(new CollisionBox { Transform = transform, HalfExtents = halfExtents });
                    }
                }
            }

            // Parse Decals
            for (int i = 0; i < decalCount; i++)
            {
                uint decalNameSize = reader.ReadUInt32();

                string decalName = "";
                if (decalNameSize > 0)
                {
                    decalName = Helpers.ReadString(reader, decalNameSize);
                }

                Vector3 position = Helpers.ReadVector3(reader);

                float scaleX = reader.ReadSingle();
                float scaleY = reader.ReadSingle();

                float zOffset = reader.ReadSingle();

                Decals.Add(new DecalObject { Name = decalName, Position = position, ScaleX = scaleX, ScaleY = scaleY, ZOffset = zOffset });
            }

            // Parse Lights
            for (int i = 0; i < lightCount; i++)
            {
                LightObject.LightType lightType = (LightObject.LightType)reader.ReadUInt32();

                LightObject light = new LightObject();
                light.Type = lightType;

                switch (lightType)
                {
                    case LightObject.LightType.POINT_LIGHT:

                        Vector3 pointPos = Helpers.ReadVector3(reader);

                        if (_isOldFormat)
                        {
                            float radius = reader.ReadSingle();

                            light.Point.UsesRadius = true;
                            light.Point.Radius = radius;
                        }
                        else
                        {
                            float start = reader.ReadSingle();
                            float end = reader.ReadSingle();

                            light.Point.UsesRadius = false;
                            light.Point.Start = start;
                            light.Point.End = end;
                        }

                        light.Point.Position = pointPos;

                        break;
                    case LightObject.LightType.SPOT_LIGHT:
                        Vector3 spotPos = Helpers.ReadVector3(reader);
                        Vector3 spotDir = Helpers.ReadVector3(reader);

                        float cone = reader.ReadSingle();

                        light.Spot.Position = spotPos;
                        light.Spot.Direction = spotDir;
                        light.Spot.ConeRadius = cone;

                        break;
                    case LightObject.LightType.DIRECTIONAL_LIGHT:
                        Vector3 directionalDir = Helpers.ReadVector3(reader);


                        light.Directional.Direction = directionalDir;

                        break;
                    case LightObject.LightType.AMBIENT_LIGHT:

                        break;
                    default:
                        break;
                }

                float diffuseR = reader.ReadSingle();
                float diffuseG = reader.ReadSingle();
                float diffuseB = reader.ReadSingle();
                float diffuseA = reader.ReadSingle();

                float ambientR = reader.ReadSingle();
                float ambientG = reader.ReadSingle();
                float ambientB = reader.ReadSingle();
                float ambientA = reader.ReadSingle();

                float specularR = reader.ReadSingle();
                float specularG = reader.ReadSingle();
                float specularB = reader.ReadSingle();
                float specularA = reader.ReadSingle();

                light.DiffuseColor = new Color(diffuseR, diffuseG, diffuseB, diffuseA);
                light.AmbientColor = new Color(ambientR, ambientG, ambientB, ambientA);
                light.SpecularColor = new Color(specularR, specularG, specularB, specularA);

                Lights.Add(light);
            }

            // Parse Hulls

            // Parse Collision Boxes
            if (colBoxCount > 0)
            {
                for (int i = 0; i < colBoxCount; i++)
                {
                    Matrix4x4 transform = Helpers.ReadMatrix(reader);
                    Vector3 halfExtents = Helpers.ReadVector3(reader);

                    CollisionBoxes.Add(new CollisionBox { Transform = transform, HalfExtents = halfExtents });
                }
            }
        }

        private StaticMeshAsset ParseMesh(BinaryReader r)
        {
            var asset = new StaticMeshAsset();

            // Materials
            uint matCount = r.ReadUInt32();
            for (int i = 0; i < matCount; i++)
            {
                uint nameLen = r.ReadUInt32();

                string materialName = "";
                if (nameLen > 0)
                {
                    materialName = Helpers.ReadString(r, nameLen);
                }

                uint texCount = r.ReadUInt32();
                string diffuseTexture = null;

                for (int t = 0; t < texCount; t++)
                {
                    uint tLen = r.ReadUInt32();

                    string textureName = "";
                    if (tLen > 0)
                    {
                        textureName = Helpers.ReadString(r, tLen);

                        if (t == 0) diffuseTexture = textureName;
                    }
                }

                bool isTwoSided = r.ReadByte() != 0; // Two sided
                r.ReadBytes(4 * 16); // UV Map (4 vec4s)
                float unkMaterialFloat = r.ReadSingle(); // Unk

                asset.Materials.Add(new Material { TextureName = diffuseTexture ?? "", IsTwoSided = isTwoSided, UnknownValue = unkMaterialFloat });
            }

            // LODs
            uint lodCount = r.ReadUInt32();
            for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
            {
                r.ReadUInt32(); // Unk
                uint meshCount = r.ReadUInt32();
                if (meshCount == 0) continue;

                for (int m = 0; m < meshCount; m++)
                {
                    bool isActive = r.ReadByte() != 0;
                    uint vtopVertCount = _isOldFormat ? r.ReadUInt32() : 0;
                    uint subMeshCount = r.ReadUInt32();

                    // Corners (8 vec3)
                    r.ReadBytes(8 * 12);

                    // If Old Format, read main vertices block
                    Vertex[] vertices = new Vertex[vtopVertCount];
                    if (_isOldFormat && vtopVertCount > 0)
                    {
                        for (int v = 0; v < vtopVertCount; v++)
                        {
                            Vector3 point = Helpers.ReadVector3(r);
                            Vector3 normal = Helpers.ReadVector3(r);
                            float u = r.ReadSingle();
                            float v_tex = r.ReadSingle();

                            vertices[v] = new Vertex(
                                point,
                                normal,
                                new Vector2(u, v_tex)
                            );
                        }
                    }

                    if (subMeshCount == 0) continue;

                    for (int s = 0; s < subMeshCount; s++)
                    {
                        uint strideType = r.ReadUInt32();

                        if (_isOldFormat)
                        {
                            uint matIdx = r.ReadUInt32();

                            uint idxCount = r.ReadUInt32();
                            uint vertCount = r.ReadUInt32();
                            r.ReadUInt32(); // Unk

                            ushort[] vertexRemapTable = new ushort[vertCount];
                            for (int i = 0; i < vertCount; i++)
                            {
                                vertexRemapTable[i] = r.ReadUInt16();
                            }

                            ushort[] indices = new ushort[idxCount * 3];
                            byte[] indexBytes = r.ReadBytes((int)idxCount * 3 * 2);
                            System.Buffer.BlockCopy(indexBytes, 0, indices, 0, indexBytes.Length);

                            // We only want to parse the first LOD (highest quality)
                            // Not doing this causes multiple geometry pieces in the same place
                            if (lodIndex == 0)
                            {
                                var outVertices = new Vertex[vtopVertCount];

                                for (int i = 0; i < vertexRemapTable.Length; i++)
                                {
                                    ushort originalIndex = vertexRemapTable[i];
                                    outVertices[i] = vertices[originalIndex];
                                }

                                asset.SubMeshes.Add(new SubMesh { Vertices = outVertices, Indices = indices, MaterialIndex = (int)matIdx });
                            }
                        }
                        else
                        {
                            uint materialID = r.ReadUInt32();
                            uint matIdx = r.ReadUInt32();
                            uint faceCount = r.ReadUInt32();
                            uint vertCount = r.ReadUInt32();
                            r.ReadUInt32(); // Unk

                            // Indices
                            ushort[] indices = new ushort[faceCount * 3];
                            byte[] indexBytes = r.ReadBytes((int)faceCount * 3 * 2);
                            System.Buffer.BlockCopy(indexBytes, 0, indices, 0, indexBytes.Length);

                            // Vertices
                            int stride = (strideType != 0) ? 40 : 32;
                            vertices = new Vertex[vertCount];

                            for (int v = 0; v < vertCount; v++)
                            {
                                Vector3 point = Helpers.ReadVector3(r);
                                Vector3 normal = Helpers.ReadVector3(r);
                                float u = r.ReadSingle();
                                float v_tex = r.ReadSingle();

                                // Ignore extra UVs if stride is 40
                                if (stride == 40) r.ReadBytes(8);

                                vertices[v] = new Vertex(
                                    point,
                                    normal,
                                    new Vector2(u, v_tex)
                                );
                            }

                            if (lodIndex == 0)
                            {
                                asset.SubMeshes.Add(new SubMesh { Vertices = vertices, Indices = indices, MaterialIndex = (int)matIdx });
                            }
                        }
                    }
                }
            }

            if (_isOldFormat)
            {
                uint cbCount = r.ReadUInt32();
                if (cbCount > 0)
                {
                    for (int i = 0; i < cbCount; i++)
                    {
                        Matrix4x4 transform = Helpers.ReadMatrix(r);
                        Vector3 halfExtents = Helpers.ReadVector3(r);

                        CollisionBoxes.Add(new CollisionBox { Transform = transform, HalfExtents = halfExtents });
                    }
                }
            }

            // Corners
            Vector3[] corners = new Vector3[8];
            for (int c = 0; c < 8; c++)
            {
                corners[c] = Helpers.ReadVector3(r);
            }
            asset.Corners = corners;

            asset.WorldTransform = Helpers.ReadMatrix(r);

            return asset;
        }
    }
}
