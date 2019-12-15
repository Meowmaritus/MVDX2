﻿using MVDX2.DbgMenus;
using MVDX2.DebugPrimitives;
using MVDX2.GFXShaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVDX2
{
    public enum GFXDrawStep : byte
    {
        DbgPrimPrepass = 1,
        Opaque = 2,
        AlphaEdge = 3,
        DbgPrimOverlay,
        GUI = 5,
    }

    public enum LODMode : int
    {
        Automatic = -1,
        ForceFullRes = 0,
        ForceLOD1 = 1,
        ForceLOD2 = 2,
    }

    public static class GFX
    {
        public static Viewport LastViewport = new Viewport(0, 0, 100, 100);

        public static SurfaceFormat BackBufferFormat => SurfaceFormat.Rgba1010102;

        public static class Display
        {
            public static void SetFromDisplayMode(DisplayMode mode)
            {
                Width = mode.Width;
                Height = mode.Height;
                Format = mode.Format;
            }
            public static int Width = 1600;
            public static int Height = 900;
            public static SurfaceFormat Format = SurfaceFormat.Color;
            public static bool Vsync = true;
            public static bool Fullscreen = false;
            public static bool SimpleMSAA = true;
            public static void Apply()
            {
                Main.ApplyPresentationParameters(Width, Height, Format, Vsync, Fullscreen, SimpleMSAA);
            }
        }

        private static int _ssaa = 2;
        public static int SSAA
        {
            get => _ssaa;
            set
            {
                if (value != _ssaa)
                {
                    _ssaa = value;
                    Main.RequestViewportRenderTargetResolutionChange = true;
                }
            }
        }

        public static int EffectiveSSAA = 2;

        public static int MSAA
        {
            get => Device.PresentationParameters.MultiSampleCount;
            set
            {
                if (value != Device.PresentationParameters.MultiSampleCount)
                {
                    Device.PresentationParameters.MultiSampleCount = value;
                }
            }
        }

        public static void InitShaders()
        {
            FlverShader.Effect.Legacy_AmbientColor = new Vector4(1, 1, 1, 1);
            FlverShader.Effect.Legacy_AmbientIntensity = 0.25f;
            FlverShader.Effect.Legacy_DiffuseColor = new Vector4(1, 1, 1, 1);
            FlverShader.Effect.Legacy_DiffuseIntensity = 0.85f;
            FlverShader.Effect.Legacy_SpecularColor = new Vector4(1, 1, 1, 1);
            FlverShader.Effect.Legacy_SpecularPower = 8;
            FlverShader.Effect.Legacy_NormalMapCustomZ = 1.0f;
            FlverShader.Effect.Legacy_DiffusePower = 1 / 1.5f;
            FlverShader.Effect.AlphaTest = 0.1f;

            FlverShader.Effect.LightDirection = Vector3.Forward;

            FlverShader.Effect.EmissiveMapMult = 1;

            FlverShader.Effect.EnvironmentMap = Environment.CurrentCubemap;
            SkyboxShader.Effect.EnvironmentMap = Environment.CurrentCubemap;

            //GFX.FlverOpacity = 0.15f;
        }

        public static FlverShadingMode? ForcedFlverShadingMode = null;

        private static Dictionary<FlverShadingMode, string> _flverShadingModeNames
            = new Dictionary<FlverShadingMode, string>
        {
            { FlverShadingMode.PBR_GLOSS_DS3, "PBR Gloss (Dark Souls III)" },
            { FlverShadingMode.PBR_GLOSS_BB, "PBR Gloss (Bloodborne) [WIP]" },
            { FlverShadingMode.CLASSIC_DIFFUSE_PTDE, "Classic Diffuse (PTDE) [Placeholder]" },
            { FlverShadingMode.LEGACY, "Legacy (MVDX)" },

            { FlverShadingMode.TEXDEBUG_DIFFUSEMAP, "TEX DEBUG: Diffuse/Albedo Map" },
            { FlverShadingMode.TEXDEBUG_SPECULARMAP, "TEX DEBUG: Specular/Reflectance Map" },
            { FlverShadingMode.TEXDEBUG_NORMALMAP, "TEX DEBUG: Normal Map" },
            { FlverShadingMode.TEXDEBUG_EMISSIVEMAP, "TEX DEBUG: Emissive Map" },
            { FlverShadingMode.TEXDEBUG_BLENDMASKMAP, "TEX DEBUG: Blend Mask Map" },
            { FlverShadingMode.TEXDEBUG_SHININESSMAP, "TEX DEBUG: Shininess Map" },
            { FlverShadingMode.TEXDEBUG_NORMALMAP_BLUE, "TEX DEBUG: Normal Map (Blue Channel)" },

            { FlverShadingMode.MESHDEBUG_NORMALS, "MESH DEBUG: Normals" },
            { FlverShadingMode.MESHDEBUG_NORMALS_MESH_ONLY, "MESH DEBUG: Normals (Mesh Only)" },
            { FlverShadingMode.MESHDEBUG_VERTEX_COLOR_ALPHA, "MESH DEBUG: Vertex Color Alpha" },
        };

        private static List<FlverShadingMode> _flverNonDebugShadingModes = new List<FlverShadingMode>
        {
            FlverShadingMode.LEGACY,
            FlverShadingMode.PBR_GLOSS_DS3,
            FlverShadingMode.PBR_GLOSS_BB,
            FlverShadingMode.CLASSIC_DIFFUSE_PTDE,
        };

        public static IReadOnlyList<FlverShadingMode> FlverNonDebugShadingModes => _flverNonDebugShadingModes;

        public static bool IsInDebugShadingMode => !((GFX.ForcedFlverShadingMode == null || GFX.FlverNonDebugShadingModes.Contains(GFX.ForcedFlverShadingMode.Value)));

        public static IReadOnlyDictionary<FlverShadingMode, string> FlverShadingModeNames => _flverShadingModeNames;

        public static bool FlverAutoRotateLight = false;
        private static float LightSpinTimer = 0;
        public static bool FlverLightFollowsCamera = true;

        public static bool FlverDisableTextureBlending = false;

        public static bool UseTonemap = true;

        public static float FlverOpacity = 1.0f;

        public static GFXDrawStep CurrentStep = GFXDrawStep.Opaque;

        public static readonly GFXDrawStep[] DRAW_STEP_LIST;
        static GFX()
        {
            DRAW_STEP_LIST = (GFXDrawStep[])Enum.GetValues(typeof(GFXDrawStep));
        }

        public static bool HideFLVERs = false;

        public const int LOD_MAX = 2;
        public static LODMode LODMode = LODMode.Automatic;
        public static float LOD1Distance = 200;
        public static float LOD2Distance = 400;

        public static IGFXShader<FlverShader> FlverShader;
        public static IGFXShader<SkyboxShader> SkyboxShader;
        public static IGFXShader<CollisionShader> CollisionShader;
        public static IGFXShader<DbgPrimWireShader> DbgPrimWireShader;
        public static IGFXShader<DbgPrimSolidShader> DbgPrimSolidShader;
        public static Stopwatch FpsStopwatch = new Stopwatch();
        private static FrameCounter FpsCounter = new FrameCounter();

        public static float FPS => FpsCounter.CurrentFramesPerSecond;
        public static float AverageFPS => FpsCounter.AverageFramesPerSecond;

        public static bool EnableFrustumCulling = false;
        public static bool EnableTextures = true;
        //public static bool EnableLightmapping = true;

        private static RasterizerState HotSwapRasterizerState_BackfaceCullingOff_WireframeOff;
        private static RasterizerState HotSwapRasterizerState_BackfaceCullingOn_WireframeOff;
        private static RasterizerState HotSwapRasterizerState_BackfaceCullingOff_WireframeOn;
        private static RasterizerState HotSwapRasterizerState_BackfaceCullingOn_WireframeOn;

        private static DepthStencilState DepthStencilState_Normal;
        private static DepthStencilState DepthStencilState_DontWriteDepth;

        public static WorldView World = new WorldView();

        public static GraphicsDevice Device;
        //public static FlverShader FlverShader;
        //public static DbgPrimShader DbgPrimShader;
        public static SpriteBatch SpriteBatch;
        public static string FlverShader__Name => $@"{Main.ExeDirectory}\Content\Shaders\FlverShader";
        public static string CollisionShader__Name => $@"{Main.ExeDirectory}\Content\Shaders\CollisionShader";
        public static string SkyboxShader__Name => $@"{Main.ExeDirectory}\Content\Shaders\CubemapSkyboxShader";

        private static bool _wireframe = false;
        public static bool Wireframe
        {
            set
            {
                _wireframe = value;
                UpdateRasterizerState();
            }
            get => _wireframe;
        }

        private static bool _backfaceCulling = false;
        public static bool BackfaceCulling
        {
            set
            {
                _backfaceCulling = value;
                UpdateRasterizerState();
            }
            get => _backfaceCulling;
        }

        private static void UpdateRasterizerState()
        {
            if (!BackfaceCulling && !Wireframe)
                Device.RasterizerState = HotSwapRasterizerState_BackfaceCullingOff_WireframeOff;
            else if (!BackfaceCulling && Wireframe)
                Device.RasterizerState = HotSwapRasterizerState_BackfaceCullingOff_WireframeOn;
            else if (BackfaceCulling && !Wireframe)
                Device.RasterizerState = HotSwapRasterizerState_BackfaceCullingOn_WireframeOff;
            else if (BackfaceCulling && Wireframe)
                Device.RasterizerState = HotSwapRasterizerState_BackfaceCullingOn_WireframeOn;
        }

        private static void CompletelyChangeRasterizerState(RasterizerState rs)
        {
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOff = rs.GetCopyOfState();
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOff.CullMode = CullMode.None;
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOff.FillMode = FillMode.Solid;

            HotSwapRasterizerState_BackfaceCullingOn_WireframeOff = rs.GetCopyOfState();
            HotSwapRasterizerState_BackfaceCullingOn_WireframeOff.CullMode = CullMode.CullClockwiseFace;
            HotSwapRasterizerState_BackfaceCullingOn_WireframeOff.FillMode = FillMode.Solid;

            HotSwapRasterizerState_BackfaceCullingOff_WireframeOn = rs.GetCopyOfState();
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOn.CullMode = CullMode.None;
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOn.FillMode = FillMode.WireFrame;

            HotSwapRasterizerState_BackfaceCullingOn_WireframeOn = rs.GetCopyOfState();
            HotSwapRasterizerState_BackfaceCullingOn_WireframeOn.CullMode = CullMode.CullClockwiseFace;
            HotSwapRasterizerState_BackfaceCullingOn_WireframeOn.FillMode = FillMode.WireFrame;

            UpdateRasterizerState();
        }

        public static void InitDepthStencil()
        {
            switch (CurrentStep)
            {
                case GFXDrawStep.Opaque:
                    Device.DepthStencilState = DepthStencilState_Normal;
                    break;
                default:
                    Device.DepthStencilState = DepthStencilState_DontWriteDepth;
                    break;
            }
        }

        public static void Init(ContentManager c)
        {

            DepthStencilState_Normal = new DepthStencilState()
            {
                //CounterClockwiseStencilDepthBufferFail = Device.DepthStencilState.CounterClockwiseStencilDepthBufferFail,
                //CounterClockwiseStencilFail = Device.DepthStencilState.CounterClockwiseStencilFail,
                //CounterClockwiseStencilFunction = Device.DepthStencilState.CounterClockwiseStencilFunction,
                //CounterClockwiseStencilPass = Device.DepthStencilState.CounterClockwiseStencilPass,
                //DepthBufferEnable = Device.DepthStencilState.DepthBufferEnable,
                //DepthBufferFunction = Device.DepthStencilState.DepthBufferFunction,
                DepthBufferWriteEnable = true,
                //ReferenceStencil = Device.DepthStencilState.ReferenceStencil,
                //StencilDepthBufferFail = Device.DepthStencilState.StencilDepthBufferFail,
                //StencilEnable = Device.DepthStencilState.StencilEnable,
                //StencilFail = Device.DepthStencilState.StencilFail,
                //StencilFunction = Device.DepthStencilState.StencilFunction,
                //StencilMask = Device.DepthStencilState.StencilMask,
                //StencilPass = Device.DepthStencilState.StencilPass,
                //StencilWriteMask = Device.DepthStencilState.StencilWriteMask,
                //TwoSidedStencilMode = Device.DepthStencilState.TwoSidedStencilMode,
            };

            DepthStencilState_DontWriteDepth = new DepthStencilState()
            {
                //CounterClockwiseStencilDepthBufferFail = Device.DepthStencilState.CounterClockwiseStencilDepthBufferFail,
                //CounterClockwiseStencilFail = Device.DepthStencilState.CounterClockwiseStencilFail,
                //CounterClockwiseStencilFunction = Device.DepthStencilState.CounterClockwiseStencilFunction,
                //CounterClockwiseStencilPass = Device.DepthStencilState.CounterClockwiseStencilPass,
                //DepthBufferEnable = Device.DepthStencilState.DepthBufferEnable,
                //DepthBufferFunction = Device.DepthStencilState.DepthBufferFunction,
                DepthBufferWriteEnable = false,
                //ReferenceStencil = Device.DepthStencilState.ReferenceStencil,
                //StencilDepthBufferFail = Device.DepthStencilState.StencilDepthBufferFail,
                //StencilEnable = Device.DepthStencilState.StencilEnable,
                //StencilFail = Device.DepthStencilState.StencilFail,
                //StencilFunction = Device.DepthStencilState.StencilFunction,
                //StencilMask = Device.DepthStencilState.StencilMask,
                //StencilPass = Device.DepthStencilState.StencilPass,
                //StencilWriteMask = Device.DepthStencilState.StencilWriteMask,
                //TwoSidedStencilMode = Device.DepthStencilState.TwoSidedStencilMode,
            };

            Environment.LoadContent(c);

            FlverShader = new FlverShader(c.Load<Effect>(FlverShader__Name));
            SkyboxShader = new SkyboxShader(c.Load<Effect>(SkyboxShader__Name));

            InitShaders();

            DbgPrimWireShader = new DbgPrimWireShader(Device);
            DbgPrimSolidShader = new DbgPrimSolidShader(Device);

            DbgPrimSolidShader.Effect.AmbientLightColor = new Vector3(FlverShader.Effect.Legacy_AmbientColor.X, FlverShader.Effect.Legacy_AmbientColor.Y, FlverShader.Effect.Legacy_AmbientColor.Z) * FlverShader.Effect.Legacy_AmbientIntensity;
            DbgPrimSolidShader.Effect.DiffuseColor = new Vector3(FlverShader.Effect.Legacy_DiffuseColor.X, FlverShader.Effect.Legacy_DiffuseColor.Y, FlverShader.Effect.Legacy_DiffuseColor.Z) * FlverShader.Effect.Legacy_DiffuseIntensity;
            DbgPrimSolidShader.Effect.SpecularColor = new Vector3(FlverShader.Effect.Legacy_SpecularColor.X, FlverShader.Effect.Legacy_SpecularColor.Y, FlverShader.Effect.Legacy_SpecularColor.Z);
            DbgPrimSolidShader.Effect.SpecularPower = FlverShader.Effect.Legacy_SpecularPower;

            CollisionShader = new CollisionShader(c.Load<Effect>(CollisionShader__Name));
            CollisionShader.Effect.AmbientColor = new Vector4(0.2f, 0.5f, 0.9f, 1.0f);
            CollisionShader.Effect.DiffuseColor = new Vector4(0.2f, 0.5f, 0.9f, 1.0f);

            SpriteBatch = new SpriteBatch(Device);

            HotSwapRasterizerState_BackfaceCullingOff_WireframeOff = Device.RasterizerState.GetCopyOfState();
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOff.MultiSampleAntiAlias = true;
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOff.CullMode = CullMode.None;
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOff.FillMode = FillMode.Solid;

            HotSwapRasterizerState_BackfaceCullingOn_WireframeOff = Device.RasterizerState.GetCopyOfState();
            HotSwapRasterizerState_BackfaceCullingOn_WireframeOff.MultiSampleAntiAlias = true;
            HotSwapRasterizerState_BackfaceCullingOn_WireframeOff.CullMode = CullMode.CullClockwiseFace;
            HotSwapRasterizerState_BackfaceCullingOn_WireframeOff.FillMode = FillMode.Solid;

            HotSwapRasterizerState_BackfaceCullingOff_WireframeOn = Device.RasterizerState.GetCopyOfState();
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOn.MultiSampleAntiAlias = true;
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOn.CullMode = CullMode.None;
            HotSwapRasterizerState_BackfaceCullingOff_WireframeOn.FillMode = FillMode.WireFrame;

            HotSwapRasterizerState_BackfaceCullingOn_WireframeOn = Device.RasterizerState.GetCopyOfState();
            HotSwapRasterizerState_BackfaceCullingOn_WireframeOn.MultiSampleAntiAlias = true;
            HotSwapRasterizerState_BackfaceCullingOn_WireframeOn.CullMode = CullMode.CullClockwiseFace;
            HotSwapRasterizerState_BackfaceCullingOn_WireframeOn.FillMode = FillMode.WireFrame;

        }

        public static bool SpriteBatchHasBegun { get; private set; } = false;

        public static void SpriteBatchBeginForText()
        {
            GFX.SpriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);
            SpriteBatchHasBegun = true;
        }

        public static void SpriteBatchBegin(SpriteSortMode sortMode = SpriteSortMode.Deferred, 
            BlendState blendState = null, SamplerState samplerState = null, 
            DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null, 
            Effect effect = null, Matrix? transformMatrix = null)
        {
            SpriteBatch.Begin(sortMode, blendState, samplerState, depthStencilState, 
                rasterizerState, effect, transformMatrix);
        }

        public static void SpriteBatchEnd()
        {
            GFX.SpriteBatch.End();
            SpriteBatchHasBegun = false;
        }

        public static void BeginDraw()
        {
            InitDepthStencil();
            //InitBlendState();

            World.ApplyViewToShader(DbgPrimWireShader, Matrix.Identity);
            World.ApplyViewToShader(DbgPrimSolidShader, Matrix.Identity);
            World.ApplyViewToShader(FlverShader, Matrix.Identity);
            World.ApplyViewToShader(CollisionShader, Matrix.Identity);

            //foreach (var m in ModelDrawer.Models)
            //{
            //    m.ApplyWorldToInstances(World.CameraTransform.RotationMatrix);
            //    m.ReinitInstanceData();
            //}

            Device.SamplerStates[0] = SamplerState.LinearWrap;

            FlverShader.Effect.EyePosition = World.CameraTransform.Position;
            //SkyboxShader.Effect.EyePosition = World.CameraTransform.Position;
            SkyboxShader.Effect.EyePosition = Vector3.Zero;
            //FlverShader.Effect.EyePosition = Vector3.Backward;


            if (FlverAutoRotateLight)
            {
                LightSpinTimer = (LightSpinTimer + Main.DELTA_UPDATE);
                FlverShader.Effect.LightDirection = Vector3.Transform(Vector3.Forward, Matrix.CreateRotationY(MathHelper.Pi * LightSpinTimer * 0.05f));
            }
            else if (FlverLightFollowsCamera)
            {
                FlverShader.Effect.LightDirection = -Vector3.Normalize(World.CameraTransform.Position);
            }
            

            //FlverShader.Effect.LightDirection = World.CameraTransform.RotationMatrix;
            FlverShader.Effect.ColorMap = Main.DEFAULT_TEXTURE_DIFFUSE;
            FlverShader.Effect.NormalMap = Main.DEFAULT_TEXTURE_NORMAL;
            FlverShader.Effect.SpecularMap = Main.DEFAULT_TEXTURE_SPECULAR;
            FlverShader.Effect.EmissiveMap = Main.DEFAULT_TEXTURE_EMISSIVE;
            FlverShader.Effect.BlendmaskMap = Main.DEFAULT_TEXTURE_EMISSIVE;

            FlverShader.Effect.EnvironmentMap = Environment.CurrentCubemap;
            SkyboxShader.Effect.EnvironmentMap = Environment.CurrentCubemap;

            FlverShader.Effect.AmbientLightMult = Environment.FlverIndirectLightMult * 1;
            FlverShader.Effect.DirectLightMult = Environment.FlverDirectLightMult * 1;
            FlverShader.Effect.IndirectLightMult = 1.0f;
            FlverShader.Effect.SceneBrightness = Environment.FlverSceneBrightness * 1.45f * 2;
            FlverShader.Effect.EmissiveMapMult = Environment.FlverEmissiveMult;
            FlverShader.Effect.Legacy_SceneBrightness = Environment.FlverSceneBrightness * 1.45f * 2;
            FlverShader.Effect.Opacity = FlverOpacity;

            SkyboxShader.Effect.AmbientLightMult = Environment.FlverIndirectLightMult * 1;
            SkyboxShader.Effect.SceneBrightness = Environment.FlverSceneBrightness * 1.45f * 2;

            DbgPrimSolidShader.Effect.DirectionalLight0.Enabled = true;
            DbgPrimSolidShader.Effect.DirectionalLight0.DiffuseColor = Vector3.One * 0.45f;
            DbgPrimSolidShader.Effect.DirectionalLight0.SpecularColor = Vector3.One * 0.35f;
            DbgPrimSolidShader.Effect.DirectionalLight0.Direction = FlverShader.Effect.LightDirection;
            DbgPrimSolidShader.Effect.DirectionalLight1.Enabled = false;
            DbgPrimSolidShader.Effect.DirectionalLight2.Enabled = false;

            CollisionShader.Effect.EyePosition = World.CameraTransform.Position;

        }

        private static void DoDrawStep()
        {
            switch (CurrentStep)
            {
                case GFXDrawStep.Opaque:
                case GFXDrawStep.AlphaEdge:
                    if (!HideFLVERs)
                    {
                        Scene.Draw();
                    }
                    break;
                case GFXDrawStep.DbgPrimOverlay:
                    DBG.DrawPrimitives();
                    break;
                case GFXDrawStep.DbgPrimPrepass:
                    DBG.DrawBehindPrims();
                    break;
                case GFXDrawStep.GUI:
                    DBG.DrawPrimitiveTexts();
                    if (DBG.EnableMenu)
                        DbgMenuItem.CurrentMenu.Draw();
                    break;
            }
        }

        private static void DoDraw()
        {
            if (Main.DISABLE_DRAW_ERROR_HANDLE)
            {
                DoDrawStep();
            }
            else
            {
                try
                {
                    DoDrawStep();
                }
                catch (Exception ex)
                {
                    var errText = $"Draw Call Failed ({CurrentStep.ToString()}):\n\n{ex.ToString()}";
                    var errTextSize = DBG.DEBUG_FONT_SIMPLE.MeasureString(errText);
                    float hScale = ((GFX.LastViewport.Width) / (errTextSize.X)) * GFX.EffectiveSSAA;

                    if (SpriteBatchHasBegun)
                        SpriteBatchEnd();

                    SpriteBatchBeginForText();

                    DBG.DrawOutlinedText(errText, new Vector2(0, GFX.LastViewport.Height / 2 - (errTextSize.Y / 2)), 
                        Color.Yellow, DBG.DEBUG_FONT_SIMPLE, 0, new Vector2(hScale, GFX.EffectiveSSAA), Vector2.Zero);

                    SpriteBatchEnd();
                }
            }
        }

        public static void DrawScene3D()
        {
            CurrentStep = GFXDrawStep.DbgPrimPrepass;
            BeginDraw();
            DoDraw();

            CurrentStep = GFXDrawStep.Opaque;
            BeginDraw();
            DoDraw();

            CurrentStep = GFXDrawStep.AlphaEdge;
            BeginDraw();
            DoDraw();
        }

        public static void DrawSceneOver3D()
        {
            CurrentStep = GFXDrawStep.DbgPrimOverlay;
            BeginDraw();
            DoDraw();
        }

        public static void DrawSceneGUI()
        {
            CurrentStep = GFXDrawStep.GUI;
            BeginDraw();
            DoDraw();

            GFX.UpdateFPS((float)FpsStopwatch.Elapsed.TotalSeconds);
            if (Main.SceneRenderTarget != null)
            {
                GFX.SpriteBatchBeginForText();
                DBG.DrawOutlinedText($"Rendering {(Main.SceneRenderTarget.Width)}x{(Main.SceneRenderTarget.Height)} @ {(Math.Round(GFX.AverageFPS))} FPS", new Vector2(4, GFX.Device.Viewport.Height - 24), Color.Cyan, font: DBG.DEBUG_FONT_SMALL);
                GFX.SpriteBatchEnd();
            }
            //DBG.DrawOutlinedText($"FPS: {(Math.Round(1 / (float)gameTime.ElapsedGameTime.TotalSeconds))}", new Vector2(0, GFX.Device.Viewport.Height - 20), Color.Cyan, font: DBG.DEBUG_FONT_SMALL);
            FpsStopwatch.Restart();
        }

        public static void UpdateFPS(float elapsedSeconds)
        {
            FpsCounter.Update(elapsedSeconds);
        }
    }
}
