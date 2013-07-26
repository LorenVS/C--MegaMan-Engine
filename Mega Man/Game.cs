﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using MegaMan.Common;
using MegaMan.Common.Geometry;
using MegaMan.IO.Xml;
using MegaMan.Engine.Entities;

namespace MegaMan.Engine
{
    // These args, and the event, are used to forcibly resize
    // the window and screen when the game is loaded, since
    // the game can specify its size in pixels.
    public class ScreenSizeChangedEventArgs : EventArgs
    {
        public int PixelsAcross { get; private set; }
        public int PixelsDown { get; private set; }

        public ScreenSizeChangedEventArgs(int across, int down)
        {
            PixelsAcross = across;
            PixelsDown = down;
        }
    }

    public class Game
    {
        public static Game CurrentGame { get; private set; }

        private Project project;
        private StageFactory stageFactory;

        private string currentPath;
        private Stack<IGameplayContainer> handlerStack;

        private GameEntitySource _entitySource;
        private GameEntityPool _entityPool;
        private GameTilePropertiesSource _tileProperties;
        private IEntityRespawnTracker _respawnTracker;

        public int PixelsAcross { get; private set; }
        public int PixelsDown { get; private set; }
        public float Gravity { get; private set; }
        public bool GravityFlip { get; set; }

        public ITilePropertiesSource TileProperties { get { return _tileProperties; } }

        public string Name
        {
            get
            {
                if (project == null) return "Mega Man";
                if (string.IsNullOrWhiteSpace(project.Name)) return "Untitled Fan Game";
                return project.Name;
            }
        }

        public string BasePath { get; private set; }

        public Player Player { get; private set; }

        public static event EventHandler<ScreenSizeChangedEventArgs> ScreenSizeChanged;

        public static void Load(string path, List<string> pathArgs = null)
        {
            Engine.Instance.Begin();
            if (CurrentGame != null)
            {
                CurrentGame.Unload();
            }
            CurrentGame = new Game();
            CurrentGame.LoadFile(path, pathArgs);
        }

        public void Unload()
        {
            Engine.Instance.Stop();
            while (handlerStack.Count > 0)
            {
                handlerStack.Pop().StopHandler();
            }
            _entitySource.Unload();
            Engine.Instance.UnloadAudio();
            FontSystem.Unload();
            HealthMeter.Unload();
            Scene.Unload();
            Menu.Unload();
            PaletteSystem.Unload();
            EffectParser.Unload();
            CurrentGame = null;
        }

        public void Reset()
        {
            Unload();
            Load(currentPath);
        }

        private Game()
        {
            Gravity = 0.25f;
            GravityFlip = false;
            handlerStack = new Stack<IGameplayContainer>();
            _entitySource = new GameEntitySource();
            _entityPool = new GameEntityPool(_entitySource);
            _tileProperties = new GameTilePropertiesSource();
            _respawnTracker = new GameEntityRespawnTracker();
        }

        private void LoadFile(string path, List<string> pathArgs = null)
        {
            var projectReader = new ProjectXmlReader();

            project = projectReader.FromXml(path);

            BasePath = project.BaseDir;

            PixelsDown = project.ScreenHeight;
            PixelsAcross = project.ScreenWidth;

            if (ScreenSizeChanged != null)
            {
                ScreenSizeChangedEventArgs args = new ScreenSizeChangedEventArgs(PixelsAcross, PixelsDown);
                ScreenSizeChanged(this, args);
            }

            if (project.MusicNSF != null) Engine.Instance.SoundSystem.LoadMusicNSF(project.MusicNSF.Absolute);
            if (project.EffectsNSF != null) Engine.Instance.SoundSystem.LoadSfxNSF(project.EffectsNSF.Absolute);

            stageFactory = new StageFactory(_entityPool, _respawnTracker);
            foreach (var stageInfo in project.Stages)
            {
                stageFactory.Load(stageInfo);
            }

            var includeReader = new IncludeFileXmlReader();

            foreach (string includePath in project.Includes)
            {
                string includefile = Path.Combine(BasePath, includePath);
                IncludeXmlFile(includefile);
                includeReader.LoadIncludedFile(project, includefile);
            }

            Engine.Instance.SoundSystem.LoadEffectsFromInfo(project.Sounds);
            Scene.LoadScenes(project.Scenes);
            Menu.LoadMenus(project.Menus);
            FontSystem.Load(project.Fonts);
            PaletteSystem.LoadPalettes(project.Palettes);

            currentPath = path;

            if (pathArgs != null && pathArgs.Any())
            {
                ProcessCommandLineArgs(pathArgs);
            }
            else if (project.StartHandler != null)
            {
                StartHandler(project.StartHandler);
            }
            else
            {
                throw new GameRunException("The game file loaded correctly, but it failed to specify a starting point!");
            }

            Player = new Player();
        }

