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
using VToyEditor.Parsers;
using VToyEditor.Modules;

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
        public bool IsTwoSided;

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
        private static Shader _sceneShader;
        private static Shader _debugShader;
        private static float _globalGamma = 1f;
        private static MeshGenerator _decalMesh = new MeshGenerator();
        private static MeshGenerator _obbMesh = new MeshGenerator();

        private static IKeyboard _primaryKeyboard;
        private static IMouse _primaryMouse;
        private static bool _cursorLocked = true;

        public static ImGuiController imguiController = null;

        public static vtPackParser packer = new vtPackParser();
        public static VTSCNParser scene;
        public static VTOPTParser sceneOPT;
        public static VTHMParser sceneHeightMap;

        public static Dictionary<string, Texture> textureCache = new Dictionary<string, Texture>();

        public static void AddTextureToCache(string texName, string fullPath)
        {
            if (string.IsNullOrEmpty(texName) || textureCache.ContainsKey(texName)) return;

            if (!texName.EndsWith(".tex")) fullPath += ".tex";

            if (File.Exists(fullPath))
            {
                try
                {
                    textureCache[texName] = new Texture(_gl, fullPath);
                }
                catch (Exception ex) { Console.WriteLine($"Failed to load {texName}: {ex.Message}"); }
            }
            else
            {
                Console.WriteLine($"Texture missing: {texName}");
            }
        }

        public static void ParseScene(string scenePathRaw, bool skipSceneParse = false, string customMapName = "", string customHeightMapName = "")
        {
            // If we have no scene object, create it anyways (even if we should skip this scene parse)
            if (!skipSceneParse || scene == null)
            {
                scene = new VTSCNParser();
            }

            // Verify each file exists
            var scenePath = Helpers.GetCaseInsensitivePath(scenePathRaw);

            // Obviously, if we shouldn't skip this scene, we need to parse it
            if (!skipSceneParse)
            {
                scene.Parse(scenePath);
            }

            var heightMapPathRaw = skipSceneParse ? customHeightMapName : vtPackParser.DefaultOutputFolder + scene.HeightMapName;
            var mapPathRaw = skipSceneParse ? customMapName : vtPackParser.DefaultOutputFolder + scene.MapName;

            var heightMapPath = Helpers.GetCaseInsensitivePath(heightMapPathRaw);
            var mapPath = Helpers.GetCaseInsensitivePath(mapPathRaw);

            if ((scenePath == null || scenePath == string.Empty) && !skipSceneParse)
            {
                DebugMenu.ShowError("Missing scene", $"Could not find scene file '{scenePathRaw}', aborting.");
                return;
            }
            
            bool failedHeightMap = false;
            if (heightMapPath == null || heightMapPath == string.Empty)
            {
                if (sceneHeightMap == null)
                {
                    throw new Exception($"Missing height map file '{heightMapPathRaw}' and no height map object was ever created.");
                }

                DebugMenu.ShowError("Missing height map", $"Could not find height map file '{heightMapPathRaw}', using last loaded height map file.");
                failedHeightMap = true;
            }

            if (mapPath == null || mapPath == string.Empty)
            {
                if (sceneOPT == null)
                {
                    throw new Exception($"Missing map file '{mapPathRaw}' and no map object was ever created.");
                }

                DebugMenu.ShowError("Missing map", $"Could not find map file '{mapPathRaw}', aborting.");
                return;
            }

            // Destroy old meshes
            if (sceneOPT != null)
            {
                foreach (var mesh in sceneOPT.StaticMeshes)
                {
                    foreach (var sub in mesh.SubMeshes)
                    {
                        sub.Dispose();
                    }
                }

                _decalMesh.mesh.Dispose();
                _obbMesh.mesh.Dispose();
            }

            if (!failedHeightMap && heightMapPath != null && heightMapPath != string.Empty)
            {
                sceneHeightMap = new VTHMParser();
                sceneHeightMap.Parse(heightMapPath);
            }
            
            sceneOPT = new VTOPTParser();
            sceneOPT.Parse(mapPath);

            string textureDir = vtPackParser.DefaultOutputFolder + "texs\\";

            // Load sky texture
            if (!skipSceneParse)
            {
                string skyTexturePath = Path.Combine(textureDir, scene.SkyTextureName);
                AddTextureToCache(scene.SkyTextureName, skyTexturePath);
            }

            // Load scene textures
            foreach (var mesh in sceneOPT.StaticMeshes)
            {
                foreach (var material in mesh.Materials)
                {
                    string fullPath = Path.Combine(textureDir, material.TextureName);
                    AddTextureToCache(material.TextureName, fullPath);
                }
            }

            // Load decal textures
            foreach (var decal in sceneOPT.Decals)
            {
                string fullPath = Path.Combine(textureDir, decal.Name);
                AddTextureToCache(decal.Name, fullPath);
            }

            // Upload geometry
            foreach (var mesh in sceneOPT.StaticMeshes)
            {
                foreach (var sub in mesh.SubMeshes)
                {
                    sub.Upload(_gl);
                }
            }

            _decalMesh.mesh = MeshGenerator.GenerateQuadMesh(_gl);
            _obbMesh.mesh = MeshGenerator.GenerateCubeMesh(_gl);
        }

        static void Main(string[] args)
        {
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(1620, 920);
            options.Title = "VToyEditor";

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

            // Unpack file system
            if (File.Exists(vtPackParser.DefaultInPackName) && !Directory.Exists(vtPackParser.DefaultOutputFolder))
            {
                Console.WriteLine("Initial unpacking...");
                
                packer.Unpack(vtPackParser.DefaultInPackName, vtPackParser.DefaultOutputFolder);

                Console.WriteLine("Unpack done");
            }
            else if (!File.Exists(vtPackParser.DefaultInPackName) && !Directory.Exists(vtPackParser.DefaultOutputFolder))
            {
                throw new Exception($"Missing '{vtPackParser.DefaultInPackName}' file, please get this from your legit game installation and place it next to 'VToyEditor.exe'.");
            }

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

            // Initialize emulated modules list
            IGameModule.Initialize();

            _sceneShader = new Shader(_gl, "scene_shader.vert", "scene_shader.frag");
            _debugShader = new Shader(_gl, "debug_shader.vert", "debug_shader.frag");

            // Todo: we assume this scene always exists but maybe we should let the user pick which scene to load at startup?
            ParseScene(vtPackParser.ScenesFolder + "mp_dm_vertigo.scn");

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

            if (!_cursorLocked && _primaryMouse != null && button == MouseButton.Right)
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

            _gl.ClearColor(scene.FogColor.r, scene.FogColor.g, scene.FogColor.b, scene.FogColor.a);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Shader setup
            _sceneShader.Use();

            _sceneShader.SetUniform("uIsUnlit", false);
            _sceneShader.SetUniform("uGamma", _globalGamma);

            _sceneShader.SetUniform("uFogColor", new Vector3(scene.FogColor.r, scene.FogColor.g, scene.FogColor.b));
            _sceneShader.SetUniform("uFogNear", scene.FogNear);
            _sceneShader.SetUniform("uFogFar", scene.FogFar);

            // Camera setup
            var view = Camera.GetViewMatrix();
            var projection = Camera.GetProjectionMatrix((float)_window.Size.X / _window.Size.Y, scene.CamNear, scene.CamFar);

            _sceneShader.SetUniform("view", view);
            _sceneShader.SetUniform("projection", projection);
            _sceneShader.SetUniform("viewPos", Camera.camPos);

            // Lighting
            // Todo: add DX9-like shadows so these display appropriately
            int lightIndex = 0;
            Vector3 globalAmbientAccumulator = Vector3.Zero;
            int maxLights = 128;

            foreach (var l in sceneOPT.Lights)
            {
                if (lightIndex >= maxLights) break;

                if (l.Type == LightObject.LightType.AMBIENT_LIGHT)
                {
                    globalAmbientAccumulator += new Vector3(l.DiffuseColor.r, l.DiffuseColor.g, l.DiffuseColor.b);
                    continue;
                }

                string baseUniform = $"lights[{lightIndex}]";

                _sceneShader.SetUniform($"{baseUniform}.type", (int)l.Type);
                _sceneShader.SetUniform($"{baseUniform}.ambient", new Vector4(l.SpecularColor.r, l.SpecularColor.g, l.SpecularColor.b, 1.0f));
                _sceneShader.SetUniform($"{baseUniform}.diffuse", new Vector4(l.DiffuseColor.r, l.DiffuseColor.g, l.DiffuseColor.b, 1.0f));
                _sceneShader.SetUniform($"{baseUniform}.specular", new Vector4(l.AmbientColor.r, l.AmbientColor.g, l.AmbientColor.b, 1.0f));

                float att0 = 0.0000001f;
                float att1 = 0.0001f;
                float att2 = 0.00002f;
                float range = 1000.0f;

                if (l.Type == LightObject.LightType.POINT_LIGHT)
                {
                    _sceneShader.SetUniform($"{baseUniform}.position", l.Point.Position * new Vector3(1, 1, -1));

                    att0 = 0.0f;
                    att1 = 0.0f;
                    float r = l.Point.Radius > 0 ? l.Point.Radius : l.Point.End;
                    att2 = 1.0f / (r * r);
                    range = r;
                }
                else if (l.Type == LightObject.LightType.SPOT_LIGHT)
                {
                    _sceneShader.SetUniform($"{baseUniform}.position", l.Spot.Position * new Vector3(1, 1, -1));
                    _sceneShader.SetUniform($"{baseUniform}.direction", l.Spot.Direction);

                    float radius = 90f * MathF.PI / 180f; // Default to 90 degrees

                    _sceneShader.SetUniform($"{baseUniform}.theta", radius);
                    _sceneShader.SetUniform($"{baseUniform}.phi", radius);
                    _sceneShader.SetUniform($"{baseUniform}.falloff", 1.0f);
                }
                else if (l.Type == LightObject.LightType.DIRECTIONAL_LIGHT)
                {
                    _sceneShader.SetUniform($"{baseUniform}.direction", l.Directional.Direction);
                }

                _sceneShader.SetUniform($"{baseUniform}.att0", att0);
                _sceneShader.SetUniform($"{baseUniform}.att1", att1);
                _sceneShader.SetUniform($"{baseUniform}.att2", att2);
                _sceneShader.SetUniform($"{baseUniform}.range", range);

                lightIndex++;
            }

            _sceneShader.SetUniform("numLights", lightIndex);
            _sceneShader.SetUniform("globalAmbient", globalAmbientAccumulator);

            var transparentQueue = new List<TransparentDrawCall>();

            Matrix4x4 rootTransform = Matrix4x4.CreateScale(1, 1, -1);

            // Helper to process a mesh
            void ProcessMesh(StaticMeshAsset mesh, Matrix4x4 modelMatrix)
            {
                foreach (var sub in mesh.SubMeshes)
                {
                    Material meshMaterial = mesh.Materials[sub.MaterialIndex];

                    string texName = meshMaterial.TextureName;

                    textureCache.TryGetValue(texName, out Texture tex);

                    if (tex != null && tex.IsTransparent)
                    {
                        float dist = Vector3.DistanceSquared(Camera.camPos, modelMatrix.Translation);
                        transparentQueue.Add(new TransparentDrawCall
                        {
                            SubMesh = sub,
                            Transform = modelMatrix,
                            Texture = tex,
                            DistanceToCamera = dist,
                            IsTwoSided = meshMaterial.IsTwoSided
                        });
                    }
                    else
                    {
                        _sceneShader.SetUniform("model", modelMatrix);
                        if (tex != null) tex.Bind(TextureUnit.Texture0);

                        if (meshMaterial.IsTwoSided)
                        {
                            _gl.Disable(EnableCap.CullFace);
                        }
                        else
                        {
                            _gl.Enable(EnableCap.CullFace);
                            _gl.CullFace(GLEnum.Back);
                        }

                        _gl.BindVertexArray(sub.Vao);
                        _gl.DrawElements(PrimitiveType.Triangles, (uint)sub.IndexCount, DrawElementsType.UnsignedShort, null);
                    }
                }
            }

            void RenderTransparencyPass(bool isColorPass)
            {
                foreach (var call in transparentQueue)
                {
                    bool isDecal = (call.SubMesh == _decalMesh.mesh);

                    // Skip decals on color pass (we don't want them to write to the depth buffer)
                    if (isDecal && !isColorPass)
                    {
                        continue;
                    }
                    
                    _sceneShader.SetUniform("uIsUnlit", isDecal);

                    _sceneShader.SetUniform("model", call.Transform);
                    call.Texture.Bind(TextureUnit.Texture0);

                    _gl.BindVertexArray(call.SubMesh.Vao);

                    if (call.IsTwoSided)
                    {
                        if (isColorPass)
                        {
                            // Render back then front for correct blending
                            _gl.CullFace(GLEnum.Front);
                            _gl.DrawElements(PrimitiveType.Triangles, (uint)call.SubMesh.IndexCount, DrawElementsType.UnsignedShort, null);

                            _gl.CullFace(GLEnum.Back);
                            _gl.DrawElements(PrimitiveType.Triangles, (uint)call.SubMesh.IndexCount, DrawElementsType.UnsignedShort, null);
                        }
                        else
                        {
                            // Draw both sides at once to fill the depth buffer
                            _gl.Disable(EnableCap.CullFace);
                            _gl.DrawElements(PrimitiveType.Triangles, (uint)call.SubMesh.IndexCount, DrawElementsType.UnsignedShort, null);
                        }
                    }
                    else
                    {
                        if (isDecal) _gl.Disable(EnableCap.CullFace);
                        else
                        {
                            _gl.Enable(EnableCap.CullFace);
                            _gl.CullFace(GLEnum.Back);
                        }
                        _gl.DrawElements(PrimitiveType.Triangles, (uint)call.SubMesh.IndexCount, DrawElementsType.UnsignedShort, null);
                    }
                }

                _sceneShader.SetUniform("uIsUnlit", false);
            }

            // Opaque pass
            _gl.DepthMask(true);

            // Render static meshes
            foreach (var mesh in sceneOPT.StaticMeshes)
            {
                ProcessMesh(mesh, rootTransform);
            }

            // Render static props
            foreach (var prop in sceneOPT.Props)
            {
                if (prop.MeshIndex < 0 || prop.MeshIndex >= sceneOPT.StaticMeshes.Count) continue;
                var refMesh = sceneOPT.StaticMeshes[prop.MeshIndex];

                Matrix4x4.Invert(refMesh.WorldTransform, out Matrix4x4 invBase);
                Matrix4x4 combined = invBase * prop.Transform * rootTransform;

                ProcessMesh(refMesh, combined);
            }

            // Transparency pass
            // Todo: in the map 'plaza' there's a church where some flags don't render correctly through the transparent floor, fix this
            // Pass decals to transparent renderer
            foreach (var decal in sceneOPT.Decals)
            {
                if (!textureCache.TryGetValue(decal.Name, out Texture tex))
                {
                    if (!textureCache.TryGetValue(decal.Name + ".tex", out tex)) continue;
                }

                Matrix4x4 viewMatrix = Camera.GetViewMatrix();

                Vector3 camRight = new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31);
                Vector3 camUp = new Vector3(viewMatrix.M12, viewMatrix.M22, viewMatrix.M32);
                Vector3 camBack = new Vector3(viewMatrix.M13, viewMatrix.M23, viewMatrix.M33);

                Vector3 decalPos = decal.Position * new Vector3(1, 1, -1);
                Vector3 worldPos = decalPos + (camBack * decal.ZOffset);

                // Apply billboard transform
                Matrix4x4 modelMatrix = new Matrix4x4(
                    camRight.X * decal.ScaleX, camRight.Y * decal.ScaleX, camRight.Z * decal.ScaleX, 0,
                    -camBack.X, -camBack.Y, -camBack.Z, 0,
                    camUp.X * decal.ScaleY, camUp.Y * decal.ScaleY, camUp.Z * decal.ScaleY, 0,
                    worldPos.X, worldPos.Y, worldPos.Z, 1
                );

                float dist = Vector3.DistanceSquared(Camera.camPos, worldPos);

                transparentQueue.Add(new TransparentDrawCall
                {
                    SubMesh = _decalMesh.mesh,
                    Transform = modelMatrix,
                    Texture = tex,
                    DistanceToCamera = dist
                });
            }

            transparentQueue.Sort();

            // Depth prepass
            _gl.DepthMask(true);
            _gl.ColorMask(false, false, false, false);
            _gl.Disable(EnableCap.CullFace); // Double sided

            _sceneShader.Use();
            _sceneShader.SetUniform("alphaCutoff", 0.5f); // Only draw solid part of the texture

            RenderTransparencyPass(false); // Do depth prepass

            // Color pass
            _gl.DepthMask(false);
            _gl.ColorMask(true, true, true, true);
            _gl.DepthFunc(DepthFunction.Lequal);
            _gl.Enable(EnableCap.Blend);

            _sceneShader.SetUniform("alphaCutoff", 0.0f);

            RenderTransparencyPass(true); // Do color pass

            // Restore defaults
            _gl.DepthMask(true);
            _gl.Enable(EnableCap.CullFace);
            _gl.DepthFunc(DepthFunction.Less);

            // Debug pass
            _debugShader.Use();

            _debugShader.SetUniform("view", view);
            _debugShader.SetUniform("projection", projection);

            if (DebugMenu.DisableDepthTest)
            {
                _gl.Disable(GLEnum.DepthTest);
            }

            if (DebugMenu.ShowCollisionBoxes)
            {
                _debugShader.SetUniform("uColor", new Vector4(0, 1, 0, 1));

                _gl.PolygonMode(GLEnum.FrontAndBack, PolygonMode.Line); // Enable Wireframe
                _gl.Disable(EnableCap.CullFace);

                foreach (var box in sceneOPT.CollisionBoxes)
                {
                    Matrix4x4 sizeScale = Matrix4x4.CreateScale(box.HalfExtents * 2f);
                    Matrix4x4 model = sizeScale * box.Transform * rootTransform;

                    _debugShader.SetUniform("model", model);

                    _gl.BindVertexArray(_obbMesh.mesh.Vao);
                    _gl.DrawElements(PrimitiveType.Triangles, (uint)_obbMesh.mesh.IndexCount, DrawElementsType.UnsignedShort, null);
                }

                _gl.PolygonMode(GLEnum.FrontAndBack, PolygonMode.Fill); // Restore solid rendering
                _gl.Enable(EnableCap.CullFace);
            }

            if (DebugMenu.ShowStartPoints)
            {
                _debugShader.SetUniform("uColor", new Vector4(1, 1, 0, 0.5f));

                foreach (var startPoint in mp_CStartPoint.spawnObjects)
                {
                    Matrix4x4 sizeScale = Matrix4x4.CreateScale(new Vector3(75f, 15f, 75f));
                    Matrix4x4 positionTransform = Matrix4x4.CreateTranslation(startPoint.SpawnPosition);
                    Matrix4x4 model = sizeScale * positionTransform * rootTransform;

                    _debugShader.SetUniform("model", model);

                    _gl.BindVertexArray(_obbMesh.mesh.Vao);
                    _gl.DrawElements(PrimitiveType.Triangles, (uint)_obbMesh.mesh.IndexCount, DrawElementsType.UnsignedShort, null);
                }
            }

            if (DebugMenu.ShowHealthPacks)
            {
                _debugShader.SetUniform("uColor", new Vector4(1, 0, 0, 0.5f));

                foreach (var healthPack in mp_salud.healthPackObjects)
                {
                    Matrix4x4 sizeScale = Matrix4x4.CreateScale(50f);
                    Matrix4x4 model = sizeScale * healthPack.Transform * rootTransform;

                    _debugShader.SetUniform("model", model);

                    _gl.BindVertexArray(_obbMesh.mesh.Vao);
                    _gl.DrawElements(PrimitiveType.Triangles, (uint)_obbMesh.mesh.IndexCount, DrawElementsType.UnsignedShort, null);
                }
            }

            if (DebugMenu.ShowFlagObjects)
            {
                _debugShader.SetUniform("uColor", new Vector4(0, 0, 1, 0.5f));

                foreach (var flagObject in mp_Flag.flagObjects)
                {
                    Matrix4x4 sizeScale = Matrix4x4.CreateScale(50f);
                    Matrix4x4 model = sizeScale * flagObject.Transform * rootTransform;

                    _debugShader.SetUniform("model", model);

                    _gl.BindVertexArray(_obbMesh.mesh.Vao);
                    _gl.DrawElements(PrimitiveType.Triangles, (uint)_obbMesh.mesh.IndexCount, DrawElementsType.UnsignedShort, null);
                }
            }

            if (DebugMenu.DisableDepthTest)
            {
                _gl.Enable(GLEnum.DepthTest);
            }

            DebugMenu.OnRender();

            imguiController.Render();
        }
    }
}