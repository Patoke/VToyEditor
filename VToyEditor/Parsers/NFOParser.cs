using System;
using System.Collections.Generic;
using System.Text;

namespace VToyEditor.Parsers
{
    public class VTNFOParser
    {
        public void Parse(string path)
        {
            using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs);

            var magicBytes = reader.ReadBytes(4);
            string magic = System.Text.Encoding.UTF8.GetString(magicBytes);

            // First game and the online variant both use the same format
            if (magic != "VTFF") throw new Exception($"Unknown Magic: {magic}");

            // Todo: finish implementing
            throw new NotImplementedException();
        }
    }
}
