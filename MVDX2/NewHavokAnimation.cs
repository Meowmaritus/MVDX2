﻿using Microsoft.Xna.Framework;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVDX2
{
    public abstract class NewHavokAnimation
    {
        public HKX.AnimationBlendHint BlendHint = HKX.AnimationBlendHint.NORMAL;

        public string Name;

        public override string ToString()
        {
            return $"{Name} [{Math.Round(1 / FrameDuration)} FPS]";
        }

        public readonly NewAnimSkeleton Skeleton;

        public readonly Vector4 RootMotionUp = new Vector4(0, 1, 0, 0);
        public readonly Vector4 RootMotionForward = new Vector4(0, 0, 1, 0);
        public readonly Vector4[] RootMotionFrames = null;

        private Vector4 currentRootMotionVector4 = Vector4.Zero;
        public Matrix CurrentRootMotionMatrix = Matrix.Identity;

        private object _lock_boneMatrixStuff = new object();

        private List<NewBlendableTransform> blendableTransforms = new List<NewBlendableTransform>();
        private List<int> bonesAlreadyCalculated = new List<int>();

        public bool IsAdditiveBlend =>
            BlendHint == HKX.AnimationBlendHint.ADDITIVE ||
            BlendHint == HKX.AnimationBlendHint.ADDITIVE_DEPRECATED;

        public float Duration;
        public float FrameDuration;
        public int FrameCount;

        public bool HasEnded => CurrentTime >= Duration;

        public float CurrentTime;

        public float CurrentFrame => CurrentTime / FrameDuration;

        public abstract NewBlendableTransform GetBlendableTransformOnCurrentFrame(int hkxBoneIndex);

        public void ApplyMotionToSkeleton()
        {
            WriteCurrentFrameToSkeleton();
            UpdateCurrentRootMotion();
        }

        public void Scrub(float newTime, bool loop, bool forceUpdate = false)
        {
            if (newTime != CurrentTime)
            {
                CurrentTime = newTime;

                if (loop)
                {
                    if (CurrentTime >= Duration)
                    {
                        CurrentTime = CurrentTime % Duration;
                    }
                }
                else
                {
                    if (CurrentTime > (Duration - FrameDuration))
                    {
                        CurrentTime = (Duration - FrameDuration);
                    }
                }  

                ApplyMotionToSkeleton();
            }
            else if (forceUpdate)
            {
                ApplyMotionToSkeleton();
            }
        }

        public void Play(float deltaTime, bool loop, bool forceUpdate = false)
        {
            float oldTime = CurrentTime;

            if (loop)
            {
                CurrentTime += deltaTime;
                CurrentTime = CurrentTime % Duration;
            }
            else
            {
                CurrentTime += deltaTime;
                if (CurrentTime > (Duration - FrameDuration))
                    CurrentTime = (Duration - FrameDuration);
            }

            if (forceUpdate || (oldTime != CurrentTime))
            {
                ApplyMotionToSkeleton();
            }
        }

        private void UpdateCurrentRootMotion()
        {
            if (RootMotionFrames != null)
            {
                float frameFloor = (float)Math.Floor(CurrentFrame % RootMotionFrames.Length);
                currentRootMotionVector4 = RootMotionFrames[(int)frameFloor];

                if (CurrentFrame != frameFloor)
                {
                    float frameMod = CurrentFrame % 1;

                    Vector4 nextFrameRootMotion;
                    if (CurrentFrame >= RootMotionFrames.Length - 1)
                        nextFrameRootMotion = RootMotionFrames[0];
                    else
                        nextFrameRootMotion = RootMotionFrames[(int)(frameFloor + 1)];

                    currentRootMotionVector4.X = MathHelper.Lerp(currentRootMotionVector4.X, nextFrameRootMotion.X, frameMod);
                    currentRootMotionVector4.Y = MathHelper.Lerp(currentRootMotionVector4.Y, nextFrameRootMotion.Y, frameMod);
                    currentRootMotionVector4.Z = MathHelper.Lerp(currentRootMotionVector4.Z, nextFrameRootMotion.Z, frameMod);
                    currentRootMotionVector4.W = MathHelper.Lerp(currentRootMotionVector4.W, nextFrameRootMotion.W, frameMod);
                }
            }
            else
            {
                currentRootMotionVector4 = Vector4.Zero;
            }

            

            CurrentRootMotionMatrix = 
                Matrix.CreateRotationY(currentRootMotionVector4.W) *
                Matrix.CreateWorld(
                    new Vector3(currentRootMotionVector4.X, currentRootMotionVector4.Y, currentRootMotionVector4.Z),
                    new Vector3(RootMotionForward.X, RootMotionForward.Y, -RootMotionForward.Z),
                    new Vector3(RootMotionUp.X, RootMotionUp.Y, RootMotionUp.Z));
        }

        public NewHavokAnimation(NewAnimSkeleton skeleton, HKX.HKADefaultAnimatedReferenceFrame refFrame, HKX.HKAAnimationBinding binding)
        {
            Skeleton = skeleton;
            if (refFrame != null)
            {
                RootMotionFrames = new Vector4[refFrame.ReferenceFrameSamples.Size];
                for (int i = 0; i < refFrame.ReferenceFrameSamples.Size; i++)
                {
                    RootMotionFrames[i] = new Vector4(
                        refFrame.ReferenceFrameSamples[i].Vector.X, 
                        refFrame.ReferenceFrameSamples[i].Vector.Y, 
                        refFrame.ReferenceFrameSamples[i].Vector.Z, 
                        refFrame.ReferenceFrameSamples[i].Vector.W);
                }
                RootMotionUp = new Vector4(refFrame.Up.X, refFrame.Up.Y, refFrame.Up.Z, refFrame.Up.W);
                RootMotionForward = new Vector4(refFrame.Forward.X, refFrame.Forward.Y, refFrame.Forward.Z, refFrame.Forward.W);
            }

            lock (_lock_boneMatrixStuff)
            {
                blendableTransforms = new List<NewBlendableTransform>();
                for (int i = 0; i < skeleton.HkxSkeleton.Count; i++)
                {
                    blendableTransforms.Add(NewBlendableTransform.Identity);
                }
            }

            BlendHint = binding.BlendHint;
        }

        public void WriteCurrentFrameToSkeleton()
        {
            bonesAlreadyCalculated.Clear();

            lock (_lock_boneMatrixStuff)
            {
                void WalkTree(int i, Matrix currentMatrix, Vector3 currentScale)
                {
                    if (!bonesAlreadyCalculated.Contains(i))
                    {
                        blendableTransforms[i] = GetBlendableTransformOnCurrentFrame(i);
                        currentMatrix = blendableTransforms[i].GetMatrix() * currentMatrix;
                        currentScale *= blendableTransforms[i].Scale;
                        Skeleton.SetHkxBoneMatrix(i, Matrix.CreateScale(currentScale) * currentMatrix);
                        bonesAlreadyCalculated.Add(i);
                    }

                    foreach (var c in Skeleton.HkxSkeleton[i].ChildIndices)
                        WalkTree(c, currentMatrix, currentScale);
                }

                foreach (var root in Skeleton.RootBoneIndices)
                    WalkTree(root, Matrix.Identity, Vector3.One);
            }

        }
    }
}
