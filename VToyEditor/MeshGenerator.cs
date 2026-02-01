using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using VToyEditor.Parsers;

namespace VToyEditor
{
    public class MeshGenerator
    {
        public SubMesh mesh;

        public static SubMesh GenerateQuadMesh(GL gl)
        {
            // Creates a flat quad on the XZ plane (Floor)
            // Centered at 0,0,0 with size 1x1
            float s = 0.5f;

            Vertex[] vertices = new Vertex[]
            {
                // Position (X, Y, Z)              // Normal (Up) // UV
                new Vertex(new Vector3(-s, 0, -s), Vector3.UnitY, new Vector2(0, 0)), // Top Left
                new Vertex(new Vector3( s, 0, -s), Vector3.UnitY, new Vector2(1, 0)), // Top Right
                new Vertex(new Vector3( s, 0,  s), Vector3.UnitY, new Vector2(1, 1)), // Bottom Right
                new Vertex(new Vector3(-s, 0,  s), Vector3.UnitY, new Vector2(0, 1))  // Bottom Left
            };

            ushort[] indices = new ushort[]
            {
                0, 2, 1, // Tri 1
                0, 3, 2  // Tri 2
            };

            var mesh = new SubMesh
            {
                Vertices = vertices,
                Indices = indices,
                IndexCount = indices.Length,
                MaterialIndex = -1
            };

            mesh.Upload(gl);
            return mesh;
        }
        
        public static SubMesh GenerateCubeMesh(GL gl)
        {
            float s = 0.5f;
            // Define 24 vertices (6 faces * 4 verts per face) to ensure correct normals per face
            List<Vertex> verts = new List<Vertex>();
            List<ushort> inds = new List<ushort>();

            // Helper to add a face
            void AddFace(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 normal)
            {
                ushort baseIdx = (ushort)verts.Count;
                verts.Add(new Vertex(p1, normal, new Vector2(0, 0)));
                verts.Add(new Vertex(p2, normal, new Vector2(1, 0)));
                verts.Add(new Vertex(p3, normal, new Vector2(1, 1)));
                verts.Add(new Vertex(p4, normal, new Vector2(0, 1)));

                inds.Add(baseIdx); inds.Add((ushort)(baseIdx + 1)); inds.Add((ushort)(baseIdx + 2));
                inds.Add(baseIdx); inds.Add((ushort)(baseIdx + 2)); inds.Add((ushort)(baseIdx + 3));
            }

            // Front face (Z+)
            AddFace(new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, s, s), new Vector3(-s, s, s), new Vector3(0, 0, 1));
            // Back face (Z-)
            AddFace(new Vector3(s, -s, -s), new Vector3(-s, -s, -s), new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(0, 0, -1));
            // Top face (Y+)
            AddFace(new Vector3(-s, s, s), new Vector3(s, s, s), new Vector3(s, s, -s), new Vector3(-s, s, -s), new Vector3(0, 1, 0));
            // Bottom face (Y-)
            AddFace(new Vector3(-s, -s, -s), new Vector3(s, -s, -s), new Vector3(s, -s, s), new Vector3(-s, -s, s), new Vector3(0, -1, 0));
            // Right face (X+)
            AddFace(new Vector3(s, -s, s), new Vector3(s, -s, -s), new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(1, 0, 0));
            // Left face (X-)
            AddFace(new Vector3(-s, -s, -s), new Vector3(-s, -s, s), new Vector3(-s, s, s), new Vector3(-s, s, -s), new Vector3(-1, 0, 0));

            var mesh = new SubMesh
            {
                Vertices = verts.ToArray(),
                Indices = inds.ToArray(),
                MaterialIndex = -1
            };

            mesh.Upload(gl);
            return mesh;
        }
    }
}
