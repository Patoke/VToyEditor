using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace VToyEditor.Modules
{
    public class CarObject
    {
        public string ModelName;
        public Matrix4x4 Transform;
    }

    public class mp_CCar : IGameModule
    {
        public static List<CarObject> carObjects = new List<CarObject>();

        public void Run(byte[] data)
        {
            uint modelNameSize = BitConverter.ToUInt32(data, 0);
            string modelName = Helpers.GetString(data, 4, (int)modelNameSize);

            Matrix4x4 modelTransform = Helpers.GetMatrix(data, 4 + (int)modelNameSize);

            Console.WriteLine($"mp_CCar, model name: {modelName}");

            carObjects.Add(new CarObject { ModelName = modelName, Transform = modelTransform });
        }
    }
}
