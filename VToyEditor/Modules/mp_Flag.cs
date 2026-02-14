using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace VToyEditor.Modules
{
    public class FlagObject : VTModule
    {
        public string ModelName;
        public Matrix4x4 Transform;
        public string UnkString1;
        public string UnkString2;
        public uint FlagTeam;
    }

    public class mp_Flag : IGameModule
    {
        public static List<FlagObject> flagObjects = new List<FlagObject>();

        public void Run(VTModule data)
        {
            var reader = new Helpers.ByteReader { data = data.ModuleData };

            uint modelNameSize = Math.Max(reader.GetUInt32(), 0);
            string modelName = reader.GetString((int)modelNameSize);

            Matrix4x4 modelTransform = reader.GetMatrix();

            uint unkStringSize = Math.Max(reader.GetUInt32(), 0);
            string unkString = reader.GetString((int)unkStringSize);

            uint unkStringSize2 = Math.Max(reader.GetUInt32(), 0);
            string unkString2 = reader.GetString((int)unkStringSize2);

            uint flagTeam = reader.GetUInt32();

            var fo = VTModule.Create<FlagObject>(data);

            fo.ModelName = modelName;
            fo.Transform = modelTransform;
            fo.UnkString1 = unkString;
            fo.UnkString2 = unkString2;
            fo.FlagTeam = flagTeam;

            flagObjects.Add(fo);

            Console.WriteLine($"mp_Flag, model name: {modelName}, unk string 1: {unkString}, unk string 2: {unkString2}, flag team: {flagTeam}");
        }

        public static void WriteObjects(BinaryWriter writer)
        {
            for (int i = 0; i < flagObjects.Count; i++)
            {
                var hp = flagObjects[i];
                hp.Write(writer);

                using (new Helpers.DataSizeWriterScope(writer))
                {
                }
            }
        }
    }
}
