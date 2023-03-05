using System;
using System.Collections.Generic;
using System.Diagnostics;
using CUE4Parse_Conversion.Animations;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Math;

namespace AnimationRuntime;

public class Pose : ICloneable
{
    public Bone[] Bones;
    public int AnimFrame;
    public bool Processed;

    public object Clone()
    {
        return new Pose
        {
            Bones = this.Bones,
            AnimFrame = this.AnimFrame,
            Processed = this.Processed
        };
    }

    public Pose()
    {

    }

    public Pose(Pose pose)
    {
        Bones = new Bone[pose.Bones.Length];
        for (int boneIndex = 0; boneIndex < Bones.Length; boneIndex++)
        {
            Bones[boneIndex] = (Bone)pose.Bones[boneIndex].Clone();
        }
        AnimFrame = pose.AnimFrame;
    }

    public Pose(FReferenceSkeleton refSkel)
    {
        this.Bones = new Bone[refSkel.FinalRefBoneInfo.Length];
    }

    public void NormalizeRotations()
    {
        foreach (Bone bone in this.Bones)
            bone.Transform.Rotation.Normalize();
    }

    public void AddToTracks(List<CAnimTrack> tracks)
    {
        Debug.Assert(tracks.Count == this.Bones.Length);

        for (int index = 0; index < this.Bones.Length; ++index)
        {
            if (!this.Bones[index].IsValidKey) continue;

            FTransform transform = this.Bones[index].Transform;
            CAnimTrack track = tracks[index];

            List<FQuat> fquatList = new List<FQuat>();
            fquatList.AddRange((IEnumerable<FQuat>) track.KeyQuat);
            fquatList.Add(transform.Rotation);
            List<FVector> fvectorList1 = new List<FVector>();
            fvectorList1.AddRange((IEnumerable<FVector>) track.KeyPos);
            fvectorList1.Add(transform.Translation);
            List<FVector> fvectorList2 = new List<FVector>();
            fvectorList2.AddRange((IEnumerable<FVector>) track.KeyScale);
            fvectorList2.Add(transform.Scale3D);

            tracks[index].KeyPos = fvectorList1.ToArray();
            tracks[index].KeyQuat = fquatList.ToArray();
            tracks[index].KeyScale = fvectorList2.ToArray();
        }
    }
}

public struct Bone : ICloneable
{
    public FTransform Transform;
    public int ParentIndex;
    public string Name;
    public bool IsValidKey;
    public bool Accumulated;

    public Bone()
    {
        this.Transform = FTransform.Identity;
        this.ParentIndex = -1;
        this.Name = "";
        IsValidKey = false;
        Accumulated = false;
    }

    public void AccumulateWithAdditiveScale(FTransform atom, float weight)
    {
        var DELTA = 0.00001;
        Debug.Assert(!Accumulated);
        var blendedRotation = atom.Rotation * weight;

        var SquareRotationW = blendedRotation.W * blendedRotation.W;

        if (SquareRotationW < 1 - DELTA * DELTA)
        {
            this.Transform.Rotation = UnrealMathSSE.VectorQuaternionMultiply2(blendedRotation, this.Transform.Rotation);
        }
        var translationResult = this.Transform.Translation + (atom.Translation * weight);
        var scale3DResult = this.Transform.Scale3D * (atom.Scale3D * weight) + FVector.OneVector;

        this.Transform.Translation = translationResult;
        this.Transform.Scale3D = scale3DResult;
        Accumulated = true;
    }

    public object Clone()
    {
        return new Bone
        {
            Transform = this.Transform,
            ParentIndex = this.ParentIndex,
            Name = this.Name,
            IsValidKey = this.IsValidKey,
            Accumulated = false
        };
    }
}

public static class FAnimationRuntime
{
    public static Pose[] LoadAsPoses(CAnimSet anim, USkeleton skeleton)
    {
        var seq = anim.Sequences[0];
        var poses = new Pose[seq.NumFrames];
        for (int frameIndex = 0; frameIndex < seq.NumFrames; frameIndex++)
        {
            poses[frameIndex] = new Pose(skeleton.ReferenceSkeleton) { AnimFrame = frameIndex };
            for (var boneIndex = 0; boneIndex < poses[frameIndex].Bones.Length; boneIndex++)
            {
                var boneInfo = skeleton.ReferenceSkeleton.FinalRefBoneInfo[boneIndex];
                var track = seq.Tracks[boneIndex];

                var boneOrientation = FQuat.Identity;
                var bonePosition = FVector.ZeroVector;
                var boneScale = FVector.OneVector;
                if (seq.bAdditive)
                {
                    boneScale = FVector.ZeroVector;
                }
                track.GetBonePosition(frameIndex, seq.NumFrames, false, ref bonePosition, ref boneOrientation, ref boneScale);

               poses[frameIndex].Bones[boneIndex] = new Bone
                {
                    Name = boneInfo.Name.ToString(),
                    ParentIndex = boneInfo.ParentIndex,
                    Transform = new FTransform(boneOrientation, bonePosition, boneScale),
                    IsValidKey = frameIndex <= Math.Min(track.KeyPos.Length, track.KeyQuat.Length)
                };
            }
        }
        return poses;
    }

