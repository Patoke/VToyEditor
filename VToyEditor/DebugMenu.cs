using System;
using System.Collections.Generic;
using System.Text;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using ImGuiNET;
using System.Numerics;

namespace VToyEditor
{
    public static class DebugMenu
    {
        private static bool _needsRefresh = true;
        private static int _selectedScene = 0;
        private static List<string> _scenes = new List<string>();

        public static void OnRender()
        {
            ImGui.Begin("DEBUG");
            {
                if (_needsRefresh)
                {
                    _scenes.Clear();

                    string currentDirectory = Directory.GetCurrentDirectory();
                    string[] files = Directory.GetFiles(currentDirectory);

                    foreach (string file in files)
                    {
                        if (file.Contains(".opt")) _scenes.Add(Path.GetFileName(file));
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
                    Program.ParseScene(_scenes[_selectedScene]);
                }

                if (ImGui.TreeNode("Lights"))
                {
                    for (int i = 0; i < Program.scene.Lights.Count; i++)
                    {
                        var currLight = Program.scene.Lights[i];

                        ImGui.PushID(i);
                        if (ImGui.TreeNode(currLight.Type.ToString()))
                        {
                            if (currLight.Type == LightObject.LightType.POINT_LIGHT)
                            {
                                ImGui.SliderFloat3("Light Position", ref currLight.Point.Position, -50000f, 50000f);
                                if (ImGui.Button("Go to light"))
                                {
                                    Camera.camPos = currLight.Point.Position;
                                }
                                if (currLight.Point.UsesRadius)
                                {
                                    ImGui.SliderFloat("Light Radius", ref currLight.Point.Radius, 0f, 100f);
                                }
                                else
                                {
                                    ImGui.SliderFloat("Light Start", ref currLight.Point.Start, 0f, 100f);
                                    ImGui.SliderFloat("Light End", ref currLight.Point.End, 0f, 100f);
                                }
                            }
                            else if (currLight.Type == LightObject.LightType.SPOT_LIGHT)
                            {
                                ImGui.SliderFloat3("Light Position", ref currLight.Spot.Position, -50000f, 50000f);
                                if (ImGui.Button("Go to light"))
                                {
                                    Camera.camPos = currLight.Spot.Position;
                                }
                                ImGui.SliderFloat3("Light Direction", ref currLight.Spot.Direction, -1f, 1f);
                                ImGui.SliderFloat("Light Cone Radius", ref currLight.Spot.ConeRadius, 0f, 100f);
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
            }
            ImGui.End();
        }
    }
}
