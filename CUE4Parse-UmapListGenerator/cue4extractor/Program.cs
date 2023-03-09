using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.Meshes;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse.Utils;
using Newtonsoft.Json;
using Serilog;

namespace cue4extractor
{
    class Program
    {
        /// <param name="PaksDirectory">An option whose argument is parsed as an int</param>
        /// <param name="ppath">An option whose argument is parsed as a bool</param>
        /// <param name="Game">An option whose argument is parsed as a bool</param>
        /// <param name="aesKey">An option whose argument is parsed as a bool</param>
        public static (UWorld,ULevel, List<String>) ExportUmap(string UmapPath,DefaultFileProvider provider)
        {
            UWorld WorldAsset = new UWorld();
            ULevel LevelAsset = new ULevel();
            UObject bforeLast = null;
            List<String> listaProdutos = new List<String>();
            var umapExportFull = provider.LoadObjectExports(UmapPath);
            var lastObject = umapExportFull.Last();
            var MapName = lastObject.Owner.Name.ToString().Substring(lastObject.Owner.Name.ToString().LastIndexOf('/')+1);
            LevelAsset = (ULevel)provider.LoadObject($"{lastObject.Owner.Name}.PersistentLevel");
            WorldAsset = (UWorld)LevelAsset.Outer;
            var AllMapActors = LevelAsset.Actors;
            for (int i = 0; i < AllMapActors.Count(); i++)
            {
                var Actor = AllMapActors[i];
                if (Actor.Name.Contains("BuildingFoundation") || Actor.Name.Contains("LF_Athena_POI"))
                {
                    var NewActor = Actor.Load();
                    foreach (var propsumapexp in NewActor.Properties)
                    {
                        if (propsumapexp.Name.Text == "AdditionalWorlds")
                        {
                            //if (Actor.Name.Contains("LF_Athena_POI"))
                            var worldprops = propsumapexp;
                            UScriptArray props = (UScriptArray)worldprops.Tag.GenericValue;
                            for (int b = 0; b < props.Properties.Count; b++)
                            {
                                SoftObjectProperty proparrayworld = (SoftObjectProperty)props.Properties[b];
                                //var idkppe = provider.LoadObjectExports(proparrayworld.Value());
                                var result = proparrayworld.ToString().Substring(0, proparrayworld.ToString().LastIndexOf('.'));
                                var idkppe = provider.LoadObjectExports(result);
                                listaProdutos.Add(result);
                            break;
                            }
                            break;
                        }
                    }
                }
            }
            return (WorldAsset, LevelAsset, listaProdutos);
        }
        private static void Main(
            string ppath = @"D:\MainFR",
            string PaksDirectory = @"C:\Riot Games\VALORANT\live\ShooterGame\Content\Paks",
            string aesKey = "0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6",
            EGame Game = EGame.GAME_Valorant)
        {
            //// SCRIPT STARTS HERE ////

            Dictionary<string, List<String>> DictUmap = new Dictionary<string, List<string>>();
            var AllUmaps = new List<GameFile>();
            var LocalAllUmaps = new List<string>();
            var versions = new VersionContainer(Game);
            var provider = new DefaultFileProvider(PaksDirectory, SearchOption.AllDirectories, true, versions);
            provider.Initialize();
            provider.SubmitKey(new FGuid(), new FAesKey(aesKey));
            //Console.WriteLine($"DEBUG - CUE4Parse - {ppath}//{PaksDirectory}//{aesKey}//{Game}");
            Console.WriteLine($"INFO - CUE4Parse - Starting to extract all maps of the currently selected game");
            bool bIsFortnite = false;
            var element0array = provider.GameName.ToLower();
            var sWatch = Stopwatch.StartNew();
            if (element0array.Contains("fortnitegame"))
            {
                bIsFortnite = true;
            }
            foreach (var file in provider.Files.Values)
            {
                if (file.Extension == "umap")
                {
                    if (bIsFortnite) 
                    {
                        if (file.Path.Contains("Athena"))
                        {
                            AllUmaps.Add(file);
                            LocalAllUmaps.Add(file.NameWithoutExtension);
                            continue;
                        }
                        continue;
                    }
                    AllUmaps.Add(file);
                    LocalAllUmaps.Add(file.NameWithoutExtension);
                }
            }
            provider.LoadLocalization(ELanguage.English);
            var folder = Path.Combine(ppath, "Uiana/Content/Python/assets");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var converters = new Dictionary<Type, JsonConverter>()
            {
               
            };
            var settings = new JsonSerializerSettings { ContractResolver = new CustomResolver(converters) };
            var umapList = AllUmaps;
            var mapn = umapList.Count();
            Console.WriteLine($"INFO - CUE4Parse - Found {mapn} .umaps starting to parse");
            foreach (var umap in umapList)
            {
                List<String> FullListaProdutos = new List<String>();
                var umapInternalPath = $"{umap}";
                var UmapExport = ExportUmap(umapInternalPath, provider);
                UWorld vWorldAsset = UmapExport.Item1;
                FullListaProdutos = UmapExport.Item3;
                var bIsStreaming = vWorldAsset.StreamingLevels.Length > 0;
                FullListaProdutos.Add(vWorldAsset.Owner.Name.ToString());
                for (int i = 0; i < vWorldAsset.StreamingLevels.Count(); i++)
                {
                    if (!bIsStreaming)
                    {
                        break;
                    }
                    var item = vWorldAsset.StreamingLevels.ElementAt(i);
                    var mapname = item.Owner.Name;//worldasset
                    var export = provider.LoadObject($"{mapname}.{item.Name}");
                    foreach (var varprop in export.Properties)
                    {
                        if (varprop.Name.PlainText == "WorldAsset")
                        {
                            var props = varprop.Tag.GenericValue.ToString();
                            var result = props.Substring(0, props.LastIndexOf('.'));
                            FullListaProdutos.Add(result);
                            break;
                        }
                    }
                }
                if (FullListaProdutos.Count > 0)
                {
                    DictUmap.TryAdd(umap.NameWithoutExtension.ToLower(), FullListaProdutos);
                    var mapname = DictUmap.Keys.Last();
                }
            }
            var umapJASON = JsonConvert.SerializeObject(DictUmap, Formatting.Indented, settings);
            var umapJSONFile = Path.Combine(folder, $"umaps.json");
            File.WriteAllText(umapJSONFile, umapJASON);
            sWatch.Stop();
            var swms = sWatch.ElapsedMilliseconds;
            SoundPlayer simpleSound = new SoundPlayer(@"c:\Windows\Media\chimes.wav");
            simpleSound.Play();
            Console.WriteLine($"INFO - CUE4Parse umap list generation took : {swms}ms");
        }
    }

}