    public static Pose[] LoadAsPoses(CAnimSet anim, USkeleton skeleton, int numFrames, int refFrame)
    {
        var seq = anim.Sequences[0];
        var poses = new Pose[numFrames];
        for (int frameIndex = 0; frameIndex < poses.Length; frameIndex++)
        {
            poses[frameIndex] = new Pose(skeleton.ReferenceSkeleton) { AnimFrame = frameIndex };
            for (var boneIndex = 0; boneIndex < poses[frameIndex].Bones.Length; boneIndex++)
            {
                var boneInfo = skeleton.ReferenceSkeleton.FinalRefBoneInfo[boneIndex];
                var originalTransform = skeleton.ReferenceSkeleton.FinalRefBonePose[boneIndex];
                var track = seq.Tracks[boneIndex];

                var boneOrientation = FQuat.Identity;
                var bonePosition = FVector.ZeroVector;
                var boneScale = FVector.OneVector;
                if (seq.bAdditive)
                {
                    boneScale = FVector.ZeroVector;
                }
                track.GetBonePosition(refFrame, seq.NumFrames, false, ref bonePosition, ref boneOrientation, ref boneScale);

                switch (anim.BoneModes[boneIndex])
                {
                    case EBoneTranslationRetargetingMode.Skeleton:
                    {
                        var targetTransform = seq.RetargetBasePose?[boneIndex] ?? anim.BonePositions[boneIndex];
                        bonePosition = targetTransform.Translation;
                        break;
                    }
                    case EBoneTranslationRetargetingMode.AnimationScaled:
                    {
                        var sourceTranslationLength = originalTransform.Translation.Size();
                        if (sourceTranslationLength > UnrealMath.KindaSmallNumber)
                        {
                            var targetTranslationLength = seq.RetargetBasePose?[boneIndex].Translation.Size() ?? anim.BonePositions[boneIndex].Translation.Size();
                            bonePosition.Scale(targetTranslationLength / sourceTranslationLength);
                        }
                        break;
                    }
                    case EBoneTranslationRetargetingMode.AnimationRelative:
                    {
                        // can't tell if it's working or not
                        var sourceSkelTrans = originalTransform.Translation;
                        var refPoseTransform  = seq.RetargetBasePose?[boneIndex] ?? anim.BonePositions[boneIndex];

                        boneOrientation = boneOrientation * FQuat.Conjugate(originalTransform.Rotation) * refPoseTransform.Rotation;
                        bonePosition += refPoseTransform.Translation - sourceSkelTrans;
                        boneScale *= refPoseTransform.Scale3D * originalTransform.Scale3D;
                        boneOrientation.Normalize();
                        break;
                    }
                    case EBoneTranslationRetargetingMode.OrientAndScale:
                    {
                        var sourceSkelTrans = originalTransform.Translation;
                        var targetSkelTrans = seq.RetargetBasePose?[boneIndex].Translation ?? anim.BonePositions[boneIndex].Translation;

                        if (!sourceSkelTrans.Equals(targetSkelTrans))
                        {
                            var sourceSkelTransLength = sourceSkelTrans.Size();
                            var targetSkelTransLength = targetSkelTrans.Size();
                            if (!UnrealMath.IsNearlyZero(sourceSkelTransLength * targetSkelTransLength))
                            {
                                var sourceSkelTransDir = sourceSkelTrans / sourceSkelTransLength;
                                var targetSkelTransDir = targetSkelTrans / targetSkelTransLength;

                                var deltaRotation = FQuat.FindBetweenNormals(sourceSkelTransDir, targetSkelTransDir);
                                var scale = targetSkelTransLength / sourceSkelTransLength;
                                bonePosition = deltaRotation.RotateVector(bonePosition) * scale;
                            }
                        }
                        break;
                    }
                }

                poses[frameIndex].Bones[boneIndex] = new Bone
                {
                    Name = boneInfo.Name.ToString(),
                    ParentIndex = boneInfo.ParentIndex,
                    Transform = new FTransform(boneOrientation, bonePosition, boneScale),
                    IsValidKey = frameIndex <= numFrames
                };
            }
        }
        return poses;
    }

    public static void AccumulateLocalSpaceAdditivePoseInternal(Pose basePose, Pose additivePose, float weight)
    {
        if (weight < 0.999989986419678)
            throw new NotImplementedException();

        for (int index = 0; index < basePose.Bones.Length; index++)
        {
            basePose.Bones[index].AccumulateWithAdditiveScale(additivePose.Bones[index].Transform, weight);
        }
    }

    public static void AccumulateMeshSpaceRotationAdditiveToLocalPoseInternal(Pose BasePose, Pose AdditivePose, float Weight)
    {
        ConvertPoseToMeshRotation(BasePose);
        AccumulateLocalSpaceAdditivePoseInternal(BasePose, AdditivePose, Weight);
        ConvertMeshRotationPoseToLocalSpace(BasePose);
    }

    public static void ConvertPoseToMeshRotation(Pose LocalPose)
    {
        for (var BoneIndex = 1; BoneIndex < LocalPose.Bones.Length; ++BoneIndex)
        {
            var parentIndex = LocalPose.Bones[BoneIndex].ParentIndex;
            var meshSpaceRotation = LocalPose.Bones[parentIndex].Transform.Rotation * LocalPose.Bones[BoneIndex].Transform.Rotation;
            LocalPose.Bones[BoneIndex].Transform.Rotation = meshSpaceRotation;
        }
    }

    public static void ConvertMeshRotationPoseToLocalSpace(Pose pose)
    {
        for (var BoneIndex = pose.Bones.Length - 1; BoneIndex > 0; --BoneIndex)
        {
            var parentIndex = pose.Bones[BoneIndex].ParentIndex;
            var localSpaceRotation = pose.Bones[parentIndex].Transform.Rotation.Inverse() * pose.Bones[BoneIndex].Transform.Rotation;
            pose.Bones[BoneIndex].Transform.Rotation = localSpaceRotation;
        }
    }
}
