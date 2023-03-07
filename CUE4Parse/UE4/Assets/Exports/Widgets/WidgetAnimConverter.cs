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

namespace CUE4Parse.UE4.Assets.Exports.Widget
{
    public static class WidgetAnimConverter
    {
        public struct FAnimSect : IUStruct
        {
            public float TimelineTime;
            //public int FrameTime;
            public float Value;
        }
        public struct FAnimSection : IUStruct
        {
            public string ProperName;
            public FAnimSect[] Keys;
        }
        public struct FAnimTrackData : IUStruct
        {
            public string PropertyName;
            public FAnimSection[] Sections;

        }
        public struct FAnimBindingData : IUStruct
        {
            public string BindingName;
            public FAnimTrackData[] Tracks;

        }
        public struct FWidgetAnimData : IUStruct
        {
            public string AnimName;
            public FAnimBindingData[] Bindings;
        }

        private const string _gameDirectory = @"C:\Riot Games\VALORANT\live\ShooterGame\Content\Paks";
        private const string _aesKey = "0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6";
        private const string _objectPath = "ShooterGame/Content/UI/Screens/OutOfGame/MainMenu/Party/QueueSelector";

        // Rick has 2 exports as of today
        //      - CID_A_112_Athena_Commando_M_Ruckus
        //      - FortCosmeticCharacterPartVariant_0
        //
        // this example will show you how to get them all or just one of them

        public static FWidgetAnimData DeserializeAnim(UObject Object)
        {
            var MovieScene = Object.GetOrDefault<UObject>("MovieScene");
            var AnimationBindings = MovieScene.GetOrDefault<FStructFallback[]>("ObjectBindings");
            FAnimBindingData[] ArraySec = new FAnimBindingData[AnimationBindings.Count()];
            for (int an = 0; an < AnimationBindings.Count(); an++)
            {
                var bind = DeserializeAnimBinding(AnimationBindings[an]);
                ArraySec[an] = bind;
            }
            FWidgetAnimData local = new FWidgetAnimData();
            local.AnimName = Object.Name.Replace("_INST", "");
            local.Bindings = ArraySec;
            return local;
        }
        public static FAnimBindingData DeserializeAnimBinding(FStructFallback AnimBinding)
        {
            var BindingName = AnimBinding.GetOrDefault<string>("BindingName");
            var AnimTracks = AnimBinding.GetOrDefault<UObject[]>("Tracks");
            FAnimTrackData[] ArraySec = new FAnimTrackData[AnimTracks.Count()];
            for (int track = 0; track < AnimTracks.Count(); track++)
            {
                var track_z = DeserializeAnimTrack(AnimTracks[track]);
                ArraySec[track] = track_z;
            }
            FAnimBindingData local = new FAnimBindingData();
            local.BindingName = AnimBinding.GetOrDefault<string>("BindingName");
            local.Tracks = ArraySec;
            return local;
        }
        public static FAnimTrackData DeserializeAnimTrack(UObject AnimTrack)
        {
            var PropertyBinding = AnimTrack.GetOrDefault<FStructFallback>("PropertyBinding");
            var Sections = AnimTrack.GetOrDefault<UObject[]>("Sections")[0]; // movie scene float channel

            List<FAnimSection> animSections = new List<FAnimSection>();

            for (int idx = 0; idx < Sections.Properties.Count(); idx++)
            {
                var section = Sections.Properties[idx];
                UScriptStruct scriptStructSection = (UScriptStruct)section.Tag.GenericValue;
                if (section.TagData.StructType != "MovieSceneFloatChannel")
                {
                    //Console.Error.WriteLine($"Error: Section type: {section.TagData.StructType} is not 'MovieSceneFloatChannel");
                    continue;
                }
                var dese = DeserializeAnimSection((FMovieSceneChannel<float>)scriptStructSection.StructType, section.Name.Text);
                animSections.Add(dese);
            }

            FAnimTrackData local = new FAnimTrackData();
            local.PropertyName = PropertyBinding.GetOrDefault<FName>("PropertyName").Text;
            local.Sections = animSections.ToArray();
            return local;
        }
        public static FAnimSection DeserializeAnimSection(FMovieSceneChannel<float> AnimSceneChannel, string PropName)
        {
            FAnimSect[] ArraySec = new FAnimSect[AnimSceneChannel.Values.Count()];
            for (int sec = 0; sec < AnimSceneChannel.Values.Count(); sec++)
            {
                FAnimSect fAnimSect = new FAnimSect();
                //fAnimSect.FrameTime = AnimSceneChannel.Times[sec].Value;
                fAnimSect.TimelineTime = AnimSceneChannel.Times[sec].ToUnrealValue();
                fAnimSect.Value = AnimSceneChannel.Values[sec].Value; ;
                ArraySec[sec] = fAnimSect;
            }
            FAnimSection local = new FAnimSection();
            local.ProperName = PropName;
            local.Keys = ArraySec;
            return local;
        }
        public static void Main(string[] args)
        {
            var provider = new DefaultFileProvider(_gameDirectory, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_Valorant));

            provider.Initialize(); // will scan local files and read them to know what it has to deal with (PAK/UTOC/UCAS/UASSET/UMAP)
            provider.SubmitKey(new FGuid(), new FAesKey(_aesKey)); // decrypt basic info (1 guid - 1 key)

            provider.LoadLocalization(ELanguage.English); // explicit enough

            // these 2 lines will load all exports the asset has and transform them in a single Json string
            var allExports = provider.LoadObjectExports(_objectPath);
            var fullJson = JsonConvert.SerializeObject(allExports, Formatting.Indented);
            ///
            ///
            for (int i = 0; i < allExports.Count(); i++)
            {
                var Export = allExports.ElementAt(i);
                if (Export is UWidgetBlueprintGeneratedClass)
                {
                    Export = (UWidgetBlueprintGeneratedClass)Export;
                    var allAnimations = Export.GetOrDefault<UObject[]>("Animations");
                    FWidgetAnimData[] DeserializedAnimations = new FWidgetAnimData[allAnimations.Count()];
                    for (int iz = 0; iz < allAnimations.Count(); iz++)
                    {
                        var Anim = DeserializeAnim(allAnimations[iz]);
                        DeserializedAnimations[iz] = Anim;
                    }
                    var fullAnimJson = JsonConvert.SerializeObject(DeserializedAnimations, Formatting.Indented);
                    var lastSlash = _objectPath.LastIndexOf("/") + 1;
                    var FileUsed = _objectPath.Substring(lastSlash, _objectPath.Length - lastSlash);
                    var fileName = $"C:\\Users\\BERNA\\Desktop\\DecookedWidgetAnims\\{FileUsed}_ANIMATIONS.json";
                    File.WriteAllText(fileName, fullAnimJson);
                    break;
                }
            }
        }
    }
}
