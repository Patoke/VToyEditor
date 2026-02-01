using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Text;
using VToyEditor.Modules;

namespace VToyEditor.Parsers
{
    public class VTSCNParser
    {
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

        public void Parse(string path)
        {
            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs);

            var magicBytes = reader.ReadBytes(4);
            string magic = System.Text.Encoding.UTF8.GetString(magicBytes);

            // First game and the online variant both use the same format
            if (magic != "VTS1") throw new Exception($"Unknown Magic: {magic}");

            uint modulesCount = reader.ReadUInt32();

            uint objectNameSize = reader.ReadUInt32();
            string objectName = Helpers.ReadString(reader, objectNameSize);

            uint mainModuleNameSize = reader.ReadUInt32();
            string mainModuleName = Helpers.ReadString(reader, mainModuleNameSize);

            // Unused for now because all modules use the same header format (That I know of)
            uint descriptorSize = reader.ReadUInt32(); // Engine reads the following as a string and send it over to the mainModule

            uint mapNameSize = reader.ReadUInt32();
            string mapName = Helpers.ReadString(reader, mapNameSize);

            uint heightMapNameSize = reader.ReadUInt32();
            string heightMapName = Helpers.ReadString(reader, heightMapNameSize);

            Vector3 shadowLightDirection = Helpers.ReadVector3(reader);

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

            ShadowLightDirection = shadowLightDirection;
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

                if (IGameModule.Modules.TryGetValue(cleanName, out Type moduleType))
                {
                    var instance = (IGameModule)Activator.CreateInstance(moduleType);
                    instance.Run(moduleData);
                }
                else
                {
                    Console.WriteLine($"No class found for module '{cleanName}'");
                }
            }
        }
    }
}
