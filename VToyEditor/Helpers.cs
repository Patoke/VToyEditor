using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace VToyEditor
{
    public static class Helpers
    {
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

        public static string GetString(byte[] data, int offset, int count)
        {
            return System.Text.Encoding.UTF8.GetString(data, 4, (int)count).Replace("\0", string.Empty);
        }

        public static Matrix4x4 GetMatrix(byte[] data, int offset)
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

        public static Vector3 GetVector3(byte[] data, int offset)
        {
            return new Vector3(BitConverter.ToSingle(data, offset), BitConverter.ToSingle(data, offset + 4), BitConverter.ToSingle(data, offset + 8));
        }
    }
}
