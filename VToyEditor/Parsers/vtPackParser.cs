using System;
using System.Collections.Generic;
using System.Text;

namespace VToyEditor.Parsers
{
    public class vtPackParser
    {
        public static string DefaultInPackName = "demo.vpk";
        public static string DefaultOutPackName = "export.vpk";
        public static string DefaultOutputFolder = "Out\\";
        public static string DefaultExportFolder = "Export\\";

        public static string ModelsFolder = DefaultOutputFolder + "nfos\\";
        public static string TexturesFolder = DefaultOutputFolder + "texs\\";
        public static string ScenesFolder = DefaultOutputFolder + "scns\\";
        public static string LQSoundsFolder = DefaultOutputFolder + "lqwavs\\";
        public static string HQSoundsFolder = DefaultOutputFolder + "hqwavs\\";
        public static string AnimationsFolder = DefaultOutputFolder + "keys\\";

        enum VpkEntryType
        {
            Unknown = 0,
            Directory = 1,
            File = 2
        }

        private struct PackEntry
        {
            public string FullPath;
            public string FileName;
            public string DirectoryName;
            public uint ParentIndex;
            public uint FileSize;
            public uint FileOffset; // Offset into the file contents table
            public uint TimeStamp;
            public VpkEntryType Type;
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
                throw new NotSupportedException("Only VPK version 1.0.x is supported.");
            }

            var verPatch = reader.ReadUInt32(); // Not checked for by the engine, value is 0
            var unkValue = reader.ReadUInt32();
            var headerSize = reader.ReadUInt32();

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

                var fileTimeStamp = DateTimeOffset.FromUnixTimeSeconds(timeStamp).DateTime;
                var fullOutputPath = Path.Combine(outPath, directory).Trim('\\').Trim('\\');

                if (entryType == (uint)VpkEntryType.Directory)
                {
                    // Some dates appear to be wrong, I don't really care though since this is basically just visual
                    if (!Directory.Exists(fullOutputPath))
                        Directory.CreateDirectory(fullOutputPath);

                    Directory.SetCreationTime(fullOutputPath, fileTimeStamp);
                    Directory.SetLastWriteTime(fullOutputPath, fileTimeStamp);
                    continue;
                }

                if (fileOffset == 0 || fileSize == 0)
                {
                    continue;
                }

                var currentPos = reader.BaseStream.Position;
                reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);

                var fileData = reader.ReadBytes((int)fileSize);

                if (File.Exists(fullOutputPath))
                    fullOutputPath += "_dup";

                if (!Directory.Exists(fullOutputPath))
                    Directory.CreateDirectory(fullOutputPath);

                var filePath = Path.Combine(fullOutputPath, fileName);
                File.WriteAllBytes(filePath, fileData);

                File.SetCreationTime(filePath, fileTimeStamp);
                File.SetLastWriteTime(filePath, fileTimeStamp);

