using System;
using System.IO;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using K4os.Compression.LZ4.Streams;
using K4os.Compression.LZ4;
using CUE4Parse.UE4.Vfs;
using CUE4Parse.UE4.Readers;
using System.Diagnostics;

namespace CUE4Parse.Example
{
    public static class Program
    {
        public enum Endianness
        {
            LittleEndian,
            BigEndian
        }
        //C:\Users\BERNA\OneDrive\Documentos\Release\WindowsNoEditor\BreckieVal\Content\Paks
        private const string _gameDirectory = @"C:\\Riot Games\\VALORANT\\live\\ShooterGame\\Content\\Paks";
        //private const string _gameDirectory = @"C:\\Users\\BERNA\\OneDrive\\Documentos\\Release\\WindowsNoEditor\\BreckieVal\\Content\\Paks"; // Change game directory path to the one you have.
        private const string _aesKey = "0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6";
        private const string _exportDir = @"C:\\ValoDiffManager";
        private const string _backupsDir = @"C:\\ValoDiffManager\\Backups";
        private const string _exportsDir = @"C:\ValoDiffManager\Export";
        private const uint _IS_LZ4 = 0x184D2204u;

        private static int newFiles = 0;
        private static int modifiedFiles = 0;
        private static DefaultFileProvider provider = new DefaultFileProvider(_gameDirectory, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_Valorant));
        //private static DefaultFileProvider provider = new DefaultFileProvider(_gameDirectory, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_UE4_26));

        public static async Task Main(string[] args)
        {
            //provider = new DefaultFileProvider(_gameDirectory, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_Valorant));
            provider.Initialize();
            provider.SubmitKey(new FGuid(), new FAesKey(_aesKey));
            //Console.WriteLine($"FGameLog: {provider.GameName} was loaded successfully");
            var startwatch = new Stopwatch();
            startwatch.Start();
            if (!File.Exists(Path.Combine(_exportDir, "Save.fsav")))
            {
                
                ExportAllFiles();
            }
            var lastBackup = GetLastBackup();
            if (lastBackup != null )
            {
                CompareBackups(lastBackup);
            }

            await CreateBackup(provider);
            startwatch.Stop();
            Console.WriteLine($"It took {startwatch.Elapsed.TotalSeconds} ms to extract everything.");
            return;
        }
        public static async Task CreateBackup(DefaultFileProvider provider)
        {
            var fileName = $"{provider.GameName}_{DateTime.Now:MM'_'dd'_'yyyy}.chksum";
            var fullPath = Path.Combine(_backupsDir, fileName);
            //
            using var fileStream = new FileStream(fullPath, FileMode.Create);
            using var compressedStream = LZ4Stream.Encode(fileStream, LZ4Level.L00_FAST);
            using var writer = new BinaryWriter(compressedStream);
            foreach (var asset in provider.Files.Values)
            {
                if (asset is not VfsEntry entry || entry.Path.EndsWith(".uexp") ||
               entry.Path.EndsWith(".ubulk") || entry.Path.EndsWith(".uptnl"))
                    continue;
                // write
                writer.Write((long)0);
                writer.Write((long)0);
                writer.Write(entry.Size);
                writer.Write(entry.IsEncrypted);
                writer.Write(0);
                writer.Write($"/{entry.Path.ToLower()}");
                writer.Write(0);
                // writers
            }
            SaveCheck(fullPath, fileName, "created", "create");
        }
        public static void SaveCheck(string fullPath, string fileName, string type1, string type2)
        {
            if (new FileInfo(fullPath).Length > 0)
            {
                //Console.WriteLine($"FSaveLog: {fileName} successfully {type1}");
            }
            else
            {
                //Console.WriteLine($"FSaveLog: {fileName} could not be {type1}");
            }
        }

        public static FileInfo GetLastBackup()
        {
            var directoryInfo = new DirectoryInfo(_backupsDir);
            var lastModifiedFile = directoryInfo.GetFiles().OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            if (lastModifiedFile != null)
            {
                //Console.WriteLine($"FBackupLog: The last modified file is: {lastModifiedFile.Name}");
            }
            else
            {
                //Console.WriteLine("FBackupLog: No files found in the directory");
                return null;
            }
            return lastModifiedFile;
        }

