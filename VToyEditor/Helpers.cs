using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using VToyEditor.Parsers;

namespace VToyEditor
{
    public static class Helpers
    {
        public static string GetCaseInsensitivePath(string filePath)
        {
            if (filePath == string.Empty)
            {
                return string.Empty;
            }

            string? actualFilePath = Directory.GetFiles(Path.GetDirectoryName(filePath))
                .FirstOrDefault(f => Path.GetFileName(f)
                .Equals(Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase));

            return actualFilePath ?? string.Empty;
        }
        
        public struct DataSizeWriterScope : IDisposable
        {
            private readonly BinaryWriter _writer;
            private readonly long _prefixPosition;
            public readonly long DataStartPosition;

            public DataSizeWriterScope(BinaryWriter writer)
            {
                _writer = writer;
                _prefixPosition = _writer.BaseStream.Position;
                _writer.Write(0);
                DataStartPosition = _writer.BaseStream.Position;
            }

            public void Dispose()
            {
                long endPosition = _writer.BaseStream.Position;
                uint totalSize = (uint)(endPosition - DataStartPosition);

                _writer.BaseStream.Seek(_prefixPosition, SeekOrigin.Begin);
                _writer.Write(totalSize);
                _writer.BaseStream.Seek(endPosition, SeekOrigin.Begin);
            }
        }

        public static Vector3 ExtractEulerAngles(Matrix4x4 matrix)
        {
            float pitch = MathF.Asin(Math.Clamp(-matrix.M23, -1f, 1f));
            float yaw, roll;

            if (MathF.Cos(pitch) > 0.0001)
            {
                yaw = MathF.Atan2(matrix.M13, matrix.M33);
                roll = MathF.Atan2(matrix.M21, matrix.M22);
            }
            else
            {
                yaw = 0;
                roll = MathF.Atan2(-matrix.M12, matrix.M11);
            }
            return new Vector3(pitch, yaw, roll) * (180f / MathF.PI);
        }

        public static string ReadString(BinaryReader r, uint length)
        {
            if (length == 0) return "";
            var bytes = r.ReadBytes((int)length);
            return System.Text.Encoding.UTF8.GetString(bytes).Replace("\0", string.Empty);
        }

        public static Vector3 ReadVector3(BinaryReader r)
        {
            return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        public static Matrix4x4 ReadMatrix(BinaryReader r)
        {
            float[] m = new float[16];
            for (int i = 0; i < 16; i++) m[i] = r.ReadSingle();

            return new Matrix4x4(
                m[0], m[1], m[2], m[3],
                m[4], m[5], m[6], m[7],
                m[8], m[9], m[10], m[11],
                m[12], m[13], m[14], m[15]
            );
        }

        public static byte[] MatrixAsBytes(Matrix4x4 matrix)
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes(matrix.M11));
            bytes.AddRange(BitConverter.GetBytes(matrix.M12));
            bytes.AddRange(BitConverter.GetBytes(matrix.M13));
            bytes.AddRange(BitConverter.GetBytes(matrix.M14));
            bytes.AddRange(BitConverter.GetBytes(matrix.M21));
            bytes.AddRange(BitConverter.GetBytes(matrix.M22));
            bytes.AddRange(BitConverter.GetBytes(matrix.M23));
            bytes.AddRange(BitConverter.GetBytes(matrix.M24));
            bytes.AddRange(BitConverter.GetBytes(matrix.M31));
            bytes.AddRange(BitConverter.GetBytes(matrix.M32));
            bytes.AddRange(BitConverter.GetBytes(matrix.M33));
            bytes.AddRange(BitConverter.GetBytes(matrix.M34));
            bytes.AddRange(BitConverter.GetBytes(matrix.M41));
            bytes.AddRange(BitConverter.GetBytes(matrix.M42));
            bytes.AddRange(BitConverter.GetBytes(matrix.M43));
            bytes.AddRange(BitConverter.GetBytes(matrix.M44));
            return bytes.ToArray();
        }

        public class ByteReader
        {
            public int offset = 0;
            public byte[] data;

            public string GetString(int count)
            {
                string outStr = System.Text.Encoding.UTF8.GetString(data, offset, (int)count).Replace("\0", string.Empty);
                offset += count;
                return outStr;
            }

            public Matrix4x4 GetMatrix()
            {
                float[] m = new float[16];
                for (int i = 0; i < 16; i++)
                {
                    m[i] = BitConverter.ToSingle(data, offset);
                    offset += 4;
                }

                return new Matrix4x4(
                    m[0], m[1], m[2], m[3],
                    m[4], m[5], m[6], m[7],
                    m[8], m[9], m[10], m[11],
                    m[12], m[13], m[14], m[15]
                );
            }

            public Vector3 GetVector3()
            {
                Vector3 outVector = new Vector3(BitConverter.ToSingle(data, offset), BitConverter.ToSingle(data, offset + 4), BitConverter.ToSingle(data, offset + 8));
                offset += 12;
                return outVector;
            }

            public float GetSingle()
            {
                float outSingle = BitConverter.ToSingle(data, offset);
                offset += 4;
                return outSingle;
            }

            public uint GetUInt32()
            {
                uint outInt = BitConverter.ToUInt32(data, offset);
                offset += 4;
                return outInt;
            }

            public int GetInt32()
            {
                int outInt = BitConverter.ToInt32(data, offset);
                offset += 4;
                return outInt;
            }
        }
    }
}
