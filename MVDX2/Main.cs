using MVDX2.DbgMenus;
using MVDX2.DebugPrimitives;
using MVDX2.GFXShaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MVDX2
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class Main : Game
    {
        //public static Form WinForm;

        public static string ExeDirectory = null;

        public const string VERSION = "Version 1.5.5";

        public static bool FIXED_TIME_STEP = false;

        public static bool REQUEST_EXIT = false;

        public static float DELTA_UPDATE;
        public static float DELTA_UPDATE_ROUNDED;
        public static float DELTA_DRAW;

        public static IServiceProvider ContentServiceProvider = null;

        private bool prevFrameWasLoadingTaskRunning = false;

        public static bool Active { get; private set; }

        public static bool DISABLE_DRAW_ERROR_HANDLE = false;

        private static float MemoryUsageCheckTimer = 0;
        private static long MemoryUsage_Unmanaged = 0;
        private static long MemoryUsage_Managed = 0;
        private const float MemoryUsageCheckInterval = 0.25f;

        public static readonly Color SELECTED_MESH_COLOR = Color.Yellow * 0.05f;
        //public static readonly Color SELECTED_MESH_WIREFRAME_COLOR = Color.Yellow;

        public static Texture2D DEFAULT_TEXTURE_DIFFUSE;
        public static Texture2D DEFAULT_TEXTURE_SPECULAR;
        public static Texture2D DEFAULT_TEXTURE_NORMAL;
        public static Texture2D DEFAULT_TEXTURE_MISSING;
        public static TextureCube DEFAULT_TEXTURE_MISSING_CUBE;
        public static Texture2D DEFAULT_TEXTURE_EMISSIVE;
        public string DEFAULT_TEXTURE_MISSING_NAME => $@"{Main.ExeDirectory}\Content\Utility\MissingTexture";

        //public static TaeEditor.TaeEditorScreen TAE_EDITOR;
        private static SpriteBatch TaeEditorSpriteBatch;
        public static Texture2D TAE_EDITOR_BLANK_TEX;
        public static SpriteFont TAE_EDITOR_FONT;
        public static SpriteFont TAE_EDITOR_FONT_SMALL;
        public static Texture2D TAE_EDITOR_SCROLLVIEWER_ARROW;

        public static FlverTonemapShader MainFlverTonemapShader = null;

        //public static Stopwatch UpdateStopwatch = new Stopwatch();
        //public static TimeSpan MeasuredTotalTime = TimeSpan.Zero;
        //public static TimeSpan MeasuredElapsedTime = TimeSpan.Zero;

        public bool IsLoadingTaskRunning = false;

        public static ContentManager CM = null;

        public static RenderTarget2D SceneRenderTarget = null;

        public static bool RequestViewportRenderTargetResolutionChange = false;
        private const float TimeBeforeNextRenderTargetUpdate_Max = 0.5f;
        private static float TimeBeforeNextRenderTargetUpdate = 0;

        public Rectangle TAEScreenBounds
        {
            get => GFX.Device.Viewport.Bounds;
            set
            {
                if (value != TAEScreenBounds)
                {
                    GFX.Device.Viewport = new Viewport(value);
                }
            }
        }

        public Rectangle ClientBounds => new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);

        private static GraphicsDeviceManager graphics;
        //public ContentManager Content;
        //public bool IsActive = true;

        public static List<DisplayMode> GetAllResolutions()
        {
            List<DisplayMode> result = new List<DisplayMode>();
            foreach (var mode in GraphicsAdapter.DefaultAdapter.SupportedDisplayModes)
            {
                result.Add(mode);
            }
            return result;
        }

        public static void ApplyPresentationParameters(int width, int height, SurfaceFormat format,
            bool vsync, bool fullscreen, bool simpleMsaa)
        {
            graphics.PreferMultiSampling = simpleMsaa;
            graphics.PreferredBackBufferWidth = width;
            graphics.PreferredBackBufferHeight = height;
            graphics.PreferredBackBufferFormat = GFX.BackBufferFormat;
            graphics.IsFullScreen = fullscreen;
            graphics.SynchronizeWithVerticalRetrace = vsync;

            graphics.ApplyChanges();
        }



        //MCG MCGTEST_MCG;



        public static Model StandaloneSelectedModel = null;

        static DbgMenuPadRepeater StandalonePadUp = new DbgMenuPadRepeater(Buttons.A, 0.25f, 0.1f);
        static DbgMenuPadRepeater StandalonePadDown = new DbgMenuPadRepeater(Buttons.B, 0.25f, 0.1f);
        static DbgMenuPadRepeater StandalonePadLeft = new DbgMenuPadRepeater(Buttons.DPadLeft, 0.25f, 0.1f);
        static DbgMenuPadRepeater StandalonePadRight = new DbgMenuPadRepeater(Buttons.DPadRight, 0.25f, 0.1f);

        static bool StandalonePrevFramePause = false;
        static bool StandalonePrevFrameToggleRootMotion = false;


        public static void UpdateStandaloneViewer()
        {
            var key = Keyboard.GetState();
            StandalonePadUp.Update(GamePadState.Default, DELTA_UPDATE, key.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up));
            StandalonePadDown.Update(GamePadState.Default, DELTA_UPDATE, key.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down));
            StandalonePadLeft.Update(GamePadState.Default, DELTA_UPDATE, key.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Left));
            StandalonePadRight.Update(GamePadState.Default, DELTA_UPDATE, key.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Right));

            bool pause = key.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Space);
            bool toggleRootMotion = key.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Tab);

           

            if (StandaloneSelectedModel != null)
            {
                if (pause && !StandalonePrevFramePause)
                {
                    StandaloneSelectedModel.AnimContainer.IsPlaying = !StandaloneSelectedModel.AnimContainer.IsPlaying;
                }

                if (toggleRootMotion && !StandalonePrevFrameToggleRootMotion)
                {
                    StandaloneSelectedModel.AnimContainer.EnableRootMotion = !StandaloneSelectedModel.AnimContainer.EnableRootMotion;
                }

                lock (Scene._lock_ModelLoad_Draw)
                {
                    if (Scene.Models.Count > 1)
                    {
                        if (StandalonePadRight.State)
                        {
                            var curIndex = Scene.Models.IndexOf(StandaloneSelectedModel);
                            curIndex++;
                            if (curIndex >= Scene.Models.Count)
                                curIndex = 0;
                            StandaloneSelectedModel = Scene.Models[curIndex];
                            GameDataManager.Init(StandaloneSelectedModel.LoadedWithGameType, 
                                StandaloneSelectedModel.LoadedWithInterroot);
                        }

                        if (StandalonePadLeft.State)
                        {
                            var curIndex = Scene.Models.IndexOf(StandaloneSelectedModel);
                            curIndex--;
                            if (curIndex < 0)
                                curIndex = Scene.Models.Count - 1;
                            StandaloneSelectedModel = Scene.Models[curIndex];
                            GameDataManager.Init(StandaloneSelectedModel.LoadedWithGameType, 
                                StandaloneSelectedModel.LoadedWithInterroot);
                        }
                    }
                }

                if (key.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C))
                {
                    var mainBoneTransform = StandaloneSelectedModel.Skeleton.ShaderMatrices0[0];
                    var bonePos = Vector3.Transform(Vector3.Zero, mainBoneTransform);
                    StandaloneSelectedModel.StartTransform = new Transform(Matrix.CreateTranslation(-bonePos));
                }

                if (StandalonePadUp.State)
                {
                    var animNames = StandaloneSelectedModel.AnimContainer.Animations.Keys.ToList();
                    if (animNames.Count > 1)
                    {
                        var curAnimIndex = animNames.IndexOf(StandaloneSelectedModel.AnimContainer.CurrentAnimationName);
                        curAnimIndex--;
                        if (curAnimIndex < 0)
                            curAnimIndex = animNames.Count - 1;
                        StandaloneSelectedModel.AnimContainer.CurrentAnimationName = animNames[curAnimIndex];
                    }
                }

                if (StandalonePadDown.State)
                {
                    var animNames = StandaloneSelectedModel.AnimContainer.Animations.Keys.ToList();
                    if (animNames.Count > 1)
                    {
                        var curAnimIndex = animNames.IndexOf(StandaloneSelectedModel.AnimContainer.CurrentAnimationName);
                        curAnimIndex++;
                        if (curAnimIndex >= animNames.Count)
                            curAnimIndex = 0;
                        StandaloneSelectedModel.AnimContainer.CurrentAnimationName = animNames[curAnimIndex];
                    }
                }

                GFX.World.ModelHeight_ForOrbitCam =
                Math.Abs(StandaloneSelectedModel.Bounds.Max.Y - StandaloneSelectedModel.Bounds.Min.Y);
                GFX.World.ModelCenter_ForOrbitCam = (StandaloneSelectedModel.Bounds.Max - StandaloneSelectedModel.Bounds.Min) / 2;
                GFX.World.ModelDepth_ForOrbitCam =
                    Math.Abs(StandaloneSelectedModel.Bounds.Max.Z - StandaloneSelectedModel.Bounds.Min.Z);
            }


            StandalonePrevFramePause = pause;
            StandalonePrevFrameToggleRootMotion = toggleRootMotion;
            
        }

        public static void DrawStandaloneViewer()
        {
            if (StandaloneSelectedModel != null)
            {
                GFX.SpriteBatchBeginForText();
                DBG.DrawOutlinedText("Selected Model (Left/Right): " 
                    + StandaloneSelectedModel.Name,
                    new Vector2(16, 16 + (32 * 0)), Color.Yellow);
                DBG.DrawOutlinedText("Selected Animation (Up/Down): "
                    + StandaloneSelectedModel.AnimContainer.CurrentAnimationName,
                    new Vector2(16, 16 + (32 * 1)), Color.Yellow);
                DBG.DrawOutlinedText("Play Animation (Space): "
                    + (StandaloneSelectedModel.AnimContainer.IsPlaying ? "YES" : "NO"),
                    new Vector2(16, 16 + (32 * 2)), Color.Yellow);
                DBG.DrawOutlinedText("Enable Root Motion (Tab): "
                    + (StandaloneSelectedModel.AnimContainer.EnableRootMotion ? "YES" : "NO"),
                    new Vector2(16, 16 + (32 * 3)), Color.Yellow);

                GFX.SpriteBatchEnd();
            }
        }

        public static void InitStandaloneViewer()
        {
            Environment.CurrentCubemapName = "m32_00_GILM0131";

            DBG.CategoryEnableDraw[DbgPrimCategory.DummyPoly] = false;
            DBG.CategoryEnableDraw[DbgPrimCategory.DummyPolyHelper] = false;
            DBG.CategoryEnableDraw[DbgPrimCategory.FlverBone] = false;
            DBG.CategoryEnableDraw[DbgPrimCategory.FlverBoneBoundingBox] = false;
            DBG.CategoryEnableDraw[DbgPrimCategory.HkxBone] = false;

            DBG.CategoryEnableNameDraw[DbgPrimCategory.DummyPoly] = false;
            DBG.CategoryEnableNameDraw[DbgPrimCategory.DummyPolyHelper] = false;
            DBG.CategoryEnableNameDraw[DbgPrimCategory.FlverBone] = false;
            DBG.CategoryEnableNameDraw[DbgPrimCategory.FlverBoneBoundingBox] = false;
            DBG.CategoryEnableNameDraw[DbgPrimCategory.HkxBone] = false;

            DBG.CategoryEnableDbgLabelDraw[DbgPrimCategory.DummyPoly] = false;
            DBG.CategoryEnableDbgLabelDraw[DbgPrimCategory.DummyPolyHelper] = false;
            DBG.CategoryEnableDbgLabelDraw[DbgPrimCategory.FlverBone] = false;
            DBG.CategoryEnableDbgLabelDraw[DbgPrimCategory.FlverBoneBoundingBox] = false;
            DBG.CategoryEnableDbgLabelDraw[DbgPrimCategory.HkxBone] = false;



            //LoadingTaskMan.DoLoadingTask("ALL_CHR_LINEUP_DS3_MEOW",
            //    "Loading ALL CHR LINEUP...", progress =>
            //    {
            //        GameDataManager.Init(GameDataManager.GameTypes.DS1,
            //            @"C:\Program Files (x86)\Steam\steamapps\common\Dark Souls Prepare to Die Edition\DATA");
            //        StandaloneModel = GameDataManager.LoadCharacter("c4100");
            //    });

            if (Program.ARGS.Length == 1)
            {
                LoadingTaskMan.DoLoadingTask("STANDALONE_MODEL_LOAD",
                "Loading model...", progress =>
                {
                    LoadModel(Program.ARGS[0]);
                });
            }

            

        }

        static string GetParentDirectory(string path)
        {
            var folder = new System.IO.FileInfo(path).DirectoryName;

            var lastSlashInFolder = folder.LastIndexOf("\\");

            return folder.Substring(0, lastSlashInFolder);
        }

        static void SetParentFolderAsInterroot(string path)
        {
            var interroot = GetParentDirectory(path);

            if (File.Exists(Utils.Frankenpath(interroot, "DARKSOULS.exe")))
            {
                GameDataManager.Init(GameDataManager.GameTypes.DS1, interroot);
            }
            else if (File.Exists(Utils.Frankenpath(interroot, "DarkSoulsRemastered.exe")))
            {
                GameDataManager.Init(GameDataManager.GameTypes.DS1R, interroot);
            }
            else if (File.Exists(Utils.Frankenpath(interroot, "DarkSoulsIII.exe")))
            {
                GameDataManager.Init(GameDataManager.GameTypes.DS3, interroot);
            }
            else if (interroot.ToLower().EndsWith("dvdroot_ps4"))
            {
                GameDataManager.Init(GameDataManager.GameTypes.BB, interroot);
            }
            else if (File.Exists(Utils.Frankenpath(interroot, "sekiro.exe")))
            {
                GameDataManager.Init(GameDataManager.GameTypes.SDT, interroot);
            }
            else if (File.Exists(Utils.Frankenpath(interroot, "DarkSoulsII.exe"))
                && File.Exists(Utils.Frankenpath(interroot, "steam_api64.dll")))
            {
                GameDataManager.Init(GameDataManager.GameTypes.DS2SOTFS, interroot);
            }
            else if (File.Exists(Utils.Frankenpath(interroot, "DarkSoulsII.exe"))
                && File.Exists(Utils.Frankenpath(interroot, "steam_api.dll")))
            {
                GameDataManager.Init(GameDataManager.GameTypes.DS2, interroot);
            }
            else if (File.Exists(Utils.Frankenpath(interroot, "EBOOT.bin"))
               && interroot.ToUpper().EndsWith("USRDIR"))
            {
                GameDataManager.Init(GameDataManager.GameTypes.DES, interroot);
            }
        }

        static void LoadModel(string modelPath)
        {
            var shortPath = Utils.GetShortIngameFileName(modelPath).ToLower();

            bool properFile = false;

            if (shortPath.StartsWith("c"))
            {
                SetParentFolderAsInterroot(modelPath);
                StandaloneSelectedModel = GameDataManager.LoadCharacter(shortPath);
                StandaloneSelectedModel.LoadedWithGameType = GameDataManager.GameType;
                StandaloneSelectedModel.LoadedWithInterroot = GameDataManager.InterrootPath;
            }
            else if (shortPath.StartsWith("o"))
            {
                SetParentFolderAsInterroot(modelPath);
                StandaloneSelectedModel = GameDataManager.LoadObject(shortPath);
                StandaloneSelectedModel.LoadedWithGameType = GameDataManager.GameType;
                StandaloneSelectedModel.LoadedWithInterroot = GameDataManager.InterrootPath;
            }
            else
            {

            }
        }

        public Main()
        {
            Window.Title = "MVDX2";

            ExeDirectory = new FileInfo(typeof(Main).Assembly.Location).DirectoryName;

            graphics = new GraphicsDeviceManager(this);
            graphics.DeviceCreated += Graphics_DeviceCreated;
            graphics.DeviceReset += Graphics_DeviceReset;

            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromTicks(166667);
            // Setting this max higher allows it to skip frames instead of do slow motion.
            MaxElapsedTime = TimeSpan.FromSeconds(0.5);

            //IsFixedTimeStep = false;
            graphics.SynchronizeWithVerticalRetrace = GFX.Display.Vsync;
            graphics.IsFullScreen = GFX.Display.Fullscreen;
            graphics.PreferMultiSampling = GFX.Display.SimpleMSAA;
            graphics.PreferredBackBufferWidth = GFX.Display.Width;
            graphics.PreferredBackBufferHeight = GFX.Display.Height;
            if (!GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.HiDef))
            {
                System.Windows.Forms.MessageBox.Show("MonoGame is detecting your GPU as too " +
                    "low-end and refusing to enter the non-mobile Graphics Profile, " +
                    "which is needed for the model viewer. The app will likely crash now.");

                graphics.GraphicsProfile = GraphicsProfile.Reach;
            }
            else
            {
                graphics.GraphicsProfile = GraphicsProfile.HiDef;
            }

            graphics.PreferredBackBufferFormat = GFX.BackBufferFormat;

            graphics.ApplyChanges();

            Window.AllowUserResizing = true;

            Window.ClientSizeChanged += Window_ClientSizeChanged;

            GFX.Display.SetFromDisplayMode(GraphicsAdapter.DefaultAdapter.CurrentDisplayMode);

            //GFX.Device.Viewport = new Viewport(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            RequestViewportRenderTargetResolutionChange = true;
        }

        public void RebuildRenderTarget()
        {
            if (TimeBeforeNextRenderTargetUpdate <= 0)
            {
                SceneRenderTarget?.Dispose();
                GC.Collect();
                SceneRenderTarget = new RenderTarget2D(GFX.Device, ClientBounds.Width * GFX.SSAA,
                       ClientBounds.Height * GFX.SSAA, true, SurfaceFormat.Vector4, DepthFormat.Depth24);

                TimeBeforeNextRenderTargetUpdate = TimeBeforeNextRenderTargetUpdate_Max;

                RequestViewportRenderTargetResolutionChange = false;

                GFX.EffectiveSSAA = GFX.SSAA;
            }
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            CFG.Save();

            //TAE_EDITOR.SaveConfig();

            //TaeEditor.TaeSoundManager.DisposeAll();

            base.OnExiting(sender, args);
        }

        private void Graphics_DeviceCreated(object sender, System.EventArgs e)
        {
            GFX.Device = GraphicsDevice;
        }

        private void Graphics_DeviceReset(object sender, System.EventArgs e)
        {
            GFX.Device = GraphicsDevice;
        }

        protected override void Initialize()
        {
            try
            {
                var winForm = (Form)Control.FromHandle(Window.Handle);
                winForm.AllowDrop = true;
                winForm.DragEnter += GameWindowForm_DragEnter;
                winForm.DragDrop += GameWindowForm_DragDrop;


                IsMouseVisible = true;

                DEFAULT_TEXTURE_DIFFUSE = new Texture2D(GraphicsDevice, 1, 1);
                DEFAULT_TEXTURE_DIFFUSE.SetData(new Color[] { new Color(1.0f, 1.0f, 1.0f) });

                DEFAULT_TEXTURE_SPECULAR = new Texture2D(GraphicsDevice, 1, 1);
                DEFAULT_TEXTURE_SPECULAR.SetData(new Color[] { new Color(1.0f, 1.0f, 1.0f) });

                DEFAULT_TEXTURE_NORMAL = new Texture2D(GraphicsDevice, 1, 1);
                DEFAULT_TEXTURE_NORMAL.SetData(new Color[] { new Color(0.5f, 0.5f, 1.0f) });

                DEFAULT_TEXTURE_EMISSIVE = new Texture2D(GraphicsDevice, 1, 1);
                DEFAULT_TEXTURE_EMISSIVE.SetData(new Color[] { Color.Black });

                DEFAULT_TEXTURE_MISSING = Content.Load<Texture2D>(DEFAULT_TEXTURE_MISSING_NAME);

                DEFAULT_TEXTURE_MISSING_CUBE = new TextureCube(GraphicsDevice, 1, false, SurfaceFormat.Color);
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.PositiveX, new Color[] { Color.Fuchsia });
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.PositiveY, new Color[] { Color.Fuchsia });
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.PositiveZ, new Color[] { Color.Fuchsia });
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.NegativeX, new Color[] { Color.Fuchsia });
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.NegativeY, new Color[] { Color.Fuchsia });
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.NegativeZ, new Color[] { Color.Fuchsia });

                GFX.Device = GraphicsDevice;

                base.Initialize();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Error occurred while initializing (please report):\n\n{ex.ToString()}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
        }

        private void GameWindowForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] modelFiles = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            if (modelFiles != null && modelFiles.Length >= 1)
            {
                LoadingTaskMan.DoLoadingTask("STANDALONE_MODEL_DROP",
                "Loading dropped model...", progress =>
                {
                    StandaloneSelectedModel = null;
                    Scene.ClearScene();
                    LoadModel(modelFiles[0]);
                });

                
            }

            

            //TAE_EDITOR.

            //LoadDragDroppedFiles(modelFiles.ToDictionary(f => f, f => File.ReadAllBytes(f)));
        }

        private void GameWindowForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.All;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        protected override void LoadContent()
        {
            ContentServiceProvider = Content.ServiceProvider;
            CM = Content;

            GFX.Init(Content);
            DBG.LoadContent(Content);
            //InterrootLoader.OnLoadError += InterrootLoader_OnLoadError;

            DBG.CreateDebugPrimitives();

            GFX.World.ResetCameraLocation();

            //DbgMenuItem.Init();

            UpdateMemoryUsage();

            CFG.AttemptLoadOrDefault();

            TAE_EDITOR_FONT = Content.Load<SpriteFont>($@"{Main.ExeDirectory}\Content\Fonts\DbgMenuFontSmall");
            TAE_EDITOR_FONT_SMALL = Content.Load<SpriteFont>($@"{Main.ExeDirectory}\Content\Fonts\DbgMenuFontSmaller");
            TAE_EDITOR_BLANK_TEX = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            TAE_EDITOR_BLANK_TEX.SetData(new Color[] { Color.White }, 0, 1);
            TAE_EDITOR_SCROLLVIEWER_ARROW = Content.Load<Texture2D>($@"{Main.ExeDirectory}\Content\Utility\TaeEditorScrollbarArrow");

            //TAE_EDITOR = new TaeEditor.TaeEditorScreen((System.Windows.Forms.Form)System.Windows.Forms.Form.FromHandle(Window.Handle));

            TaeEditorSpriteBatch = new SpriteBatch(GFX.Device);

            if (Program.ARGS.Length > 0)
            {
                //TAE_EDITOR.FileContainerName = Program.ARGS[0];

                //LoadingTaskMan.DoLoadingTask("ProgramArgsLoad", "Loading ANIBND and associated model(s)...", progress =>
                //{
                //    TAE_EDITOR.LoadCurrentFile();
                //}, disableProgressBarByDefault: true);

                //LoadDragDroppedFiles(Program.ARGS.ToDictionary(f => f, f => File.ReadAllBytes(f)));
            }

            MainFlverTonemapShader = new FlverTonemapShader(Content.Load<Effect>($@"Content\Shaders\FlverTonemapShader"));


            InitStandaloneViewer();
        }

        private void InterrootLoader_OnLoadError(string contentName, string error)
        {
            Console.WriteLine($"CONTENT LOAD ERROR\nCONTENT NAME:{contentName}\nERROR:{error}");
        }

        private string GetMemoryUseString(string prefix, long MemoryUsage)
        {
            const double MEM_KB = 1024f;
            const double MEM_MB = 1024f * 1024f;
            //const double MEM_GB = 1024f * 1024f * 1024f;

            if (MemoryUsage < MEM_KB)
                return $"{prefix}{(1.0 * MemoryUsage):0} B";
            else if (MemoryUsage < MEM_MB)
                return $"{prefix}{(1.0 * MemoryUsage / MEM_KB):0.00} KB";
            else// if (MemoryUsage < MEM_GB)
                return $"{prefix}{(1.0 * MemoryUsage / MEM_MB):0.00} MB";
            //else
            //    return $"{prefix}{(1.0 * MemoryUsage / MEM_GB):0.00} GB";
        }

        private Color GetMemoryUseColor(long MemoryUsage)
        {
            const double MEM_KB = 1024f;
            const double MEM_MB = 1024f * 1024f;
            const double MEM_GB = 1024f * 1024f * 1024f;

            if (MemoryUsage < MEM_KB)
                return Color.Cyan;
            else if (MemoryUsage < MEM_MB)
                return Color.Lime;
            else if (MemoryUsage < MEM_GB)
                return Color.Yellow;
            else if (MemoryUsage < (MEM_GB * 2))
                return Color.Orange;
            else
                return Color.Red;
        }

        private void DrawMemoryUsage()
        {
            var str_managed = GetMemoryUseString("CLR Mem:  ", MemoryUsage_Managed);
            var str_unmanaged = GetMemoryUseString("RAM USE:  ", MemoryUsage_Unmanaged);

            var strSize_managed = DBG.DEBUG_FONT_SMALL.MeasureString(str_managed);
            var strSize_unmanaged = DBG.DEBUG_FONT.MeasureString(str_unmanaged);

            //DBG.DrawOutlinedText(str_managed, new Vector2(GFX.Device.Viewport.Width - 2, 
            //    GFX.Device.Viewport.Height - (strSize_managed.Y * 0.75f) - (strSize_unmanaged.Y * 0.75f)),
            //    Color.Cyan, DBG.DEBUG_FONT_SMALL, scale: 0.75f, scaleOrigin: new Vector2(strSize_managed.X, 0));
            GFX.SpriteBatchBeginForText();
            DBG.DrawOutlinedText(str_unmanaged, new Vector2(GFX.Device.Viewport.Width - 6,
                GFX.Device.Viewport.Height),
                GetMemoryUseColor(MemoryUsage_Unmanaged), DBG.DEBUG_FONT, scale: 1, scaleOrigin: strSize_unmanaged);
            GFX.SpriteBatchEnd();
        }

        private void UpdateMemoryUsage()
        {
            using (var proc = Process.GetCurrentProcess())
            {
                MemoryUsage_Unmanaged = proc.PrivateMemorySize64;
            }
            MemoryUsage_Managed = GC.GetTotalMemory(forceFullCollection: false);
        }

        /// <summary>Returns true if the current application has focus, false otherwise</summary>
        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var procId = Process.GetCurrentProcess().Id;
            int activeProcId;
            GetWindowThreadProcessId(activatedHandle, out activeProcId);

            return activeProcId == procId;
        }


        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);


        protected override void Update(GameTime gameTime)
        {
            DELTA_UPDATE = (float)gameTime.ElapsedGameTime.TotalSeconds;//(float)(Math.Max(gameTime.ElapsedGameTime.TotalMilliseconds, 10) / 1000.0);

            if (!FIXED_TIME_STEP && GFX.AverageFPS >= 200)
            {
                DELTA_UPDATE_ROUNDED = (float)(Math.Max(gameTime.ElapsedGameTime.TotalMilliseconds, 10) / 1000.0);
            }
            else
            {
                DELTA_UPDATE_ROUNDED = DELTA_UPDATE;
            }



            Active = IsActive && ApplicationIsActivated();

            TargetElapsedTime = Active ? TimeSpan.FromTicks(166667) : TimeSpan.FromSeconds(0.25);

            IsLoadingTaskRunning = LoadingTaskMan.AnyTasksRunning();

            Scene.UpdateAnimation();

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            LoadingTaskMan.Update(elapsed);

            IsFixedTimeStep = FIXED_TIME_STEP;

            if (DBG.EnableMenu)
            {
                DbgMenuItem.UpdateInput(elapsed);
                DbgMenuItem.UICursorBlinkUpdate(elapsed);
            }

            //if (DbgMenuItem.MenuOpenState != DbgMenuOpenState.Open)
            //{
            //    // Only update input if debug menu isnt fully open.
            //    GFX.World.UpdateInput(this, gameTime);
            //}

            GFX.World.UpdateMatrices(GraphicsDevice);

            GFX.World.CameraPositionDefault.Position = Vector3.Zero;

            GFX.World.CameraOrigin.Position = new Vector3(GFX.World.CameraPositionDefault.Position.X,
                GFX.World.CameraOrigin.Position.Y, GFX.World.CameraPositionDefault.Position.Z);

            if (DBG.DbgPrim_Grid != null)
                DBG.DbgPrim_Grid.Transform = GFX.World.CameraPositionDefault;

            if (REQUEST_EXIT)
                Exit();

            MemoryUsageCheckTimer += elapsed;
            if (MemoryUsageCheckTimer >= MemoryUsageCheckInterval)
            {
                MemoryUsageCheckTimer = 0;
                UpdateMemoryUsage();
            }


            // BELOW IS TAE EDITOR STUFF

            //if (IsLoadingTaskRunning != prevFrameWasLoadingTaskRunning)
            //{
            //    TAE_EDITOR.GameWindowAsForm.Invoke(new Action(() =>
            //    {
            //        if (IsLoadingTaskRunning)
            //        {
            //            Mouse.SetCursor(MouseCursor.Wait);
            //        }

            //        foreach (Control c in TAE_EDITOR.GameWindowAsForm.Controls)
            //        {
            //            c.Enabled = !IsLoadingTaskRunning;
            //        }

            //        if (!IsLoadingTaskRunning)
            //        {
            //            TAE_EDITOR.RefocusInspectorToPreventBeepWhenYouHitSpace();
            //        }


            //    }));
            //}

            //if (!IsLoadingTaskRunning)
            //{
            //    //MeasuredElapsedTime = UpdateStopwatch.Elapsed;
            //    //MeasuredTotalTime = MeasuredTotalTime.Add(MeasuredElapsedTime);

            //    //UpdateStopwatch.Restart();

            //    if (!TAE_EDITOR.Rect.Contains(TAE_EDITOR.Input.MousePositionPoint))
            //        TAE_EDITOR.Input.CursorType = TaeEditor.MouseCursorType.Arrow;

            //    if (Active)
            //        TAE_EDITOR.Update();
            //    else
            //        TAE_EDITOR.Input.CursorType = TaeEditor.MouseCursorType.Arrow;

            //    if (!string.IsNullOrWhiteSpace(TAE_EDITOR.FileContainerName))
            //        Window.Title = $"{System.IO.Path.GetFileName(TAE_EDITOR.FileContainerName)}" +
            //            $"{(TAE_EDITOR.IsModified ? "*" : "")}" +
            //            $"{(TAE_EDITOR.IsReadOnlyFileMode ? " !READ ONLY!" : "")}" +
            //            $" - DS Anim Studio {VERSION}";
            //    else
            //        Window.Title = $"DS Anim Studio {VERSION}";
            //}

            prevFrameWasLoadingTaskRunning = IsLoadingTaskRunning;

            UpdateStandaloneViewer();

            base.Update(gameTime);
        }

        private void InitTonemapShader()
        {

        }

        protected override void Draw(GameTime gameTime)
        {
            DELTA_DRAW = (float)gameTime.ElapsedGameTime.TotalSeconds;// (float)(Math.Max(gameTime.ElapsedGameTime.TotalMilliseconds, 10) / 1000.0);

            GFX.Device.Clear(Color.DimGray);

            if (DbgMenuItem.MenuOpenState != DbgMenuOpenState.Open)
            {
                // Only update input if debug menu isnt fully open.
                GFX.World.UpdateInput(this);
            }

            if (ClientBounds.Width > 0 && ClientBounds.Height > 0)
            {
                if (SceneRenderTarget == null)
                {
                    RebuildRenderTarget();
                    if (TimeBeforeNextRenderTargetUpdate > 0)
                        TimeBeforeNextRenderTargetUpdate -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                }
                else if (RequestViewportRenderTargetResolutionChange)
                {
                    RebuildRenderTarget();

                    if (TimeBeforeNextRenderTargetUpdate > 0)
                        TimeBeforeNextRenderTargetUpdate -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                }

                GFX.Device.SetRenderTarget(SceneRenderTarget);

                GFX.Device.Clear(Color.DimGray);

                GFX.Device.Viewport = new Viewport(0, 0, SceneRenderTarget.Width, SceneRenderTarget.Height);

                GFX.LastViewport = new Viewport(ClientBounds);

                //TaeInterop.TaeViewportDrawPre(gameTime);
                GFX.DrawScene3D();

                //if (!DBG.DbgPrimXRay)
                //    GFX.DrawSceneOver3D();

                if (DBG.DbgPrimXRay)
                    GFX.Device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);

                GFX.DrawSceneOver3D();

                GFX.Device.SetRenderTarget(null);

                GFX.Device.Clear(Color.DimGray);

                GFX.Device.Viewport = new Viewport(ClientBounds);

                InitTonemapShader();
                GFX.SpriteBatchBegin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

                if (GFX.UseTonemap && !GFX.IsInDebugShadingMode)
                    MainFlverTonemapShader.Effect.CurrentTechnique.Passes[0].Apply();

                GFX.SpriteBatch.Draw(SceneRenderTarget,
                    new Rectangle(0, 0, ClientBounds.Width, ClientBounds.Height), Color.White);
                GFX.SpriteBatchEnd();

                //try
                //{
                //    using (var renderTarget3DScene = new RenderTarget2D(GFX.Device, TAE_EDITOR.ModelViewerBounds.Width * GFX.SSAA,
                //   TAE_EDITOR.ModelViewerBounds.Height * GFX.SSAA, true, SurfaceFormat.Rgba1010102, DepthFormat.Depth24))
                //    {
                //        GFX.Device.SetRenderTarget(renderTarget3DScene);

                //        GFX.Device.Clear(new Color(80, 80, 80, 255));

                //        GFX.Device.Viewport = new Viewport(0, 0, TAE_EDITOR.ModelViewerBounds.Width * GFX.SSAA, TAE_EDITOR.ModelViewerBounds.Height * GFX.SSAA);
                //        TaeInterop.TaeViewportDrawPre(gameTime);
                //        GFX.DrawScene3D(gameTime);

                //        GFX.Device.SetRenderTarget(null);

                //        GFX.Device.Clear(new Color(80, 80, 80, 255));

                //        GFX.Device.Viewport = new Viewport(TAE_EDITOR.ModelViewerBounds);

                //        InitTonemapShader();
                //        GFX.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                //        //MainFlverTonemapShader.Effect.CurrentTechnique.Passes[0].Apply();
                //        GFX.SpriteBatch.Draw(renderTarget3DScene,
                //            new Rectangle(0, 0, TAE_EDITOR.ModelViewerBounds.Width, TAE_EDITOR.ModelViewerBounds.Height), Color.White);
                //        GFX.SpriteBatch.End();
                //    }
                //}
                //catch (SharpDX.SharpDXException ex)
                //{
                //    GFX.Device.Viewport = new Viewport(TAE_EDITOR.ModelViewerBounds);
                //    GFX.Device.Clear(new Color(80, 80, 80, 255));

                //    GFX.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                //    //MainFlverTonemapShader.Effect.CurrentTechnique.Passes[0].Apply();
                //    var errorStr = $"FAILED TO RENDER VIEWPORT AT {(Main.TAE_EDITOR.ModelViewerBounds.Width * GFX.SSAA)}x{(Main.TAE_EDITOR.ModelViewerBounds.Height * GFX.SSAA)} Resolution";
                //    var errorStrPos = (Vector2.One * new Vector2(TAE_EDITOR.ModelViewerBounds.Width, TAE_EDITOR.ModelViewerBounds.Height) / 2.0f);

                //    errorStrPos -= DBG.DEBUG_FONT.MeasureString(errorStr) / 2.0f;

                //    GFX.SpriteBatch.DrawString(DBG.DEBUG_FONT, errorStr, errorStrPos - Vector2.One, Color.Black);
                //    GFX.SpriteBatch.DrawString(DBG.DEBUG_FONT, errorStr, errorStrPos, Color.Red);
                //    GFX.SpriteBatch.End();
                //}

            }

            GFX.Device.Viewport = new Viewport(ClientBounds);
            //DBG.DrawPrimitiveNames(gameTime);

            //if (DBG.DbgPrimXRay)
            //    GFX.DrawSceneOver3D();

            GFX.DrawSceneGUI();

            //TAE_EDITOR?.Graph?.ViewportInteractor?.DrawDebug();

            DrawMemoryUsage();

            LoadingTaskMan.DrawAllTasks();

            GFX.Device.Viewport = new Viewport(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);

            //TAE_EDITOR.Rect = new Rectangle(2, 0, GraphicsDevice.Viewport.Width - 4, GraphicsDevice.Viewport.Height - 2);

            //TAE_EDITOR.Draw(GraphicsDevice, TaeEditorSpriteBatch,
            //    TAE_EDITOR_BLANK_TEX, TAE_EDITOR_FONT, 
            //    (float)gameTime.ElapsedGameTime.TotalSeconds, TAE_EDITOR_FONT_SMALL,
            //    TAE_EDITOR_SCROLLVIEWER_ARROW);

            //if (IsLoadingTaskRunning)
            //{
            //    TAE_EDITOR.DrawDimmingRect(GraphicsDevice, TaeEditorSpriteBatch, TAE_EDITOR_BLANK_TEX);
            //}

            DrawStandaloneViewer();
        }
    }
}
