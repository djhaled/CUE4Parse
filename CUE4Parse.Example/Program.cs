using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Animations.PSA;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using System.Collections.Generic;
using System.IO;
using System;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Exports;
using System.Runtime.Serialization;

public class Progam
{
    public static UAnimationAsset LocalUVDensities;
    public struct FAsyncLoadedEquippableGunAnim
    {
        public UAnimationAsset LoadedDefaultAsset;
        public UAnimationAsset LoadedAltModeAsset;
    }
    public static UAnimSequence[] allAnims;
    static bool ExportAnimation(UAnimSequence additiveAnimSequence)
    {
        var additiveSkeleton = additiveAnimSequence.Skeleton.Load<USkeleton>();

        var reference = additiveAnimSequence.RefPoseSeq?.Load<UAnimSequence>();
        if (reference == null)
        {
            ExportNormalAnim(additiveAnimSequence);
            return true;
        }
        var referenceSkeleton = reference.Skeleton.Load<USkeleton>();

        var additiveAnimSet = additiveSkeleton.ConvertAnims(additiveAnimSequence);
        var referenceAnimSet = referenceSkeleton.ConvertAnims(reference);
        var animSeq = additiveAnimSet.Sequences[0];

        var additivePoses = FAnimationRuntime.LoadAsPoses(additiveAnimSet);

        animSeq.OriginalSequence = referenceAnimSet.Sequences[0].OriginalSequence;
        animSeq.Tracks = new List<CAnimTrack>(additivePoses[0].Bones.Length);
        for (int i = 0; i < additivePoses[0].Bones.Length; i++)
        {
            animSeq.Tracks.Add(new CAnimTrack(additivePoses.Length));
        }

        FCompactPose[] referencePoses;
        switch (additiveAnimSequence.RefPoseType)
        {
            //Use the Skeleton's ref pose as base
            case EAdditiveBasePoseType.ABPT_RefPose:
                referencePoses = FAnimationRuntime.LoadRestAsPoses(additiveAnimSet);
                break;
            //Use a whole animation as a base pose
            case EAdditiveBasePoseType.ABPT_AnimScaled:
                referencePoses = FAnimationRuntime.LoadAsPoses(referenceAnimSet);
                break;
            //Use one frame of an animation as a base pose
            case EAdditiveBasePoseType.ABPT_AnimFrame:
                referencePoses = FAnimationRuntime.LoadAsPoses(referenceAnimSet, additiveAnimSequence.RefFrameIndex);
                break;
            //Use one frame of this animation
            case EAdditiveBasePoseType.ABPT_LocalAnimFrame:
                referencePoses = FAnimationRuntime.LoadAsPoses(additiveAnimSet, additiveAnimSequence.RefFrameIndex);
                break;
            default:
                referencePoses = FAnimationRuntime.LoadAsPoses(referenceAnimSet);
                break;
        }

        //loop trough each Pose/Frame and add the output to the empty tracks
        for (var index = 0; index < additivePoses.Length; index++)
        {

            var addPose = additivePoses[index];
            var refPose = (FCompactPose)referencePoses[additiveAnimSequence.RefFrameIndex].Clone();
            //var refPose = referencePoses[index];
            switch (additiveAnimSequence.AdditiveAnimType)
            {
                case EAdditiveAnimationType.AAT_LocalSpaceBase:
                    FAnimationRuntime.AccumulateLocalSpaceAdditivePoseInternal(refPose, addPose, 1);
                    break;
                case EAdditiveAnimationType.AAT_RotationOffsetMeshSpace:
                    FAnimationRuntime.AccumulateMeshSpaceRotationAdditiveToLocalPoseInternal(refPose, addPose, 1);
                    break;
            }

            refPose.AddToTracks(animSeq.Tracks, index);
        }

        additiveAnimSet.Sequences.Clear();
        additiveAnimSet.Sequences.Add(animSeq);
        var exporterOptions = new ExporterOptions();
        var exporter = new AnimExporter(additiveAnimSequence, exporterOptions);
        //export PSA
        exporter.DoExportPsa(additiveAnimSet, 0);
        Console.WriteLine($"Exported {additiveAnimSequence.Name} anim");
        return exporter.TryWriteToDir(new DirectoryInfo(@"E:\BKImport"), out var label, out var fileName); ;
    }
    static bool ExportNormalAnim(UAnimSequence car)
    {
        var exporterOptions = new ExporterOptions();
        var toSave = new Exporter(car, exporterOptions);
        Console.WriteLine($"Exported {car.Name} anim");
        bool isExported = toSave.TryWriteToDir(new DirectoryInfo(@"E:\BKImport"), out var label, out var fileName);
        return isExported; 
    }
    static void Main(string[] args)
    {
        var provider = new DefaultFileProvider(@"E:\ValContentEvent\ShooterGame\Content\Paks", SearchOption.AllDirectories, true, new VersionContainer(EGame.GAME_Valorant));

        provider.Initialize();
        provider.SubmitKey(new FGuid(0), new FAesKey("0x4BE71AF2459CF83899EC9DC2CB60E22AC4B3047E0211034BBABE9D174C069DD6"));
        List<UAnimSequence> animsList = new List<UAnimSequence>();
        //load UAnimSeq from Object
        //regular anim: Game/Characters/_Core/3P/Anims/Rifle/Core_Rifle_Idle.Core_Rifle_Idle
        //local space: Game/Characters/_Core/3P/Anims/TP_Core_SprintAddN_UB.TP_Core_SprintAddN_UB
        //mesh space: Game/Equippables/Guns/SniperRifles/Boltsniper/S0/1P/Anims/FP_Core_Boltsniper_S0_Fire.FP_Core_Boltsniper_S0_Fire
        var additiveAnimSequence = provider.LoadObject<UAnimSequence>("Game/Equippables/Guns/SniperRifles/Boltsniper/S0/1P/Anims/FP_Core_Boltsniper_S0_Fire.FP_Core_Boltsniper_S0_Fire");
        var gObj = provider.LoadObject("Game/Equippables/Guns/SubMachineGuns/Vector/Vector.Vector_C") as UBlueprintGeneratedClass;
        var GunObj = gObj.ClassDefaultObject.Load();
        Console.WriteLine("///");
        // Normal Movement Anims
        var idek = GunObj.GetOrDefault<UScriptMap>("CharacterAnims1P");
        foreach (var item in idek.Properties)
        {
            var brand = item.Value;
            var mouth = (SoftObjectProperty)brand;
            var ide = mouth.Value.Load();
            if (ide is UAnimationAsset)
            {
                Console.WriteLine("///");
                Console.WriteLine($"Should export {ide.Name}");
                animsList.Add((UAnimSequence)ide);
                //ExportAnimation((UAnimSequence)ide);
            }
        }
        // Ready Component
        //var ReadyComp = GunObj.GetOrDefault<>

    }
}
