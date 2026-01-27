using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics; // Uses System.Numerics vectors/matrices
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace VToyEditor
{
    // --- Data Structures ---

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoord;

        public Vertex(Vector3 pos, Vector3 norm, Vector2 tex)
        {
            Position = pos;
            Normal = norm;
            TexCoord = tex;
        }
    }

    public struct TransparentDrawCall : IComparable<TransparentDrawCall>
    {
        public SubMesh SubMesh;
        public Matrix4x4 Transform;
        public Texture Texture;
        public float DistanceToCamera;

        // This allows List.Sort() to automatically order them
        public int CompareTo(TransparentDrawCall other)
        {
            // Sort Descending (Furthest first)
            return other.DistanceToCamera.CompareTo(this.DistanceToCamera);
        }
    }

    class Program
    {
        private static IWindow _window;
        private static GL _gl;
        private static VTSceneParser _parser;
        private static Dictionary<string, Texture> _textureCache = new Dictionary<string, Texture>();
        private static Shader _sceneShader;

        private static IKeyboard _primaryKeyboard;

        static void Main(string[] args)
        {
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(1280, 720);
            options.Title = "VTO1 Scene Renderer";

            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Render += OnRender;
            _window.Update += OnUpdate;
            _window.Run();
        }

        private static unsafe void OnLoad()
        {
            _gl = _window.CreateOpenGL();

            _sceneShader = new Shader(_gl, "scene_shader.vert", "scene_shader.frag");

            // Load Scene
            _parser = new VTSceneParser();
            // CHANGE THIS TO YOUR FILE
            _parser.Parse("mp_dm_vertigo.opt");

            string textureDir = "./texs/"; // Folder containing .tex files
            foreach (var mesh in _parser.StaticMeshes)
            {
                foreach (var texName in mesh.MaterialTextureNames)
                {
                    if (string.IsNullOrEmpty(texName) || _textureCache.ContainsKey(texName)) continue;

                    string fullPath = Path.Combine(textureDir, texName);

                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            _textureCache[texName] = new Texture(_gl, fullPath);
                        }
                        catch (Exception ex) { Console.WriteLine($"Failed to load {texName}: {ex.Message}"); }
                    }
                    else
                    {
                        Console.WriteLine($"Texture missing: {texName}");
                    }
                }
            }

            // Upload geometry
            foreach (var mesh in _parser.StaticMeshes)
            {
                foreach (var sub in mesh.SubMeshes)
                {
                    sub.Upload(_gl);
                }
            }

            _gl.Enable(EnableCap.DepthTest);

            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Input setup
            var input = _window.CreateInput();
            _primaryKeyboard = input.Keyboards.FirstOrDefault();
            if (_primaryKeyboard != null)
            {
                _primaryKeyboard.KeyDown += KeyDown;
            }

            for (int i = 0; i < input.Mice.Count; i++)
            {
                input.Mice[i].Cursor.CursorMode = CursorMode.Raw;
                input.Mice[i].MouseMove += OnMouseMove;
                input.Mice[i].Scroll += OnMouseWheel;
            }
        }

        private static void OnUpdate(double dt)
        {
            if (_primaryKeyboard == null) return;

            Camera.Move(_primaryKeyboard, (float)dt);
        }

        private static unsafe void OnMouseMove(IMouse mouse, Vector2 position)
        {
            Camera.Look(mouse, position);
        }

        private static unsafe void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
        {
            Camera.camZoom = Math.Clamp(Camera.camZoom - scrollWheel.Y, 1.0f, 90f);
        }

        private static void KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            if (key == Key.Escape)
            {
                _window.Close();
            }
        }

        private static unsafe void OnRender(double dt)
        {
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _sceneShader.Use();

            var view = Camera.GetViewMatrix();
            var projection = Camera.GetProjectionMatrix((float)_window.Size.X / _window.Size.Y);

            _sceneShader.SetUniform("view", view);
            _sceneShader.SetUniform("projection", projection);

            // List to hold transparent items for later sorting
            var transparentQueue = new List<TransparentDrawCall>();

            Matrix4x4 rootTransform = Matrix4x4.CreateScale(1, 1, -1);

            // Helper Action to process a mesh (reduces code duplication)
            void ProcessMesh(StaticMeshAsset mesh, Matrix4x4 modelMatrix)
            {
                foreach (var sub in mesh.SubMeshes)
                {
                    // 1. Resolve Texture
                    string texName = (sub.MaterialIndex >= 0 && sub.MaterialIndex < mesh.MaterialTextureNames.Count)
                        ? mesh.MaterialTextureNames[sub.MaterialIndex] : "";

                    _textureCache.TryGetValue(texName, out Texture tex);

                    // 2. Decide: Draw Now (Opaque) or Queue (Transparent)?
                    if (tex != null && tex.IsTransparent)
                    {
                        // Calculate center of object for sorting
                        // (Using translation component of matrix is a fast approximation)
                        float dist = Vector3.DistanceSquared(Camera.camPos, modelMatrix.Translation);

                        transparentQueue.Add(new TransparentDrawCall
                        {
                            SubMesh = sub,
                            Transform = modelMatrix,
                            Texture = tex,
                            DistanceToCamera = dist
                        });
                    }
                    else
                    {
                        // OPAQUE PASS: Draw Immediately
                        _sceneShader.SetUniform("model", modelMatrix);
                        if (tex != null) tex.Bind(TextureUnit.Texture0);

                        _gl.BindVertexArray(sub.Vao);
                        _gl.DrawElements(PrimitiveType.Triangles, (uint)sub.IndexCount, DrawElementsType.UnsignedShort, null);
                    }
                }
            }

            // --- PASS 1: OPAQUE & COLLECTION ---
            // Ensure depth writing is ON for opaque objects
            _gl.DepthMask(true);

            // 1. Process Static Level Geometry
            foreach (var mesh in _parser.StaticMeshes)
            {
                ProcessMesh(mesh, rootTransform);
            }

            // 2. Process Props
            foreach (var prop in _parser.Props)
            {
                if (prop.MeshIndex < 0 || prop.MeshIndex >= _parser.StaticMeshes.Count) continue;
                var refMesh = _parser.StaticMeshes[prop.MeshIndex];

                Matrix4x4.Invert(refMesh.WorldTransform, out Matrix4x4 invBase);
                Matrix4x4 combined = invBase * prop.Transform * rootTransform;

                ProcessMesh(refMesh, combined);
            }

            // --- PASS 2: TRANSPARENT ---

            // Sort from Back to Front (Furthest objects first)
            transparentQueue.Sort();

            // 1. Disable Depth Writing
            // This allows transparent objects to overlap without "cutting" holes in each other.
            // We keep Depth Testing ON so they don't draw on top of opaque walls.
            _gl.DepthMask(false);

            // 2. Enable Face Culling
            _gl.Enable(EnableCap.CullFace);

            foreach (var call in transparentQueue)
            {
                _sceneShader.SetUniform("model", call.Transform);
                call.Texture.Bind(TextureUnit.Texture0);
                _gl.BindVertexArray(call.SubMesh.Vao);

                // --- SUB-PASS A: Draw Back Faces (Inside) ---
                // We tell OpenGL to Cull (hide) the FRONT faces, so only BACK faces are drawn.
                _gl.CullFace(GLEnum.Front);
                _gl.DrawElements(PrimitiveType.Triangles, (uint)call.SubMesh.IndexCount, DrawElementsType.UnsignedShort, null);

                // --- SUB-PASS B: Draw Front Faces (Outside) ---
                // Now we Cull the BACK faces, so only FRONT faces are drawn.
                _gl.CullFace(GLEnum.Back);
                _gl.DrawElements(PrimitiveType.Triangles, (uint)call.SubMesh.IndexCount, DrawElementsType.UnsignedShort, null);
            }

            // Restore Default State (Important!)
            _gl.Disable(EnableCap.CullFace);
            _gl.DepthMask(true);
        }
    }
}