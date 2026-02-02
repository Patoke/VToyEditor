using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace VToyEditor.Modules
{
    public class StartPointObject : VTModule
    {
        public Vector3 SpawnPosition;
        public float SpawnAngle;
        public int SpawnTeam;
        public int SpawnOrder;
    }

    public class mp_CStartPoint : IGameModule
    {
        public static List<StartPointObject> spawnObjects = new List<StartPointObject>();

        public void Run(VTModule data)
        {
            var reader = new Helpers.ByteReader { data = data.ModuleData };

            Vector3 spawnPos = reader.GetVector3();
            float spawnAngle = reader.GetSingle();
            int spawnTeam = reader.GetInt32();
            int spawnOrder = reader.GetInt32();

            var sp = VTModule.Create<StartPointObject>(data);

            sp.SpawnPosition = spawnPos;
            sp.SpawnAngle = spawnAngle;
            sp.SpawnTeam = spawnTeam;
            sp.SpawnOrder = spawnOrder;

            spawnObjects.Add(sp);

            Console.WriteLine($"mp_CStartPoint, position: {spawnPos}, angle: {spawnAngle * 180f / MathF.PI}, team: {spawnTeam}, order: {spawnOrder}");
        }

        public static void WriteObjects(BinaryWriter writer)
        {
            for (int i = 0; i < spawnObjects.Count; i++)
            {
                var sp = spawnObjects[i];
                sp.Write(writer);

                using (new Helpers.DataSizeWriterScope(writer))
                {
                    writer.Write(BitConverter.GetBytes(sp.SpawnPosition.X));
                    writer.Write(BitConverter.GetBytes(sp.SpawnPosition.Y));
                    writer.Write(BitConverter.GetBytes(sp.SpawnPosition.Z));

                    writer.Write(BitConverter.GetBytes(sp.SpawnAngle));

                    writer.Write(BitConverter.GetBytes(sp.SpawnTeam));

                    writer.Write(BitConverter.GetBytes(sp.SpawnOrder));
                }
            }
        }
    }
}
