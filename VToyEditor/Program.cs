using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
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
        private static Dictionary<string, Texture> _textureCache = new Dictionary<string, Texture>();
        private static Shader _sceneShader;

        private static IKeyboard _primaryKeyboard;
        private static IMouse _primaryMouse;
        private static bool _cursorLocked = true;

        public static ImGuiController imguiController = null;
        public static VTSceneParser scene;

        public static void ParseScene(string filename)
        {
            // Destroy old meshes
            if (scene != null)
            {
                foreach (var mesh in scene.StaticMeshes)
                {
                    foreach (var sub in mesh.SubMeshes)
                    {
                        sub.Dispose();
                    }
                }
            }

            // Load Scene
            scene = new VTSceneParser();
            scene.Parse(filename);

            // Load Scene Textures
            string textureDir = "./texs/";
            foreach (var mesh in scene.StaticMeshes)
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
            foreach (var mesh in scene.StaticMeshes)
            {
                foreach (var sub in mesh.SubMeshes)
                {
                    sub.Upload(_gl);
                }
            }
        }

        static void Main(string[] args)
        {
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(1280, 720);
            options.Title = "VTO1 Scene Renderer";

            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Render += OnRender;
            _window.Update += OnUpdate;
            _window.FramebufferResize += s =>
            {
                if (_gl == null) return;
                _gl.Viewport(s);
            };

            _window.Run();
        }

        private static unsafe void OnLoad()
        {
            _gl = _window.CreateOpenGL();

            // Input setup
            var input = _window.CreateInput();
            _primaryKeyboard = input.Keyboards.FirstOrDefault();
            _primaryMouse = input.Mice.FirstOrDefault(); 
            
            if (_primaryKeyboard != null)
            {
                _primaryKeyboard.KeyDown += KeyDown;
            }

            if (_primaryMouse != null)
            {
                // Set initial state
                _primaryMouse.Cursor.CursorMode = CursorMode.Raw;
                _cursorLocked = true;

                _primaryMouse.MouseMove += OnMouseMove;
                _primaryMouse.Scroll += OnMouseWheel;
                _primaryMouse.MouseDown += OnMouseDown; // Listen for clicks to re-capture
            }

            imguiController = new ImGuiController(_gl, _window, input);

            _sceneShader = new Shader(_gl, "scene_shader.vert", "scene_shader.frag");

            ParseScene("mp_do_plaza.opt");

            _gl.Enable(EnableCap.DepthTest);

            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        private static void OnUpdate(double dt)
        {
            if (_primaryKeyboard == null) return;
            if (!_cursorLocked) return;

            Camera.Move(_primaryKeyboard, (float)dt);
        }

        private static unsafe void OnMouseMove(IMouse mouse, Vector2 position)
        {
            if (!_cursorLocked) return;

            Camera.Look(mouse, position);
        }

        private static unsafe void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
        {
            if (!_cursorLocked) return;

            Camera.camZoom = Math.Clamp(Camera.camZoom - scrollWheel.Y, 1.0f, 90f);
        }
        private static void OnMouseDown(IMouse mouse, MouseButton button)
        {
            if (ImGuiNET.ImGui.GetIO().WantCaptureMouse)
            {
                return;
            }

            if (!_cursorLocked && _primaryMouse != null)
            {
                _cursorLocked = true;
                _primaryMouse.Cursor.CursorMode = CursorMode.Raw;
            }
        }

        private static void KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            if (key == Key.Escape)
            {
                if (_cursorLocked && _primaryMouse != null)
                {
                    _cursorLocked = false;
                    _primaryMouse.Cursor.CursorMode = CursorMode.Normal;
                }
            }
        }
        private static unsafe void OnRender(double dt)
        {
            imguiController.Update((float)dt);

            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _sceneShader.Use();

            // --- CAMERA ---
            var view = Camera.GetViewMatrix();
            var projection = Camera.GetProjectionMatrix((float)_window.Size.X / _window.Size.Y);

            _sceneShader.SetUniform("view", view);
            _sceneShader.SetUniform("projection", projection);
            _sceneShader.SetUniform("viewPos", Camera.camPos);

            // --- LIGHTING ---
            int lightIndex = 0;
            Vector3 globalAmbientAccumulator = Vector3.Zero;

            // Limit to MAX_LIGHTS defined in shader (32)
            int maxLights = 32;

            foreach (var l in scene.Lights)
            {
                if (lightIndex >= maxLights) break;

                // Handle Ambient Type separately as global illumination
                if (l.Type == LightObject.LightType.AMBIENT_LIGHT)
                {
                    globalAmbientAccumulator += new Vector3(l.AmbientColor.r, l.AmbientColor.g, l.AmbientColor.b);
                    continue;
                }

                string baseUniform = $"lights[{lightIndex}]";

                // Common Properties
                _sceneShader.SetUniform($"{baseUniform}.type", (int)l.Type);
                _sceneShader.SetUniform($"{baseUniform}.ambient", l.AmbientColor);
                _sceneShader.SetUniform($"{baseUniform}.diffuse", l.DiffuseColor);
                _sceneShader.SetUniform($"{baseUniform}.specular", l.SpecularColor);

                // Type Specific Properties
                // Note: We access specific properties because they overlap in memory (Union),
                // but we must be careful to read the correct logic for the type.
                if (l.Type == LightObject.LightType.POINT_LIGHT)
                {
                    _sceneShader.SetUniform($"{baseUniform}.position", l.Point.Position);
                    _sceneShader.SetUniform($"{baseUniform}.start", l.Point.Start);
                    _sceneShader.SetUniform($"{baseUniform}.end", l.Point.End);
                    // Radius logic for old format if needed, but shader uses start/end
                }
                else if (l.Type == LightObject.LightType.SPOT_LIGHT)
                {
                    _sceneShader.SetUniform($"{baseUniform}.position", l.Spot.Position);
                    _sceneShader.SetUniform($"{baseUniform}.direction", l.Spot.Direction);
                    // Pass cone directly (assuming Cosine value or Angle depending on source data)
                    _sceneShader.SetUniform($"{baseUniform}.coneRadius", l.Spot.ConeRadius);
                    // Spotlights in this format likely also use Start/End for falloff distance
                    _sceneShader.SetUniform($"{baseUniform}.start", 0.0f);
                    _sceneShader.SetUniform($"{baseUniform}.end", 1000.0f); // Default high range if not provided
                }
                else if (l.Type == LightObject.LightType.DIRECTIONAL_LIGHT)
                {
                    _sceneShader.SetUniform($"{baseUniform}.direction", l.Directional.Direction);
                }

                lightIndex++;
            }

            _sceneShader.SetUniform("numLights", lightIndex);
            _sceneShader.SetUniform("globalAmbient", globalAmbientAccumulator);


            // --- MESH RENDERING (Existing Code) ---

            // List to hold transparent items for later sorting
            var transparentQueue = new List<TransparentDrawCall>();

            Matrix4x4 rootTransform = Matrix4x4.CreateScale(1, 1, -1);

            // Helper Action to process a mesh
            void ProcessMesh(StaticMeshAsset mesh, Matrix4x4 modelMatrix)
            {
                foreach (var sub in mesh.SubMeshes)
                {
                    string texName = (sub.MaterialIndex >= 0 && sub.MaterialIndex < mesh.MaterialTextureNames.Count)
                        ? mesh.MaterialTextureNames[sub.MaterialIndex] : "";

                    _textureCache.TryGetValue(texName, out Texture tex);

                    if (tex != null && tex.IsTransparent)
                    {
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
                        _sceneShader.SetUniform("model", modelMatrix);
                        if (tex != null) tex.Bind(TextureUnit.Texture0);

                        _gl.BindVertexArray(sub.Vao);
                        _gl.DrawElements(PrimitiveType.Triangles, (uint)sub.IndexCount, DrawElementsType.UnsignedShort, null);
                    }
                }
            }

            // --- PASS 1: OPAQUE ---
            _gl.DepthMask(true);

            foreach (var mesh in scene.StaticMeshes)
            {
                ProcessMesh(mesh, rootTransform);
            }

            foreach (var prop in scene.Props)
            {
                if (prop.MeshIndex < 0 || prop.MeshIndex >= scene.StaticMeshes.Count) continue;
                var refMesh = scene.StaticMeshes[prop.MeshIndex];

                Matrix4x4.Invert(refMesh.WorldTransform, out Matrix4x4 invBase);
                Matrix4x4 combined = invBase * prop.Transform * rootTransform;

                ProcessMesh(refMesh, combined);
            }

            // --- PASS 2: TRANSPARENT ---
            transparentQueue.Sort();
            _gl.DepthMask(false);
            _gl.Enable(EnableCap.CullFace);

            foreach (var call in transparentQueue)
            {
                _sceneShader.SetUniform("model", call.Transform);
                call.Texture.Bind(TextureUnit.Texture0);
                _gl.BindVertexArray(call.SubMesh.Vao);

                _gl.CullFace(GLEnum.Front);
                _gl.DrawElements(PrimitiveType.Triangles, (uint)call.SubMesh.IndexCount, DrawElementsType.UnsignedShort, null);

                _gl.CullFace(GLEnum.Back);
                _gl.DrawElements(PrimitiveType.Triangles, (uint)call.SubMesh.IndexCount, DrawElementsType.UnsignedShort, null);
            }

            _gl.Disable(EnableCap.CullFace);
            _gl.DepthMask(true);

            DebugMenu.OnRender();

            imguiController.Render();
        }
    }
}