        public static uint ReadUInt32(this Stream s, Endianness endian = Endianness.LittleEndian)
        {
            var b1 = s.ReadByte();
            var b2 = s.ReadByte();
            var b3 = s.ReadByte();
            var b4 = s.ReadByte();

            return endian switch
            {
                Endianness.LittleEndian => (uint)(b4 << 24 | b3 << 16 | b2 << 8 | b1),
                Endianness.BigEndian => (uint)(b1 << 24 | b2 << 16 | b3 << 8 | b4),
                _ => throw new Exception("unknown endianness")
            };
        }

        public static void ExportAllFiles()
        {
            foreach (var value in provider.Files.Values)
            {
                ExportFile(value.Path);
            }

            // if first time not
            var bedrop = Path.Combine(_exportDir, "Save.fsav");
            File.WriteAllText(bedrop, "saved");
        }
        public static void ExportFile(string filePath)
        {
            try
            {
                if (!filePath.EndsWith(".uasset") || filePath.Contains("BuiltData") || filePath.Contains("CharacterAbilityStatisticsComponent") || filePath.Contains("BasePing")) { return; }
                var exportFilePath = Path.Combine(_exportsDir, filePath.Replace(".uasset", ".json"));
                var exportDirPath = Path.GetDirectoryName(exportFilePath);
                if (File.Exists(exportFilePath)) 
                {
                    return;
                }
                Console.WriteLine("exported" + filePath);
                var exports = provider.LoadObjectExports(filePath);
                var json = JsonConvert.SerializeObject(exports, Formatting.Indented);

                Directory.CreateDirectory(exportDirPath);
                File.WriteAllText(exportFilePath, json);
                Console.WriteLine("exported = " + filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error exporting file: " + filePath + " - " + ex.Message);
            }
        }

        public static void  CompareBackups(FileInfo backup1)
        {
            using var fileStream = new FileStream(backup1.FullName, FileMode.Open);
            using var memoryStream = new MemoryStream();
            if (fileStream.ReadUInt32() == _IS_LZ4)
            {
                fileStream.Position -= 4;
                using var compressionStream = LZ4Stream.Decode(fileStream);
                compressionStream.CopyTo(memoryStream);
            }
            else fileStream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            using var archive = new FStreamArchive(fileStream.Name, memoryStream);
            var entries = new List<VfsEntry>();

            // all but new

            var paths = new Dictionary<string, int>();
            while (archive.Position < archive.Length)
            {

                archive.Position += 29;
                paths[archive.ReadString().ToLower()[1..]] = 0;
                archive.Position += 4;
            }

            foreach (var (key, value) in provider.Files)
            {
                if (value is not VfsEntry entry || paths.ContainsKey(key) || entry.Path.EndsWith(".uexp") ||
                    entry.Path.EndsWith(".ubulk") || entry.Path.EndsWith(".uptnl")) continue;

                entries.Add(entry);
                newFiles++;
            }
            // modified
            using var arch  = new FStreamArchive(fileStream.Name, memoryStream);
            arch.Position = 0;
            while (arch.Position < arch.Length)
            {

                arch.Position += 16;
                var uncompressedSize = arch.Read<long>();
                var isEncrypted = arch.ReadFlag();
                arch.Position += 4;
                var fullPath = arch.ReadString().ToLower()[1..];
                arch.Position += 4;

                if (fullPath.EndsWith(".uexp") || fullPath.EndsWith(".ubulk") || fullPath.EndsWith(".uptnl") ||
                    !provider.Files.TryGetValue(fullPath, out var asset) || asset is not VfsEntry entry ||
                    entry.Size == uncompressedSize && entry.IsEncrypted == isEncrypted)
                    continue;

                entries.Add(entry);
                modifiedFiles++;
            }
            ExtractNewlyModifiedFiles(entries);
            Console.WriteLine($"FGTrackerLog: CUE4P Game tracker detected: {newFiles} new files // {modifiedFiles} modified files");
        }
        public static void ExtractNewlyModifiedFiles(List<VfsEntry> Entries)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                ExportFile(entry.Path);
                Console.WriteLine($"{entry.Name} got replaced.");
            }
        }


    }
}