        private void ProcessCommandLineArgs(List<string> pathArgs)
        {
            var start = pathArgs[0];

            var parts = start.Split('\\');
            if (parts.Length != 2)
            {
                throw new GameRunException("The starting point given by command line argument was invalid.");
            }
            var name = parts[1];
            switch (parts[0].ToUpper())
            {
                case "SCENE":
                    StartScene(new HandlerTransfer() { Name = name, Mode = HandlerMode.Next });
                    break;

                case "STAGE":
                    var screen = (pathArgs.Count > 1) ? pathArgs[1] : null;
                    Point? startPos = null;
                    if (pathArgs.Count > 2)
                    {
                        var point = pathArgs[2];
                        var coords = point.Split(',');
                        startPos = new Point(int.Parse(coords[0]), int.Parse(coords[1]));
                    }
                    StartStage(name, screen, startPos);
                    break;

                case "MENU":
                    StartMenu(new HandlerTransfer() { Name = name, Mode = HandlerMode.Next });
                    break;

                default:
                    throw new GameRunException("The starting point given by command line argument was invalid.");
            }
        }

        private void IncludeXmlFile(string path)
        {
            try
            {
                XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
                foreach (XElement element in document.Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        case "Entities":
                            _entitySource.LoadEntities(element);
                            _tileProperties.LoadProperties(element);
                            break;

                        case "Functions":
                            EffectParser.LoadEffectsList(element);
                            break;

                        case "Sounds":
                        case "Scenes":
                        case "Scene":
                        case "Menus":
                        case "Menu":
                        case "Fonts":
                        case "Palettes":
                            break;

                        default:
                            throw new GameXmlException(element, string.Format("Unrecognized XML type: \"{0}\"", element.Name.LocalName));
                    }
                }
            }
            catch (GameXmlException ex)
            {
                ex.File = path;
                throw;
            }
        }

        public void ProcessHandler(HandlerTransfer handler)
        {
            switch (handler.Mode)
            {
                case HandlerMode.Next:
                    EndHandler(handler);
                    break;

                case HandlerMode.Push:
                    if (handler.Fade)
                    {
                        if (handlerStack.Count > 0 && handler.Pause)
                        {
                            var top = handlerStack.Peek();
                            top.PauseHandler();
                        }

                        Engine.Instance.FadeTransition(() =>
                        {
                            if (handlerStack.Count > 0 && handler.Pause)
                            {
                                var top = handlerStack.Peek();
                                top.StopDrawing();
                            }
                            StartHandler(handler);
                            handlerStack.Peek().PauseHandler();
                        },
                        () => {
                            handlerStack.Peek().ResumeHandler();
                        });
                    }
                    else
                    {
                        if (handlerStack.Count > 0 && handler.Pause)
                        {
                            var top = handlerStack.Peek();
                            top.PauseHandler();
                            top.StopDrawing();
                        }
                        StartHandler(handler);
                    }
                    break;

                case HandlerMode.Pop:
                    if (handler.Fade)
                    {
                        IGameplayContainer top = null;
                        if (handlerStack.Count > 0)
                        {
                            top = handlerStack.Pop();
                            top.PauseHandler();
                            top.End -= ProcessHandler;
                        }
                        Engine.Instance.FadeTransition(() =>
                        {
                            if (top != null) top.StopHandler();
                            if (handlerStack.Count > 0)
                            {
                                handlerStack.Peek().StartDrawing();
                            }
                        },
                        () =>
                        {
                            if (handlerStack.Count > 0)
                            {
                                handlerStack.Peek().ResumeHandler();
                            }
                        });
                    }
                    else
                    {
                        if (handlerStack.Count > 0)
                        {
                            var top = handlerStack.Pop();
                            top.StopHandler();
                            top.End -= ProcessHandler;
                            if (handlerStack.Count > 0)
                            {
                                handlerStack.Peek().ResumeHandler();
                            }
                        }
                    }
                    break;
            }
        }

