using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Reflection;
using System.Text;
using VToyEditor.Modules;

namespace VToyEditor.Parsers
{
    public class VTSCNParser
    {
        public string SceneFile { get; private set; }

        public string MapName = "";
        public string HeightMapName = "";
        public Vector3 ShadowLightDirection = new Vector3(0, 0, -1);
        public string SkyTextureName = "";
        public Color FogColor;
        public float FogNear;
        public float FogFar;
        public float CamNear;
        public float CamFar;
        public string AmbientMusicName = "";

        public List<VTModule> UncaughtModules = new List<VTModule>();
        public VTModule MainModule = new VTModule();
        public uint NumModules = 0;

        public void Parse(string path)
        {
            SceneFile = path;

            // Remove modules from last scene (if they exist)
            foreach (var moduleEntry in IGameModule.Modules)
            {
                Type moduleType = moduleEntry.Value;

                var listField = moduleType.GetFields(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>));

                if (listField == null) continue;

                var list = listField.GetValue(null) as System.Collections.IList;
                if (list == null || list.Count == 0) continue;

                // Remove all objects
                list.Clear(); // Known, implemented modules
                UncaughtModules.Clear(); // Unknown, unimplemented modules (we just store the raw data for these, so we can write them back unmodified)
            }

            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs);

            var magicBytes = reader.ReadBytes(4);
            string magic = System.Text.Encoding.UTF8.GetString(magicBytes);

            // First game and the online variant both use the same format
            if (magic != "VTS1") throw new Exception($"Unknown Magic: {magic}");

            uint modulesCount = reader.ReadUInt32();

            NumModules = modulesCount;

            uint objectNameSize = reader.ReadUInt32();
            string objectName = Helpers.ReadString(reader, objectNameSize);

            uint mainModuleNameSize = reader.ReadUInt32();
            string mainModuleName = Helpers.ReadString(reader, mainModuleNameSize);

            // Unused for now because all modules use the same header format (That I know of)
            uint descriptorSize = reader.ReadUInt32(); // Engine reads the following as a string and sends it over to the mainModule

            // Write over the main module data so we can write this scene back later
            MainModule.ObjectName = objectName;
            MainModule.ModuleName = mainModuleName;
            MainModule.ModuleData = new byte[(int)descriptorSize]; // We won't fill this data, we just want to keep the size for writing later, the data is saved below already

            uint mapNameSize = reader.ReadUInt32();
            string mapName = Helpers.ReadString(reader, mapNameSize);

            uint heightMapNameSize = reader.ReadUInt32();
            string heightMapName = Helpers.ReadString(reader, heightMapNameSize);

            Vector3 shadowLightDirectionRaw = Helpers.ReadVector3(reader);

            Vector3 shadowLightDirection = shadowLightDirectionRaw;
            float dirMagnitude = shadowLightDirection.Length();
            if (dirMagnitude == 0f)
            {
                shadowLightDirection = new Vector3(0, 0, 1);
            }
            else
            {
                float dirLength = 1f / dirMagnitude;

                shadowLightDirection *= dirLength;
            }

            uint skyTextureNameSize = reader.ReadUInt32();
            string skyTextureName = Helpers.ReadString(reader, skyTextureNameSize);
            
            int fogColorRaw = reader.ReadInt32();
            int fogR = (fogColorRaw & 0xff0000) >> 16;
            int fogG = (fogColorRaw & 0xff00) >> 8;
            int fogB = (fogColorRaw & 0xff);

            float fogNear = reader.ReadSingle();
            float fogFar = reader.ReadSingle();
            float cameraNear = reader.ReadSingle();
            float cameraFar = reader.ReadSingle();

            uint ambientMusicNameSize = reader.ReadUInt32();
            string ambientMusicName = Helpers.ReadString(reader, ambientMusicNameSize);

            MapName = mapName;
            HeightMapName = heightMapName;

            ShadowLightDirection = shadowLightDirectionRaw;
            SkyTextureName = skyTextureName;

            FogColor = new Color(fogR / 255f, fogG / 255f, fogB / 255f, 1f);
            FogNear = fogNear;
            FogFar = fogFar;

            CamNear = cameraNear;
            CamFar = cameraFar;

            AmbientMusicName = ambientMusicName;

