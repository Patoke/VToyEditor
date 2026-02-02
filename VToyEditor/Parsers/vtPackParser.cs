using System;
using System.Collections.Generic;
using System.Text;

namespace VToyEditor.Parsers
{
    public class vtPackParser
    {
        enum VpkEntryType
        {
            Unknown = 0,
            Directory = 1,
            File = 2
        }

        public void Unpack(string vpkPath, string outPath)
        {
            using var fs = File.OpenRead(vpkPath);
            using var reader = new BinaryReader(fs);

            var magicBytes = reader.ReadBytes(6);
            string magic = System.Text.Encoding.UTF8.GetString(magicBytes);

            if (magic != "vtPack") throw new Exception($"Unknown Magic: {magic}");

            var verMajor = reader.ReadUInt32();
            var verMinor = reader.ReadUInt32();
            if (verMajor != 1 || verMinor > 0)
            {
                throw new NotSupportedException("Only VPK version 1.0 is supported.");
            }

            // Skip 12 bytes (unknown/reserved)
            _ = reader.ReadBytes(4 * 3);

            uint numberOfFiles = reader.ReadUInt32();
            uint fileTableOffset = reader.ReadUInt32();

            reader.BaseStream.Seek(fileTableOffset, SeekOrigin.Begin);
            for (uint i = 0; i < numberOfFiles; i++)
            {
                var fileNameSize = reader.ReadUInt32();
                var fileName = Helpers.ReadString(reader, fileNameSize);

                var directoryNameSize = reader.ReadUInt32();
                var directory = Helpers.ReadString(reader, directoryNameSize);

                var parentIndex = reader.ReadUInt32();
                var fileSize = reader.ReadUInt32();
                var fileSizeCompressed = reader.ReadUInt32();
                var fileOffset = reader.ReadUInt32();
                var timeStamp = reader.ReadUInt32();
                var entryType = reader.ReadUInt32();

                if (entryType == (uint)VpkEntryType.Directory)
                {
                    Console.WriteLine($"Skipping directory entry: {fileName}");
                    continue;
                }

                if (fileOffset == 0 || fileSize == 0)
                {
                    Console.WriteLine($"Skipping empty file: {fileName}");
                    continue;
                }

                Console.WriteLine($"Extracting: {directory}/{fileName} (fileOffset: {fileOffset}, fileSize: {fileSize})...");

                var currentPos = reader.BaseStream.Position;
                reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);

                var fileData = reader.ReadBytes((int)fileSize);
                var fullOutputPath = Path.Combine(outPath, directory).Trim('\\').Trim('\\');

                if (File.Exists(fullOutputPath))
                    fullOutputPath += "_dup";

                if (!Directory.Exists(fullOutputPath))
                    Directory.CreateDirectory(fullOutputPath);

                File.WriteAllBytes(Path.Combine(fullOutputPath, fileName), fileData);

                reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
            }
        }

        public void Repack(string inPath, string outVpkPath)
        {
            throw new NotImplementedException();
        }
    }
}
