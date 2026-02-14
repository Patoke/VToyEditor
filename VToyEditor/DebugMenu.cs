using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using VToyEditor.Modules;
using VToyEditor.Parsers;

namespace VToyEditor
{
    public static class DebugMenu
    {
        private static bool _scenesNeedsRefresh = true;
        private static bool _mapsNeedsRefresh = true;
        private static int _selectedScene = 0;
        private static int _selectedMap = 0;
        private static List<string> _scenes = new List<string>();
        private static List<string> _maps = new List<string>();

        private static bool _showAddLightModal = false;
        private static bool _showAddDecalModal = false;
        private static bool _showAddModuleModal = false;
        private static bool _showReloadModal = false;

        private static bool _showErrorModal = false;
        private static string _errorModalTitle = "ERROR";
        private static string _errorModalMessage = "An error occurred";

        public static void ShowError(string title, string message)
        {
            _errorModalTitle = title;
            _errorModalMessage = message;
            _showErrorModal = true;
            ImGui.OpenPopup("ErrorModal");
        }

        private static Type _selectedModuleType = null;
        private static int _selectedLightType = 0;

        private static string _newObjectName = "NewObject_1";
        private static string _newDecalName = "glow10";

        public static bool ShowCollisionBoxes = false;
        public static bool DisableDepthTest = false;
        public static bool ShowStartPoints = false;
        public static bool ShowHealthPacks = false;
        public static bool ShowFlagObjects = false;

        private static void DebugOverlay()
        {
            ImGuiWindowFlags windowFlags = ImGuiWindowFlags.NoDecoration |
                               ImGuiWindowFlags.NoInputs |
                               ImGuiWindowFlags.AlwaysAutoResize |
                               ImGuiWindowFlags.NoNav |
                               ImGuiWindowFlags.NoFocusOnAppearing |
                               ImGuiWindowFlags.NoBringToFrontOnFocus;

            ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.Always);

            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.9f);

            if (ImGui.Begin("Overlay", windowFlags))
            {
                var viewAngles = Camera.GetEulerAngles();

                ImGui.TextColored(new Vector4(0, 1, 0, 1), $"FPS: {1f / ImGui.GetIO().DeltaTime:F0}");
                ImGui.Text($"Position: {Camera.camPos.X:F0}, {Camera.camPos.Y:F0}, {Camera.camPos.Z:F0}");
                ImGui.Text($"Angles: {viewAngles.X:F4}, {viewAngles.Y:F4}");
            }
            ImGui.End();

