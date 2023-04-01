using System;
using System.IO;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.MappingsProvider;
using Newtonsoft.Json;
using System.Linq;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Assets.Exports;
using System.Diagnostics;
using CUE4Parse.UE4.Objects.MovieScene;
using CUE4Parse.UE4.Objects.MovieScene.Evaluation;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using static System.Collections.Specialized.BitVector32;
using Newtonsoft.Json.Serialization;
using CUE4Parse.UE4;
using System.Reflection.Metadata.Ecma335;
using System.Collections.Generic;
using CUE4Parse.Utils;
using CUE4Parse.UE4.Wwise;
using System.Threading;
using CUE4Parse.UE4.Readers;
using CUE4Parse_Conversion.Sounds;
using CUE4Parse_Conversion.Sounds.ADPCM;

namespace CUE4Parse.Example
{
    public class AudioExportConfig
    {
        public string gameDirectory { get; set; }
        public string aesKey { get; set; }
        public string objectPath { get; set; }
        public string exportDirectory { get; set; }
    }
    public static class EventBasedAudioExportVal
    {
        private static AudioExportConfig _config;

        private const string ConfigFileName = "AudioExportConfig.json";
        private const string DefaultGameDirectory = @"C:\Riot Games\VALORANT\live\ShooterGame\Content\Paks";
        private const string DefaultAesKey = "0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6";
        private const string DefaultObjectPath = "ShooterGame/Content/WwiseAudio/Events/SFX/UI/Events_UI_InGame/InGame/Play_sfx_UI_MatchVictory.uasset";
        private const string DefaultExportDirectory = @"C:\BKAudioExports";



        public static void Main(string[] args)
        {
            LoadConfig();
            var provider = new DefaultFileProvider(_config.gameDirectory, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_Valorant));
            provider.Initialize();
            provider.SubmitKey(new FGuid(), new FAesKey(_config.aesKey));
            provider.LoadLocalization(ELanguage.English);

            var allExports = provider.LoadObjectExports(_config.objectPath);
            var mediaExportFolder = Path.Combine(_config.exportDirectory, "MediaExports");

            if (!Directory.Exists(mediaExportFolder))
            {
                Directory.CreateDirectory(mediaExportFolder);
            }

            foreach (var export in allExports)
            {
                if (export.Class.Name == "AkAudioEventData") { SaveAudio(export, mediaExportFolder); }
            }

        }

        private static void LoadConfig()
        {
            if (File.Exists(ConfigFileName))
            {
                var configJson = File.ReadAllText(ConfigFileName);
                _config = JsonConvert.DeserializeObject<AudioExportConfig>(configJson);
            }
            else
            {
                _config = new AudioExportConfig
                {
                    gameDirectory = DefaultGameDirectory,
                    aesKey = DefaultAesKey,
                    objectPath = DefaultObjectPath,
                    exportDirectory = DefaultExportDirectory
                };
                SaveConfig();
            }
        }
        private static void SaveConfig()
        {
            var configJson = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(ConfigFileName, configJson);
        }
        private static void SaveAudio(UObject export, string mediaExportFolder)
        {
            var eventName = export.Outer.Name;
            var game_path = export.Owner.Name;
            var mediaList = export.GetOrDefault<UObject[]>("MediaList");
            for (int i = 0; i < mediaList.Length; i++)
            {
                var media = mediaList[i];
                var assetData = media.GetOrDefault<UObject>("CurrentMediaAssetData");
                assetData.Decode(false, out var audioFormat, out var data);

                if (data == null || string.IsNullOrEmpty(audioFormat) || assetData.Owner == null)
                {
                    Console.WriteLine($"Error: Could not decode audio for {eventName}_{i}");
                    continue;
                }

                var mediaFileName = i == 0 ? $"{eventName}.wem" : $"{eventName}_{i}.wem";

                var tt = game_path.Replace("ShooterGame/Content/WwiseAudio/Events", "").TrimStart('/').Replace('/', '\\');
                var mediaFilePath = Path.Combine(mediaExportFolder, tt) + ".wem";

                if (!Directory.Exists(Path.GetDirectoryName(mediaFilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(mediaFilePath));
                }
                using var stream = new FileStream(mediaFilePath, FileMode.Create, FileAccess.Write);
                using var writer = new BinaryWriter(stream);
                writer.Write(data);
                Console.WriteLine($"Saved {mediaFileName} at {mediaExportFolder}!");

                var wavFilePath = mediaFilePath.Replace(".wem", ".wav");
                ConvertAudioToWav(mediaFilePath, wavFilePath);
                //if (File.Exists(mediaFilePath))
                //{
                //File.Delete(mediaFilePath);
                //Console.WriteLine($"Deleted {mediaFileName}");
                //}
            }
        }

        private static void ConvertAudioToWav(string inputFilePath, string outputFilePath)
        {
            var vgmstreamPath = Path.Combine(_config.exportDirectory, "vgmstream-win", "test.exe");

            var vgmProcess = Process.Start(new ProcessStartInfo
            {
                FileName = vgmstreamPath,
                Arguments = $"-o \"{outputFilePath}\" \"{inputFilePath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            vgmProcess?.WaitForExit();
            Console.WriteLine("Audio conversion finished.");
            vgmProcess?.Dispose();
        }
    }
    }
