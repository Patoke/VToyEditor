using System;
using System.Collections.Generic;
using System.Text;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using ImGuiNET;
using System.Numerics;
using VToyEditor.Parsers;

namespace VToyEditor
{
    public static class DebugMenu
    {
        private static bool _needsRefresh = true;
        private static int _selectedScene = 0;
        private static List<string> _scenes = new List<string>();

        public static bool ShowCollisionBoxes = false;
        public static bool DisableDepthTest = false;

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
                ImGui.Checkbox("Show collision boxes", ref ShowCollisionBoxes);

                if (ShowCollisionBoxes)
                {
                    ImGui.Checkbox("Disable box depth test", ref DisableDepthTest);
                }

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
                                ImGui.InputFloat3("Light Position", ref currLight.Point.Position);
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
                                ImGui.InputFloat3("Light Position", ref currLight.Spot.Position);
                                if (ImGui.Button("Go to light"))
                                {
                                    Camera.camPos = currLight.Spot.Position * new Vector3(1, 1, -1);
                                }
                                ImGui.SliderFloat3("Light Direction", ref currLight.Spot.Direction, -1f, 1f);
                                ImGui.InputFloat("Light Cone Radius", ref currLight.Spot.ConeRadius);
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
                            ImGui.InputFloat3("Decal Position", ref currDecal.Position);
                            if (ImGui.Button("Go to Decal"))
                            {
                                Camera.camPos = currDecal.Position * new Vector3(1, 1, -1);
                            }
                            ImGui.InputFloat("Decal Scale X", ref currDecal.ScaleX);
                            ImGui.InputFloat("Decal Scale Y", ref currDecal.ScaleY);
                            ImGui.InputFloat("Decal Z Offset", ref currDecal.ZOffset);

                            ImGui.TreePop();
                        }
                        ImGui.PopID();
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNode("Scene"))
                {
                    var currScene = Program.scene;

                    ImGui.InputFloat("Fog near plane", ref currScene.FogNear);
                    ImGui.InputFloat("Fog far plane", ref currScene.FogFar);

                    Vector3 fogColor = new Vector3(currScene.FogColor.r, currScene.FogColor.g, currScene.FogColor.b);
                    
                    ImGui.ColorEdit3("Fog color", ref fogColor);

                    currScene.FogColor = new Color(fogColor.X, fogColor.Y, fogColor.Z, 1f);

                    ImGui.InputFloat("Camera near plane", ref currScene.CamNear);
                    ImGui.InputFloat("Camera far plane", ref currScene.CamFar);

                    ImGui.TreePop();
                }
            }
            ImGui.End();
        }
    }
}
