using Silk.NET.OpenGL;

namespace VToyEditor
{
    public class Texture : IDisposable
    {
        public uint Handle;
        public string Name;
        public bool IsTransparent;
        private GL _gl;

        public unsafe Texture(GL gl, string filepath)
        {
            _gl = gl;
            Name = Path.GetFileName(filepath);

            using var fs = File.OpenRead(filepath);
            using var reader = new BinaryReader(fs);

            var magic = reader.ReadBytes(4);
            string magicStr = System.Text.Encoding.ASCII.GetString(magic);
            if (magicStr != "TeX2") throw new Exception($"Invalid texture magic: {magicStr}");

            byte formatFlag = reader.ReadByte(); // 0 = DXT1, 1 = DXT5
            reader.ReadInt32(); // Unknown int
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            int dataSize = reader.ReadInt32();

            Console.WriteLine($"Loading Texture {Name}: {width}x{height} ({(formatFlag == 1 ? "DXT5" : "DXT1")})");

            byte[] compressedData = reader.ReadBytes(dataSize);

            Handle = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, Handle);

            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);

            InternalFormat format = (formatFlag == 1)
                ? InternalFormat.CompressedRgbaS3TCDxt5Ext
                : InternalFormat.CompressedRgbaS3TCDxt1Ext;

            // DXT5 has transparency, DXT1 is opaque (1 bit alpha channel)
            IsTransparent = (format == InternalFormat.CompressedRgbaS3TCDxt5Ext);

            fixed (byte* pData = compressedData)
            {
                _gl.CompressedTexImage2D(TextureTarget.Texture2D, 0, format, (uint)width, (uint)height, 0, (uint)dataSize, pData);
            }

            _gl.GenerateMipmap(TextureTarget.Texture2D);
        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            _gl.ActiveTexture(unit);
            _gl.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Dispose()
        {
            _gl.DeleteTexture(Handle);
        }
    }

}
