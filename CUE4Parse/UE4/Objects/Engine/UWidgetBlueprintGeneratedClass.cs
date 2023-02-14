using Newtonsoft.Json;
using System.Linq;
using CUE4Parse.UE4.Objects.MovieScene;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Assets.Exports.Widget;
using System.Collections.Generic;
using CUE4Parse.UE4.Assets.Readers;

namespace CUE4Parse.UE4.Objects.Engine
{
    public class UWidgetBlueprintGeneratedClass : UBlueprintGeneratedClass
    {
        public FPackageIndex WidgetTree { get; private set; }

        public FPackageIndex[] Extensions { get; private set; }
        public FPackageIndex[] FieldNotifyNamesBeka { get; private set; }
        public int FieldNotifyStartBitNumber { get; private set; }
        public uint bClassRequiresNativeTick { get; private set; }

        public FPackageIndex[] Bindings { get; private set; }

        public FPackageIndex[] Animations { get; private set; }
        public FName[] NamedSlots { get; private set; }
        public FName[] AvailableNamedSlots { get; private set; }
        public FName[] InstanceNamedSlots { get; private set; }

        // normal stuff
        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);
            WidgetTree = new FPackageIndex(Ar);
            Extensions = Ar.ReadArray(() => new FPackageIndex(Ar));
            FieldNotifyNamesBeka = Ar.ReadArray(() => new FPackageIndex(Ar));
            FieldNotifyStartBitNumber = 0;
            bClassRequiresNativeTick = 0;
            Bindings = Ar.ReadArray(() => new FPackageIndex(Ar));
            Animations = Ar.ReadArray(() => new FPackageIndex(Ar));
            NamedSlots = Ar.ReadArray(() => Ar.ReadFName());
            AvailableNamedSlots = Ar.ReadArray(() => Ar.ReadFName());
            InstanceNamedSlots = Ar.ReadArray(() => Ar.ReadFName());
        }
        protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
        {
            base.WriteJson(writer, serializer);
            writer.WritePropertyName("AnimationsV2");
            foreach (var anim in Animations)
            {
                WidgetAnimConverter.DeserializeAnim(anim.Load());
            }
            serializer.Serialize(writer, Animations);
        }
    }

}