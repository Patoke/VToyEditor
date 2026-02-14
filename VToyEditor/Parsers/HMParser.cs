using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Text;

namespace VToyEditor.Parsers
{
    public class HeightMapSector
    {
        public float BoundsXMin;
        public float BoundsXMax;
        public float BoundsZMin;
        public float BoundsZMax;
        public HeightMapFace[] Faces;
        public HeightMapSector[] Children = new HeightMapSector[4]; // Each height map sector has 4 children sectors (quadtree)
    }

    public class HeightMapFace
    {
        public Vector3 Normals;
        public float Distance;
        public HeightMapContour[] Contours;
    }

    public class HeightMapContour
    {
        public Vector3[] Points;
    }

    public class VTHMParser
    {
        public string HeightMapFile { get; private set; }

        public List<HeightMapSector> HeightMapSectors = new List<HeightMapSector>();

        public void Parse(string path)
        {
            HeightMapFile = path;

            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs);

            var magicBytes = reader.ReadBytes(4);
            string magic = System.Text.Encoding.UTF8.GetString(magicBytes);

            // First game and the online variant both use the same format
            if (magic != "VTHM") throw new Exception($"Unknown Magic: {magic}");

            HeightMapSectors.Add(ReadSector(reader));
        }

        public HeightMapSector ReadSector(BinaryReader r)
        {
            HeightMapSector outSector = new HeightMapSector();

            float boundsXMin = r.ReadSingle();
            float boundsXMax = r.ReadSingle();
            float boundsZMin = r.ReadSingle();
            float boundsZMax = r.ReadSingle();

            outSector.BoundsXMax = boundsXMax;
            outSector.BoundsXMin = boundsXMin;
            outSector.BoundsZMax = boundsZMax;
            outSector.BoundsZMin = boundsZMin;

            uint faceCount = r.ReadUInt32();
            if (faceCount > 0)
            {
                HeightMapFace[] faces = new HeightMapFace[faceCount];
                for (int i = 0; i < faceCount; i++)
                {
                    Vector3 faceNormals = Helpers.ReadVector3(r);
                    float facePlaneDistance = r.ReadSingle();
                    uint contourCount = r.ReadUInt32();

                    HeightMapContour[] contours = new HeightMapContour[contourCount];
                    for (int j = 0; j < contourCount; j++)
                    {
                        uint pointCount = r.ReadUInt32();

                        Vector3[] points = new Vector3[pointCount];
                        for (int x = 0; x < pointCount; x++)
                        {
                            Vector3 point = Helpers.ReadVector3(r);

                            points[x] = point;
                        }

                        contours[j] = new HeightMapContour { Points = points };
                    }

                    faces[i] = new HeightMapFace
                    {
                        Normals = faceNormals,
                        Distance = facePlaneDistance,
                        Contours = contours
                    };
                }

                outSector.Faces = faces;
            }

            uint hasMoreSectors = r.ReadUInt32();
            if (hasMoreSectors != 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    outSector.Children[i] = ReadSector(r);
                }
            }

            return outSector;
        }
    }
}