            // Emulate module functionality
            for (int i = 0; i < modulesCount; i++)
            {
                uint modObjectNameSize = reader.ReadUInt32();
                string modObjectName = Helpers.ReadString(reader, modObjectNameSize);

                uint moduleNameSize = reader.ReadUInt32();
                string moduleName = Helpers.ReadString(reader, moduleNameSize);

                uint moduleDataSize = reader.ReadUInt32();
                byte[] moduleData = reader.ReadBytes((int)moduleDataSize);

                string cleanName = System.IO.Path.GetFileNameWithoutExtension(moduleName);

                var module = new VTModule()
                {
                    ObjectName = modObjectName,
                    ModuleName = moduleName,
                    ModuleData = moduleData
                };

                Console.Write($"[{modObjectName}] ");
                if (IGameModule.Modules.TryGetValue(cleanName, out Type moduleType))
                {
                    var instance = (IGameModule)Activator.CreateInstance(moduleType);
                    instance.Run(module);
                }
                else
                {
                    UncaughtModules.Add(module);

                    Console.WriteLine($"No class found for module '{cleanName}'");
                }
            }
        }

        public void Write(string path)
        {
            using var fs = File.OpenWrite(path);
            using var writer = new BinaryWriter(fs);

            // File header
            writer.Write(Encoding.UTF8.GetBytes("VTS1"));

            writer.Write(BitConverter.GetBytes(NumModules));

            // Main module data
            writer.Write(BitConverter.GetBytes(MainModule.ObjectName.Length + 1));
            writer.Write(Encoding.UTF8.GetBytes(MainModule.ObjectName + "\0"));

            writer.Write(BitConverter.GetBytes(MainModule.ModuleName.Length + 1));
            writer.Write(Encoding.UTF8.GetBytes(MainModule.ModuleName + "\0"));

            writer.Write(BitConverter.GetBytes(MainModule.ModuleData.Length));

            // Write actual scene parameters
            writer.Write(BitConverter.GetBytes(MapName.Length + 1));
            writer.Write(Encoding.UTF8.GetBytes(MapName + "\0"));

            writer.Write(BitConverter.GetBytes(HeightMapName.Length + 1));
            writer.Write(Encoding.UTF8.GetBytes(HeightMapName + "\0"));

            writer.Write(BitConverter.GetBytes(ShadowLightDirection.X));
            writer.Write(BitConverter.GetBytes(ShadowLightDirection.Y));
            writer.Write(BitConverter.GetBytes(ShadowLightDirection.Z));

            writer.Write(BitConverter.GetBytes(SkyTextureName.Length + 1));
            writer.Write(Encoding.UTF8.GetBytes(SkyTextureName + "\0"));

            byte fogR = (byte)(FogColor.r * 255f);
            byte fogG = (byte)(FogColor.g * 255f);
            byte fogB = (byte)(FogColor.b * 255f);

            writer.Write(fogB);
            writer.Write(fogG);
            writer.Write(fogR);
            writer.Write((byte)'\0');

            writer.Write(BitConverter.GetBytes(FogNear));
            writer.Write(BitConverter.GetBytes(FogFar));

            writer.Write(BitConverter.GetBytes(CamNear));
            writer.Write(BitConverter.GetBytes(CamFar));

            writer.Write(BitConverter.GetBytes(AmbientMusicName.Length + 1));
            writer.Write(Encoding.UTF8.GetBytes(AmbientMusicName + "\0"));

            // Write over known, emulated modules (with their modifications)
            IEnumerable<Type> implementingTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => typeof(IGameModule).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);

            foreach (Type type in implementingTypes)
            {
                MethodInfo method = type.GetMethod("WriteObjects", BindingFlags.Static | BindingFlags.Public);

                if (method != null)
                {
                    _ = method.Invoke(null, [writer]);
                }
            }

            // Write over unknown modules (unmodified)
            for (int i = 0; i < UncaughtModules.Count; i++)
            {
                // Todo: what the hell is this edge case?
                if (i == UncaughtModules.Count - 1)
                {
                    // What is this data supposed to mean? if I don't do this, the scene cannot be loaded properly
                    writer.Write(new byte[4]);
                    writer.Write(new byte[4]);
                    writer.Write(new byte[4]);
                    writer.Write(new byte[4]);
                    break;
                }

                var mod = UncaughtModules[i];
                writer.Write(BitConverter.GetBytes(mod.ObjectName.Length + 1));
                writer.Write(Encoding.UTF8.GetBytes(mod.ObjectName + "\0"));
                
                writer.Write(BitConverter.GetBytes(mod.ModuleName.Length + 1));
                writer.Write(Encoding.UTF8.GetBytes(mod.ModuleName + "\0"));

                writer.Write(BitConverter.GetBytes(mod.ModuleData.Length));
                writer.Write(mod.ModuleData);
            }
        }
    }
}
