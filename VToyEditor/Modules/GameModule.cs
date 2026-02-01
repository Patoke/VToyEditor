using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace VToyEditor.Modules
{
    public interface IGameModule
    {
        public static Dictionary<string, Type> Modules = new Dictionary<string, Type>();
        
        void Run(byte[] data);

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
