using CUE4Parse.UE4.Assets.Readers;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Versions;

namespace CUE4Parse.UE4.Assets.Exports.Component
{
    public class USceneComponent : UActorComponent
    {
        public bool bComputedBoundsOnceForGame;
        public FBoxSphereBounds Bounds;
        
        public override void Deserialize(FAssetArchive Ar, long validPos)
        {
            base.Deserialize(Ar, validPos);
            bComputedBoundsOnceForGame = GetOrDefault<bool>(nameof(bComputedBoundsOnceForGame));

            if (bComputedBoundsOnceForGame && FUE5PrivateFrostyStreamObjectVersion.Get(Ar) >= FUE5PrivateFrostyStreamObjectVersion.Type.SerializeSceneComponentStaticBounds)
            {
                var bIsCooked = Ar.ReadBoolean();

                if (bIsCooked)
                {
                    Bounds = Ar.Read<FBoxSphereBounds>();
                }
            }
        }
    }
}
