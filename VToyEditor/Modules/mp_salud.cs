using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace VToyEditor.Modules
{
    public class HealthPackObject : VTModule
    {
        public string ModelName;
        public Matrix4x4 Transform;
        public string UnkString1;
        public string UnkString2;
        public int HealthAmount;
    }

    public class mp_salud : IGameModule
    {
        public static List<HealthPackObject> healthPackObjects = new List<HealthPackObject>();

        public void Run(VTModule data)
        {
            var reader = new Helpers.ByteReader { data = data.ModuleData };
            
            uint modelNameSize = Math.Max(reader.GetUInt32(), 0);
            string modelName = reader.GetString((int)modelNameSize);

            Matrix4x4 modelTransform = reader.GetMatrix();

            uint unkStringSize = Math.Max(reader.GetUInt32(), 0);
            string unkString = reader.GetString((int)unkStringSize);

            uint unkString2Size = Math.Max(reader.GetUInt32(), 0);
            string unkString2 = reader.GetString((int)unkString2Size);

            int healthAmount = reader.GetInt32();
            int fixedHealthAmount = healthAmount == 0 ? 50 : healthAmount;

            var hp = VTModule.Create<HealthPackObject>(data);

            hp.ModelName = modelName;
            hp.Transform = modelTransform;
            hp.UnkString1 = unkString;
            hp.UnkString2 = unkString2;
            hp.HealthAmount = healthAmount;

            healthPackObjects.Add(hp);

            Console.WriteLine($"mp_salud, model name: {modelName}, unk string 1: {unkString}, unk string 2: {unkString2}, heal amount: {fixedHealthAmount}");
        }

        public static void WriteObjects(BinaryWriter writer)
        {
            for (int i = 0; i < healthPackObjects.Count; i++)
            {
                var hp = healthPackObjects[i];
                hp.Write(writer);

                using (new Helpers.DataSizeWriterScope(writer))
                {
                    writer.Write(BitConverter.GetBytes(hp.ModelName.Length + 1));
                    writer.Write(Encoding.UTF8.GetBytes(hp.ModelName + '\0'));

                    writer.Write(Helpers.MatrixAsBytes(hp.Transform));

                    writer.Write(BitConverter.GetBytes(hp.UnkString1.Length + 1));
                    writer.Write(Encoding.UTF8.GetBytes(hp.UnkString1 + '\0'));

                    writer.Write(BitConverter.GetBytes(hp.UnkString2.Length + 1));
                    writer.Write(Encoding.UTF8.GetBytes(hp.UnkString2 + '\0'));

                    writer.Write(BitConverter.GetBytes(hp.HealthAmount));
                }
            }
        }
    }
}