        private void EndHandler(HandlerTransfer nextHandler)
        {
            foreach (var handler in handlerStack)
            {
                handler.End -= ProcessHandler;
            }

            if (nextHandler.Fade)
            {
                Engine.Instance.FadeTransition(() =>
                {
                    while (handlerStack.Count > 0)
                    {
                        handlerStack.Pop().StopHandler();
                    }
                    StartHandler(nextHandler);
                });
            }
            else
            {
                while (handlerStack.Count > 0)
                {
                    handlerStack.Pop().StopHandler();
                }
                StartHandler(nextHandler);
            }
        }

        private void StartHandler(HandlerTransfer handler)
        {
            if (handler != null)
            {
                switch (handler.Type)
                {
                    case HandlerType.Scene:
                        StartScene(handler);
                        break;

                    case HandlerType.Stage:
                        StartStage(handler.Name);
                        break;

                    case HandlerType.Menu:
                        StartMenu(handler);
                        break;
                }
            }
        }

        private void StartMenu(HandlerTransfer handler)
        {
            var menu = Menu.Get(handler.Name);
            menu.End += ProcessHandler;
            menu.StartHandler(_entityPool);
            handlerStack.Push(menu);
        }

        private void StartScene(HandlerTransfer handler)
        {
            var scene = Scene.Get(handler.Name);
            scene.End += ProcessHandler;
            scene.StartHandler(_entityPool);
            handlerStack.Push(scene);
        }

        private void StartStage(string name, string screen = null, Point? startPosition = null)
        {
            var stage = stageFactory.Get(name);

            if (screen != null && startPosition != null)
            {
                stage.SetTestingStartPosition(screen, startPosition.Value);
            }

            handlerStack.Push(stage);
            stage.End += ProcessHandler;

            stage.StartHandler(_entityPool);
        }

        #region Debug Menu

        public void DebugEmptyHealth()
        {
            if (handlerStack.Count == 0) return;

            var map = handlerStack.Peek() as StageHandler;
            if (map != null)
            {
                map.Player.SendMessage(new DamageMessage(null, float.PositiveInfinity));
            }
        }

        public void DebugFillHealth()
        {
            if (handlerStack.Count == 0) return;

            var map = handlerStack.Peek() as StageHandler;
            if (map != null)
            {
                map.Player.SendMessage(new HealMessage(null, float.PositiveInfinity));
            }
        }

        public void DebugEmptyWeapon()
        {
            if (handlerStack.Count == 0) return;

            var map = handlerStack.Peek() as StageHandler;
            if (map != null)
            {
                var weaponComponent = map.Player.GetComponent<WeaponComponent>();
                if (weaponComponent != null)
                {
                    weaponComponent.AddAmmo(-1 * weaponComponent.Ammo(weaponComponent.CurrentWeapon));
                }
            }
        }

        public void DebugFillWeapon()
        {
            if (handlerStack.Count == 0) return;

            var map = handlerStack.Peek() as StageHandler;
            if (map != null)
            {
                var weaponComponent = map.Player.GetComponent<WeaponComponent>();
                if (weaponComponent != null)
                {
                    weaponComponent.AddAmmo(weaponComponent.MaxAmmo(weaponComponent.CurrentWeapon));
                }
            }
        }

        public static int DebugEntitiesAlive()
        {
            if (CurrentGame != null)
                return CurrentGame._entityPool.GetTotalAlive();
            else
                return 0;
        }

        public static int XmlNumAlive(string name)
        {
            if (CurrentGame != null)
                return CurrentGame._entityPool.GetNumberAlive(name);
            else
                return 0;
        }
        #endregion
    }
}
