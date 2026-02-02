using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Text;
using VToyEditor.Modules;
using VToyEditor.Parsers;

namespace VToyEditor
{
    public static class DebugMenu
    {
        private static bool _needsRefresh = true;
        private static int _selectedScene = 0;
        private static List<string> _scenes = new List<string>();

        private static bool _showAddModal = false;
        private static Type _selectedModuleType = null;
        private static string _newObjectName = "NewObject_1";

        public static bool ShowCollisionBoxes = false;
        public static bool DisableDepthTest = false;
        public static bool ShowStartPoints = false;
        public static bool ShowHealthPacks = false;

        public static void OnRender()
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

            ImGui.Begin("DEBUG");
            {
                if (ImGui.Button("Export scene"))
                {
                    string currentDirectory = Directory.GetCurrentDirectory() + "/Export/";
                    var outSceneFile = currentDirectory + Program.scene.MapName.Replace(".opt", ".scn").Replace("nfos\\", string.Empty);

                    if (!Directory.Exists(currentDirectory))
                    {
                        Directory.CreateDirectory(currentDirectory);
                    }
                    using (FileStream fs = File.Create(outSceneFile)) { }

                    Program.scene.Write(outSceneFile);
                }

                ImGui.Checkbox("Show collision boxes", ref ShowCollisionBoxes);
                ImGui.Checkbox("Disable debug depth test", ref DisableDepthTest);
                ImGui.Checkbox("Show start/spawn points", ref ShowStartPoints);
                ImGui.Checkbox("Show health packs", ref ShowHealthPacks);

                if (_needsRefresh)
                {
                    _scenes.Clear();

                    string currentDirectory = Directory.GetCurrentDirectory() + "/scns/";
                    string[] files = Directory.GetFiles(currentDirectory);

                    foreach (string file in files)
                    {
                        if (file.EndsWith(".scn")) _scenes.Add(Path.GetFileName(file));
                    }

                    _needsRefresh = false;
                }

                if (ImGui.BeginTable("Scenes", 2))
                {
                    for (int i = 0; i < _scenes.Count; i++)
                    {
                        var sceneFile = _scenes[i];

                        ImGui.TableNextColumn();
                        if (ImGui.Selectable(sceneFile, _selectedScene == i))
                        {
                            _selectedScene = i;
                        }
                    }
                    ImGui.EndTable();
                }

                if (ImGui.Button("Refresh Scenes"))
                {
                    _needsRefresh = true;
                }

                if (ImGui.Button("Load Scene"))
                {
                    Program.ParseScene("./scns/" + _scenes[_selectedScene]);
                }

                if (ImGui.TreeNode("Lights"))
                {
                    for (int i = 0; i < Program.sceneOPT.Lights.Count; i++)
                    {
                        var currLight = Program.sceneOPT.Lights[i];

                        ImGui.PushID(i);
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
                            ImGui.ColorEdit3("Diffuse Color", ref diffuse);
                            ImGui.ColorEdit3("Ambient Color", ref ambient);
                            ImGui.ColorEdit3("Specular Color", ref specular);

                            currLight.DiffuseColor = new Color(diffuse.X, diffuse.Y, diffuse.Z, 1f);
                            currLight.AmbientColor = new Color(ambient.X, ambient.Y, ambient.Z, 1f);
                            currLight.SpecularColor = new Color(specular.X, specular.Y, specular.Z, 1f);

                            ImGui.TreePop();
                        }
                        ImGui.PopID();
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNode("Decals"))
                {
                    for (int i = 0; i < Program.sceneOPT.Decals.Count; i++)
                    {
                        var currDecal = Program.sceneOPT.Decals[i];

                        ImGui.PushID(i);
                        if (ImGui.TreeNode(currDecal.Name))
                        {
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
                            _showAddModal = true;
                            ImGui.OpenPopup("AddModuleModal");
                        }

                        if (ImGui.BeginPopupModal("AddModuleModal", ref _showAddModal, ImGuiWindowFlags.AlwaysAutoResize))
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
                                    _showAddModal = false;
                                }
                            }

                            ImGui.SameLine();
                            if (ImGui.Button("Cancel", new Vector2(120, 0)))
                            {
                                ImGui.CloseCurrentPopup();
                                _showAddModal = false;
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