                reader.BaseStream.Seek(currentPos, SeekOrigin.Begin);
            }
        }

        public void Repack(string inPath, string outVpkPath)
        {
            var entries = new List<PackEntry>();
            var directoryToIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Normalize input path to ensure relative paths work
            inPath = Path.GetFullPath(inPath);
            if (!inPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                inPath += Path.DirectorySeparatorChar;

            // Get directories
            var allDirs = Directory.GetDirectories(inPath, "*", SearchOption.AllDirectories);

            // Sort by length ensures parents are added before children, though not strictly required by format, it's safer
            Array.Sort(allDirs);

            foreach (var dirPath in allDirs)
            {
                var relativePath = dirPath.Substring(inPath.Length).Trim(Path.DirectorySeparatorChar);
                if (string.IsNullOrEmpty(relativePath)) continue;

                var relativeParent = Path.GetDirectoryName(relativePath) ?? "";

                var entry = new PackEntry
                {
                    FullPath = dirPath,
                    FileName = Path.GetFileName(dirPath), // Folder name (e.g., "texs")
                    DirectoryName = relativeParent, // Parent path
                    Type = VpkEntryType.Directory,
                    FileSize = 0,
                    FileOffset = 0,
                    TimeStamp = (uint)new DateTimeOffset(Directory.GetCreationTime(dirPath)).ToUnixTimeSeconds()
                };

                // Store index for children to reference
                directoryToIndexMap[relativePath] = entries.Count;
                entries.Add(entry);
            }

            // Get files
            var allFiles = Directory.GetFiles(inPath, "*", SearchOption.AllDirectories);
            Array.Sort(allFiles);

            foreach (var filePath in allFiles)
            {
                var relativePath = filePath.Substring(inPath.Length);
                var relativeDir = Path.GetDirectoryName(relativePath);
                if (relativeDir != null && relativeDir.StartsWith(Path.DirectorySeparatorChar.ToString()))
                    relativeDir = relativeDir.Substring(1); // Remove leading slash

                var entry = new PackEntry
                {
                    FullPath = filePath,
                    FileName = Path.GetFileName(filePath),
                    DirectoryName = relativeDir ?? "",
                    Type = VpkEntryType.File,
                    TimeStamp = (uint)new DateTimeOffset(File.GetLastWriteTime(filePath)).ToUnixTimeSeconds()
                };

                entries.Add(entry);
            }

            // Get parent directory indices
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (string.IsNullOrEmpty(entry.DirectoryName))
                {
                    entry.ParentIndex = 0xFFFFFFFF; // -1
                }
                else
                {
                    if (directoryToIndexMap.TryGetValue(entry.DirectoryName, out int idx))
                    {
                        entry.ParentIndex = (uint)idx;
                    }
                    else
                    {
                        // Fallback, shouldn't happen
                        entry.ParentIndex = 0xFFFFFFFF;
                    }
                }
            }

            using var fs = File.Open(outVpkPath, FileMode.Create);
            using var writer = new BinaryWriter(fs);

            // Header
            writer.Write(Encoding.UTF8.GetBytes("vtPack")); // Magic
            writer.Write((uint)1); // Major
            writer.Write((uint)0); // Minor
            writer.Write((uint)0); // Patch
            writer.Write((uint)0); // Unknown, does this break anything?
            writer.Write((uint)34); // Header size

            // Placeholders for Count and TableOffset
            long countOffsetPosition = writer.BaseStream.Position;
            writer.Write((uint)0);
            writer.Write((uint)0);

            // File content table
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry.Type == VpkEntryType.File)
                {
                    entry.FileOffset = (uint)writer.BaseStream.Position;
                    var bytes = File.ReadAllBytes(entry.FullPath);
                    entry.FileSize = (uint)bytes.Length;
                    writer.Write(bytes);
                }
                else
                {
                    // Directories are essentially empty entries
                    entry.FileOffset = 0;
                    entry.FileSize = 0;
                }
            }

            uint tableStartOffset = (uint)writer.BaseStream.Position;

            foreach (var entry in entries)
            {
                writer.Write(BitConverter.GetBytes(entry.FileName.Length + 1));
                writer.Write(Encoding.UTF8.GetBytes(entry.FileName + '\0'));

                string dirToWrite = entry.DirectoryName;
                if (!string.IsNullOrEmpty(dirToWrite) && !dirToWrite.EndsWith("\\"))
                    dirToWrite += "\\";

                writer.Write(BitConverter.GetBytes(dirToWrite.Length + 1));
                writer.Write(Encoding.UTF8.GetBytes(dirToWrite + '\0'));

                writer.Write(entry.ParentIndex);
                writer.Write(entry.FileSize);
                writer.Write(entry.FileSize); // Compressed Size (same as uncompressed)
                writer.Write(entry.FileOffset);
                writer.Write(entry.TimeStamp);
                writer.Write((uint)entry.Type);
            }

            writer.BaseStream.Seek(countOffsetPosition, SeekOrigin.Begin);
            writer.Write((uint)entries.Count);
            writer.Write(tableStartOffset);
        }
    }
}
