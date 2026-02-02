using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace VToyEditor.Modules
{
    public class VTModule
    {
        public string ObjectName; // Name of the object to create (e.g., coche_1, coche_2, StartPointAM_1, etc)
        public string ModuleName; // Module to load (e.g, mp_CCar.dll, mp_flag.dll, etc)
        public byte[] ModuleData; // Data to send to the module into GetNewObjects

        public static T Create<T>(VTModule source) where T : VTModule, new()
        {
            var instance = new T();
            instance.ObjectName = source.ObjectName;
            instance.ModuleName = source.ModuleName;
            instance.ModuleData = source.ModuleData;
            return instance;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(BitConverter.GetBytes(ObjectName.Length + 1));
            w.Write(Encoding.UTF8.GetBytes(ObjectName + '\0'));

            w.Write(BitConverter.GetBytes(ModuleName.Length + 1));
            w.Write(Encoding.UTF8.GetBytes(ModuleName + '\0'));
        }

        public static void CreateNewModuleInstance(Type moduleType, string objectName)
        {
            var listField = moduleType.GetFields(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>));

            if (listField == null) return;

            Type objectType = listField.FieldType.GetGenericArguments()[0];

            VTModule baseData = new VTModule
            {
                ObjectName = objectName,
                ModuleName = moduleType.Name + ".dll",
                ModuleData = Array.Empty<byte>()
            };

            var createMethod = typeof(VTModule).GetMethod("Create").MakeGenericMethod(objectType);
            var newObj = createMethod.Invoke(null, new object[] { baseData });

            InitializeDefaultFields(newObj);

            var list = listField.GetValue(null) as System.Collections.IList;
            list?.Add(newObj);
        }

        private static void InitializeDefaultFields(object obj)
        {
            foreach (var field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(Matrix4x4))
                    field.SetValue(obj, Matrix4x4.Identity);
                else if (field.FieldType == typeof(string))
                    field.SetValue(obj, "");
                else if (field.FieldType == typeof(Vector3))
                    field.SetValue(obj, Vector3.Zero);
                // Ints/Floats default to 0 automatically
            }
        }
    }

    public interface IGameModule
    {
        public static Dictionary<string, Type> Modules = new Dictionary<string, Type>();

        void Run(VTModule data);

        public static void Initialize()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();

            foreach (var type in types)
            {
                if (typeof(IGameModule).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    Modules[type.Name] = type;
                }
            }
        }
    }
}