            ImGui.PopStyleVar();
        }

        private static void TextureViewer()
        {

            ImGui.Begin("Scene Textures");
            {
                int column = 0;
                int row = 0;

                foreach (var texture in Program.textureCache)
                {
                    ImGui.PushID(row + column * 5);

                    // max of 5 rows
                    if (row == 5)
                    {
                        column++;
                        row = 0;
                    }
                    else
                    {
                        ImGui.SameLine();
                    }

                    row++;

                    ImGui.ImageButton("", (nint)texture.Value.Handle, new Vector2(50, 50));
                    ImGui.SetItemTooltip(texture.Key);
                    ImGui.PopID();
                }
            }
            ImGui.End();
        }

        private static void LightsDebug()
        {
            if (ImGui.TreeNode("Lights"))
            {
                if (ImGui.Button("Add new light..."))
                {
                    _showAddLightModal = true;
                    ImGui.OpenPopup("AddLightModal");
                }

                if (ImGui.BeginPopupModal("AddLightModal", ref _showAddLightModal, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    if (ImGui.BeginCombo("Light type", ((LightObject.LightType)_selectedLightType).ToString()))
                    {
                        foreach (var type in Enum.GetValues(typeof(LightObject.LightType)))
                        {
                            if (ImGui.Selectable(type.ToString(), _selectedLightType == (int)type))
                            {
                                _selectedLightType = (int)type;
                            }
                        }
                        ImGui.EndCombo();
                    }

                    if (ImGui.Button("Create", new Vector2(120, 0)))
                    {
                        Program.sceneOPT.Lights.Add(new LightObject
                        {
                            Type = LightObject.LightType.POINT_LIGHT,
                            DiffuseColor = new Color(1f, 1f, 1f, 1f),
                            AmbientColor = new Color(0.2f, 0.2f, 0.2f, 1f),
                            SpecularColor = new Color(1f, 1f, 1f, 1f),
                            Point = new LightObject.PointLight
                            {
                                Position = Vector3.Zero,
                                Start = 0,
                                End = 10,
                                Radius = 5,
                                UsesRadius = true
                            }
                        });

                        ImGui.CloseCurrentPopup();
                        _showAddLightModal = false;
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    {
                        ImGui.CloseCurrentPopup();
                        _showAddLightModal = false;
                    }

                    ImGui.EndPopup();
                }

                for (int i = 0; i < Program.sceneOPT.Lights.Count; i++)
                {
                    var currLight = Program.sceneOPT.Lights[i];

                    ImGui.PushID(i);

                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1.0f)); // Dark Red
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                    if (ImGui.Button("X"))
                    {
                        Program.sceneOPT.Lights.RemoveAt(i);
                        i--;
                        ImGui.PopStyleColor(2);
                        ImGui.PopID();
                        continue;
                    }
                    ImGui.SetItemTooltip("Delete object");
                    ImGui.PopStyleColor(2);

                    ImGui.SameLine();

                    if (ImGui.TreeNode(currLight.Type.ToString()))
                    {
                        if (currLight.Type == LightObject.LightType.POINT_LIGHT)
                        {
                            ImGui.DragFloat3("Light Position", ref currLight.Point.Position);
                            if (ImGui.Button("Go to light"))
                            {
                                Camera.camPos = currLight.Point.Position * new Vector3(1, 1, -1);
                            }
                            if (currLight.Point.UsesRadius)
                            {
                                ImGui.SliderFloat("Light Radius", ref currLight.Point.Radius, 0f, 1000f);
                            }
                            else
                            {
                                ImGui.SliderFloat("Light Start", ref currLight.Point.Start, 0f, 1000f);
                                ImGui.SliderFloat("Light End", ref currLight.Point.End, 0f, 1000f);
                            }
                        }
                        else if (currLight.Type == LightObject.LightType.SPOT_LIGHT)
                        {
                            ImGui.DragFloat3("Light Position", ref currLight.Spot.Position);
                            if (ImGui.Button("Go to light"))
                            {
                                Camera.camPos = currLight.Spot.Position * new Vector3(1, 1, -1);
                            }
                            ImGui.SliderFloat3("Light Direction", ref currLight.Spot.Direction, -1f, 1f);
                            ImGui.DragFloat("Light Cone Radius", ref currLight.Spot.ConeRadius);
                        }
                        else if (currLight.Type == LightObject.LightType.DIRECTIONAL_LIGHT)
                        {
                            ImGui.SliderFloat3("Light Direction", ref currLight.Directional.Direction, -1f, 1f);
                        }

                        Vector3 diffuse = new Vector3(currLight.DiffuseColor.r, currLight.DiffuseColor.g, currLight.DiffuseColor.b);
                        Vector3 ambient = new Vector3(currLight.AmbientColor.r, currLight.AmbientColor.g, currLight.AmbientColor.b);
                        Vector3 specular = new Vector3(currLight.SpecularColor.r, currLight.SpecularColor.g, currLight.SpecularColor.b);

                        if (ImGui.ColorEdit3("Diffuse Color", ref diffuse)) currLight.DiffuseColor = new Color(diffuse.X, diffuse.Y, diffuse.Z, 1f);
                        if (ImGui.ColorEdit3("Ambient Color", ref ambient)) currLight.AmbientColor = new Color(ambient.X, ambient.Y, ambient.Z, 1f);
                        if (ImGui.ColorEdit3("Specular Color", ref specular)) currLight.SpecularColor = new Color(specular.X, specular.Y, specular.Z, 1f);

                        ImGui.TreePop();
                    }
                    ImGui.PopID();
                }

                ImGui.TreePop();
            }
        }

        private static void DecalsDebug()
        {
            if (ImGui.TreeNode("Decals"))
            {
                if (ImGui.Button("Add new decal..."))
                {
                    _showAddDecalModal = true;
                    ImGui.OpenPopup("AddDecalModal");
                }

                if (ImGui.BeginPopupModal("AddDecalModal", ref _showAddDecalModal, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.InputText("Decal Name/Texture", ref _newDecalName, 128);

                    ImGui.Separator();

                    if (ImGui.Button("Create", new Vector2(120, 0)))
                    {
                        Program.AddTextureToCache(_newDecalName, vtPackParser.TexturesFolder + _newDecalName);

                        Program.sceneOPT.Decals.Add(new DecalObject
                        {
                            Name = _newDecalName,
                            Position = Vector3.Zero,
                            ScaleX = 1f,
                            ScaleY = 1f,
                            ZOffset = 0f
                        });

                        ImGui.CloseCurrentPopup();
                        _showAddDecalModal = false;
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    {
                        ImGui.CloseCurrentPopup();
                        _showAddDecalModal = false;
                    }

                    ImGui.EndPopup();
                }

                for (int i = 0; i < Program.sceneOPT.Decals.Count; i++)
                {
                    var currDecal = Program.sceneOPT.Decals[i];

                    ImGui.PushID(i);

                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1.0f)); // Dark Red
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                    if (ImGui.Button("X"))
                    {
                        Program.sceneOPT.Decals.RemoveAt(i);
                        i--;
                        ImGui.PopStyleColor(2);
                        ImGui.PopID();
                        continue;
                    }
                    ImGui.SetItemTooltip("Delete object");
                    ImGui.PopStyleColor(2);

                    ImGui.SameLine();

                    if (ImGui.TreeNode(currDecal.Name))
                    {
                        if (ImGui.InputText("Decal Name/Texture", ref currDecal.Name, 128)) Program.AddTextureToCache(currDecal.Name, vtPackParser.TexturesFolder + currDecal.Name + ".tex");

                        ImGui.DragFloat3("Decal Position", ref currDecal.Position);
                        if (ImGui.Button("Go to Decal"))
                        {
                            Camera.camPos = currDecal.Position * new Vector3(1, 1, -1);
                        }
                        ImGui.DragFloat("Decal Scale X", ref currDecal.ScaleX);
                        ImGui.DragFloat("Decal Scale Y", ref currDecal.ScaleY);
                        ImGui.DragFloat("Decal Z Offset", ref currDecal.ZOffset);

                        ImGui.TreePop();
                    }
                    ImGui.PopID();
                }

                ImGui.TreePop();
            }
        }

        private static void SceneDebug()
        {
            if (ImGui.TreeNode("Scene"))
            {
                var currScene = Program.scene;

                ImGui.DragFloat("Fog near plane", ref currScene.FogNear);
                ImGui.DragFloat("Fog far plane", ref currScene.FogFar);

                Vector3 fogColor = new Vector3(currScene.FogColor.r, currScene.FogColor.g, currScene.FogColor.b);

                ImGui.ColorEdit3("Fog color", ref fogColor);

                currScene.FogColor = new Color(fogColor.X, fogColor.Y, fogColor.Z, 1f);

                ImGui.DragFloat("Camera near plane", ref currScene.CamNear);
                ImGui.DragFloat("Camera far plane", ref currScene.CamFar);

                if (ImGui.TreeNode("Modules"))
                {
                    // Todo: adding or removing modules doesn't work when repackaging the vpk, probably an issue with my repacker but not sure yet
                    if (ImGui.Button("Add new module..."))
                    {
                        _showAddModuleModal = true;
                        ImGui.OpenPopup("AddModuleModal");
                    }

                    if (ImGui.BeginPopupModal("AddModuleModal", ref _showAddModuleModal, ImGuiWindowFlags.AlwaysAutoResize))
                    {
                        ImGui.Text("Select module type:");

                        // Dropdown to pick which IGameModule implementation to use
                        if (ImGui.BeginCombo("Module type", _selectedModuleType?.Name ?? "Select type..."))
                        {
                            foreach (var entry in IGameModule.Modules)
                            {
                                if (ImGui.Selectable(entry.Key, _selectedModuleType == entry.Value))
                                {
                                    _selectedModuleType = entry.Value;
                                }
                            }
                            ImGui.EndCombo();
                        }

                        ImGui.InputText("Object name", ref _newObjectName, 128);

                        ImGui.Separator();

                        if (_selectedModuleType != null)
                        {
                            if (ImGui.Button("Create", new Vector2(120, 0)))
                            {
                                VTModule.CreateNewModuleInstance(_selectedModuleType, _newObjectName);
                                ImGui.CloseCurrentPopup();
                                _showAddModuleModal = false;
                            }
                        }

                        ImGui.SameLine();
                        if (ImGui.Button("Cancel", new Vector2(120, 0)))
                        {
                            ImGui.CloseCurrentPopup();
                            _showAddModuleModal = false;
                        }

                        ImGui.EndPopup();
                    }

                    foreach (var moduleEntry in IGameModule.Modules)
                    {
                        Type moduleType = moduleEntry.Value;

                        var listField = moduleType.GetFields(BindingFlags.Static | BindingFlags.Public)
                            .FirstOrDefault(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>));

                        if (listField == null) continue;

                        var list = listField.GetValue(null) as System.Collections.IList;
                        if (list == null || list.Count == 0) continue;

                        // Category header
                        if (ImGui.TreeNode($"{moduleType.Name}###{moduleType.FullName}"))
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                var obj = list[i] as VTModule;
                                if (obj == null) continue;

                                ImGui.PushID(i);

                                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1.0f)); // Dark Red
                                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                                if (ImGui.Button("X"))
                                {
                                    list.RemoveAt(i);
                                    i--;
                                    ImGui.PopStyleColor(2);
                                    ImGui.PopID();
                                    continue;
                                }
                                ImGui.SetItemTooltip("Delete object");
                                ImGui.PopStyleColor(2);

                                ImGui.SameLine();
                                if (ImGui.Button("D")) // D for Duplicate
                                {
                                    DuplicateModule(moduleType, obj);
                                }
                                ImGui.SetItemTooltip("Duplicate object");

                                ImGui.SameLine();

                                // Item header
                                string label = $"{obj.GetType().Name}: {obj.ObjectName}";
                                if (ImGui.TreeNode(label))
                                {
                                    DrawRecursiveProperties(obj);
                                    ImGui.TreePop();
                                }

                                ImGui.PopID();
                            }
                            ImGui.TreePop();
                        }
                    }
                    ImGui.TreePop();
                }

                ImGui.TreePop();
            }
        }

        public static void OnRender()
        {
            if (_showErrorModal)
            {
                ImGui.OpenPopup(_errorModalTitle); 
            }

            ImGui.SetNextWindowPos(ImGui.GetIO().DisplaySize / 2f - new Vector2(450 / 2f, 0), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(450, 0), ImGuiCond.Always);

            if (ImGui.BeginPopupModal(_errorModalTitle, ref _showErrorModal, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped(_errorModalMessage);
                ImGui.Separator();

                float size = 120 + ImGui.GetStyle().FramePadding.X * 2.0f;
                float avail = ImGui.GetContentRegionAvail().X;

                float off = (avail - size) * 0.5f;
                if (off > 0.0f)
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + off);

                if (ImGui.Button("Dismiss", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                    _showErrorModal = false;
                }

                ImGui.EndPopup();
            }

            DebugOverlay();
            TextureViewer();

            ImGui.Begin("DEBUG");
            {
                // Todo: currently, exporting scenes is the easiest to implement off all file types, since others need support for multiple file versions
                if (ImGui.Button("Save scene"))
                {
                    Program.scene.Write(Program.scene.SceneFile);
                }
                ImGui.SetItemTooltip("Saves scene file so it can be used in-game after repackaging the VPK.");

                if (ImGui.Button("Repackage VPK"))
                {
                    Program.packer.Repack(vtPackParser.DefaultOutputFolder, vtPackParser.DefaultExportFolder + vtPackParser.DefaultOutPackName);
                }
                ImGui.SetItemTooltip($"Exports the current file system in '{vtPackParser.DefaultOutputFolder}' to '{vtPackParser.DefaultExportFolder + vtPackParser.DefaultOutPackName}', which can be used as a direct game replacement.");

                if (ImGui.Button("Reload file system"))
                {
                    _showReloadModal = true;
                    ImGui.OpenPopup("ReloadConfirmation");
                }
                ImGui.SetItemTooltip($"Unpacks and re-loads the given '{vtPackParser.DefaultInPackName}' file.");

                if (ImGui.BeginPopupModal("ReloadConfirmation", ref _showReloadModal, ImGuiWindowFlags.AlwaysAutoResize))
                {
                    ImGui.TextWrapped($"This action is irreversible. Remember to back-up your '{vtPackParser.DefaultOutputFolder}' folder or files to keep changes!");
                    ImGui.Separator();

                    if (ImGui.Button("Continue", new Vector2(120, 0)))
                    {
                        Program.packer.Unpack(vtPackParser.DefaultInPackName, vtPackParser.DefaultOutputFolder);
                        Program.ParseScene(Program.scene.SceneFile);

                        _showReloadModal = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SetItemDefaultFocus();

                    ImGui.SameLine();
                    if (ImGui.Button("Cancel", new Vector2(120, 0)))
                    {
                        _showReloadModal = false;
                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }

                ImGui.Checkbox("Show collision boxes", ref ShowCollisionBoxes);
                ImGui.Checkbox("Disable debug depth test", ref DisableDepthTest);
                ImGui.Checkbox("Show start/spawn points", ref ShowStartPoints);
                ImGui.Checkbox("Show health packs", ref ShowHealthPacks);
                ImGui.Checkbox("Show flag objects", ref ShowFlagObjects);

                if (ImGui.TreeNode("Scene list"))
                {
                    if (_scenesNeedsRefresh)
                    {
                        _scenes.Clear();

                        string currentDirectory = vtPackParser.ScenesFolder;
                        string[] files = Directory.GetFiles(currentDirectory);

                        foreach (string file in files)
                        {
                            if (file.EndsWith(".scn")) _scenes.Add(Path.Combine(currentDirectory, Path.GetFileName(file)));
                        }

                        _scenesNeedsRefresh = false;
                    }

                    if (ImGui.BeginTable("Scenes", 2))
                    {
                        for (int i = 0; i < _scenes.Count; i++)
                        {
                            var sceneFile = _scenes[i];

                            ImGui.TableNextColumn();
                            if (ImGui.Selectable(Path.GetFileName(sceneFile), _selectedScene == i))
                            {
                                _selectedScene = i;
                            }
                        }
                        ImGui.EndTable();
                    }

                    if (ImGui.Button("Refresh Scenes"))
                    {
                        _scenesNeedsRefresh = true;
                    }

                    if (ImGui.Button("Load Scene"))
                    {
                        Program.ParseScene(_scenes[_selectedScene]);
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNode("Maps list"))
                {
                    if (_mapsNeedsRefresh)
                    {
                        _maps.Clear();

                        string currentDirectory = vtPackParser.ModelsFolder;
                        string[] files = Directory.GetFiles(currentDirectory);

                        foreach (string file in files)
                        {
                            if (file.EndsWith(".opt")) _maps.Add(Path.Combine(currentDirectory, Path.GetFileName(file)));
                        }

                        _mapsNeedsRefresh = false;
                    }

                    if (ImGui.BeginTable("Maps", 2))
                    {
                        for (int i = 0; i < _maps.Count; i++)
                        {
                            var sceneFile = _maps[i];

                            ImGui.TableNextColumn();
                            if (ImGui.Selectable(Path.GetFileName(sceneFile), _selectedMap == i))
                            {
                                _selectedMap = i;
                            }
                        }
                        ImGui.EndTable();
                    }

                    if (ImGui.Button("Refresh Maps"))
                    {
                        _mapsNeedsRefresh = true;
                    }

                    if (ImGui.Button("Load Map"))
                    {
                        Program.ParseScene(string.Empty, skipSceneParse: true, customMapName: _maps[_selectedMap], customHeightMapName: _maps[_selectedMap].Replace(".opt", ".hm"));
                    }

                    ImGui.TreePop();
                }

                LightsDebug();
                DecalsDebug();
                SceneDebug();
            }
            ImGui.End();
        }

        private static void DrawRecursiveProperties(object obj)
        {
            // We only care about public instance fields for these data objects
            var fields = obj.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var field in fields)
            {
                // Skip VTModule metadata
                if (field.Name == "ModuleName" || field.Name == "ModuleData") continue;
                
                var value = field.GetValue(obj);
                string name = field.Name;

                if (field.FieldType == typeof(string))
                {
                    string val = (string)value ?? "";
                    if (ImGui.InputText(name, ref val, 512)) field.SetValue(obj, val);
                }
                else if (field.FieldType == typeof(int))
                {
                    int val = (int)value;
                    if (ImGui.InputInt(name, ref val)) field.SetValue(obj, val);
                }
                else if (field.FieldType == typeof(float))
                {
                    float val = (float)value;
                    if (ImGui.DragFloat(name, ref val, 0.1f)) field.SetValue(obj, val);
                }
                else if (field.FieldType == typeof(Vector3))
                {
                    Vector3 val = (Vector3)value;
                    if (ImGui.DragFloat3(name, ref val, 1.0f)) field.SetValue(obj, val);
                    if (name.Contains("Position"))
                    {
                        if (ImGui.Button("Go to position"))
                        {
                            Camera.camPos = val * new Vector3(1, 1, -1);
                        }
                    }
                }
                else if (field.FieldType == typeof(Matrix4x4))
                {
                    Matrix4x4 mat = (Matrix4x4)field.GetValue(obj);

                    if (Matrix4x4.Decompose(mat, out Vector3 scale, out Quaternion rot, out Vector3 pos))
                    {
                        bool changed = false;
                        Vector3 rotationEuler = Helpers.ExtractEulerAngles(mat);

                        ImGui.TextDisabled($"--- {name} ---");
                        if (ImGui.DragFloat3($"Pos##{name}", ref pos, 1.0f)) changed = true;
                        if (ImGui.DragFloat3($"Rot##{name}", ref rotationEuler, 0.5f)) changed = true;
                        if (ImGui.DragFloat3($"Scale##{name}", ref scale, 0.05f)) changed = true;

                        if (ImGui.Button("Go to position"))
                        {
                            Camera.camPos = pos * new Vector3(1, 1, -1);
                        }

                        if (changed)
                        {
                            Matrix4x4 newMat = Matrix4x4.CreateScale(scale) *
                                               Matrix4x4.CreateFromYawPitchRoll(
                                                   rotationEuler.Y * (MathF.PI / 180f),
                                                   rotationEuler.X * (MathF.PI / 180f),
                                                   rotationEuler.Z * (MathF.PI / 180f)) *
                                               Matrix4x4.CreateTranslation(pos);
                            field.SetValue(obj, newMat);
                        }
                    }
                }
            }
        }

        private static void DuplicateModule(Type moduleType, VTModule original)
        {
            var listField = moduleType.GetFields(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>));

            if (listField == null) return;

            Type objectType = original.GetType();
            var createMethod = typeof(VTModule).GetMethod("Create").MakeGenericMethod(objectType);

            // Create new instance with same data
            var newObj = createMethod.Invoke(null, new object[] { original }) as VTModule;

            // Append "_copy" to the name to distinguish it
            newObj.ObjectName += "_copy";

            var list = listField.GetValue(null) as System.Collections.IList;
            list?.Add(newObj);
        }
    }
}
