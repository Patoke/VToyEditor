using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace VToyEditor.Modules
{
    public class StartPointObject
    {
        public Vector3 SpawnPosition;
        public float SpawnAngle;
        public int SpawnTeam;
        public int SpawnOrder;
    }

    public class mp_CStartPoint : IGameModule
    {
        public static List<StartPointObject> spawnObjects = new List<StartPointObject>();
        
        public void Run(byte[] data)
        {
            Vector3 spawnPos = Helpers.GetVector3(data, 0);
            float spawnAngle = BitConverter.ToSingle(data, 12);
            int spawnTeam = BitConverter.ToInt32(data, 16);
            int spawnOrder = BitConverter.ToInt32(data, 20);

            Console.WriteLine($"mp_CStartPoint, position: {spawnPos}, angle: {spawnAngle * 180f / MathF.PI}, team: {spawnTeam}, order: {spawnOrder}");

            spawnObjects.Add(new StartPointObject { SpawnPosition = spawnPos, SpawnAngle = spawnAngle, SpawnTeam = spawnTeam, SpawnOrder = spawnOrder });
        }
    }
}
