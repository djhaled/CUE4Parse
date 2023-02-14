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

namespace CUE4Parse.Example
{
    public static class WidgetAnimConverter
    {
        private const string _gameDirectory = @"C:\Riot Games\VALORANT\live\ShooterGame\Content\Paks";
        private const string _aesKey = "0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6";
        private const string _objectPath = "ShooterGame/Content/UI/Screens/OutOfGame/MainMenu/Party/QueueSelector";

        // Rick has 2 exports as of today
        //      - CID_A_112_Athena_Commando_M_Ruckus
        //      - FortCosmeticCharacterPartVariant_0
        //
        // this example will show you how to get them all or just one of them


        public static void Main(string[] args)
        {
            var provider = new DefaultFileProvider(_gameDirectory, SearchOption.TopDirectoryOnly, true, new VersionContainer(EGame.GAME_Valorant));

            provider.Initialize(); // will scan local files and read them to know what it has to deal with (PAK/UTOC/UCAS/UASSET/UMAP)
            provider.SubmitKey(new FGuid(), new FAesKey(_aesKey)); // decrypt basic info (1 guid - 1 key)

            provider.LoadLocalization(ELanguage.English); // explicit enough

            // these 2 lines will load all exports the asset has and transform them in a single Json string
            var allExports = provider.LoadObjectExports(_objectPath);
            var fullJson = JsonConvert.SerializeObject(allExports, Formatting.Indented);
        }
    }
}
