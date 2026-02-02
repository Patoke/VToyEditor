using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace VToyEditor.Modules
{
    public class CarObject : VTModule
    {
        public string ModelName;
        public Matrix4x4 Transform;
    }

    public class mp_CCar : IGameModule
    {
        public static List<CarObject> carObjects = new List<CarObject>();

        public void Run(VTModule data)
        {
            var reader = new Helpers.ByteReader { data = data.ModuleData };

            uint modelNameSize = Math.Max(reader.GetUInt32(), 0);
            string modelName = reader.GetString((int)modelNameSize);

            Matrix4x4 modelTransform = reader.GetMatrix();

            var co = VTModule.Create<CarObject>(data);

            co.ModelName = modelName;
            co.Transform = modelTransform;

            carObjects.Add(co);

            Console.WriteLine($"mp_CCar, model name: {modelName}");
        }

        public static void WriteObjects(BinaryWriter writer)
        {
            for (int i = 0; i < carObjects.Count; i++)
            {
                var co = carObjects[i];
                co.Write(writer);

                using (new Helpers.DataSizeWriterScope(writer))
                {
                    writer.Write(BitConverter.GetBytes(co.ModelName.Length + 1));
                    writer.Write(Encoding.UTF8.GetBytes(co.ModelName + '\0'));

                    writer.Write(Helpers.MatrixAsBytes(co.Transform));

                    // padding? apparently unused in the mp_CCar module but affects size regardless
                    writer.Write(BitConverter.GetBytes((int)1));
                    writer.Write(BitConverter.GetBytes((int)256));
                    writer.Write(new byte[0xE]);
                }
            }
        }
    }
}
