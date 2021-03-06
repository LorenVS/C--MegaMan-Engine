﻿using System;
using System.Linq;
using System.Collections.Generic;
using MegaMan.Common;
using Microsoft.Xna.Framework.Graphics;
using System.Xml.Linq;
using System.Drawing;

namespace MegaMan.Engine
{
    public class SpriteComponent : Component
    {
        private readonly Dictionary<string, SpriteGroup> _sprites;
        private System.Drawing.Image _spriteSheet;
        private string _sheetPath;

        private SpriteGroup currentSpriteGroup;

        private bool verticalFlip;

        private bool playing;
        public bool Playing
        {
            get { return playing; }
            set
            {
                playing = value;

                if (currentSpriteGroup != null)
                {
                    foreach (var sprite in currentSpriteGroup)
                    {
                        if (playing) sprite.Resume();
                        else sprite.Pause();
                    }
                }
            }
        }

        public bool Visible { get; set; }

        private PositionComponent PositionSrc { get; set; }

        public bool HorizontalFlip
        {
            set
            {
                if (currentSpriteGroup != null)
                {
                    foreach (var sprite in currentSpriteGroup)
                    {
                        sprite.HorizontalFlip = value;
                    }
                }
            }
        }

        public SpriteComponent()
        {
            _sprites = new Dictionary<string, SpriteGroup>();

            Playing = true;
            Visible = true;
        }

        public override Component Clone()
        {
            SpriteComponent copy = new SpriteComponent();

            foreach (var group in _sprites)
            {
                foreach (var spr in group.Value.NamedSprites)
                {
                    var copySprite = new Sprite(spr.Value);
                    var drawer = new XnaSpriteDrawer(copySprite);
                    drawer.SetTexture(Engine.Instance.GraphicsDevice, _sheetPath);
                    copySprite.Drawer = drawer;
                    copy.Add(group.Key, copySprite, spr.Key);
                }
            }

            copy.verticalFlip = verticalFlip;

            return copy;
        }

        public override void Start()
        {
            Parent.Container.GameThink += Update;
            Parent.Container.Draw += Instance_GameRender;
        }

        public override void Stop()
        {
            Parent.Container.GameThink -= Update;
            Parent.Container.Draw -= Instance_GameRender;
        }

        public override void Message(IGameMessage msg)
        {
            
        }

        public override void LoadXml(XElement xmlNode)
        {
            string spriteName = "Default";
            string partName = null;
            XAttribute spriteNameAttr = xmlNode.Attribute("name");
            if (spriteNameAttr != null) spriteName = spriteNameAttr.Value;
            XAttribute partAttr = xmlNode.Attribute("part");
            if (partAttr != null) partName = partAttr.Value;

            if (xmlNode.Attribute("tilesheet") != null) // explicitly specified sheet for this sprite
            {
                _sheetPath = System.IO.Path.Combine(Game.CurrentGame.BasePath, xmlNode.RequireAttribute("tilesheet").Value);
            }

            Sprite sprite = Sprite.FromXml(xmlNode);
            Add(spriteName, sprite, partName);
        }

        public static Effect ParseEffect(XElement node)
        {
            Effect action = entity => { };
            foreach (XElement prop in node.Elements())
            {
                switch (prop.Name.LocalName)
                {
                    case "Name":
                        string spritename = prop.Value;

                        action += entity =>
                        {
                            SpriteComponent spritecomp = entity.GetComponent<SpriteComponent>();
                            spritecomp.ChangeSprite(spritename);
                        };
                        break;

                    case "Playing":
                        bool play = prop.GetBool();
                        action += entity =>
                        {
                            SpriteComponent spritecomp = entity.GetComponent<SpriteComponent>();
                            spritecomp.Playing = play;
                        };
                        break;

                    case "Visible":
                        bool vis = prop.GetBool();
                        action += entity =>
                        {
                            SpriteComponent spritecomp = entity.GetComponent<SpriteComponent>();
                            spritecomp.Visible = vis;
                        };
                        break;

                    case "Palette":
                        string pal = prop.RequireAttribute("name").Value;
                        int index = prop.GetInteger("index");
                        action += entity =>
                        {
                            var palette = Palette.Get(pal);
                            if (palette != null)
                            {
                                palette.CurrentIndex = index;
                            }
                        };
                        break;
                }
            }
            return action;
        }

        public void LoadTilesheet(XElement xmlComp)
        {
            XAttribute palAttr = xmlComp.Attribute("pallete");
            string pallete = "Default";
            if (palAttr != null) pallete = palAttr.Value;
            _sheetPath = System.IO.Path.Combine(Game.CurrentGame.BasePath, xmlComp.Value);
            var sheet = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(_sheetPath);
            sheet.SetResolution(Const.Resolution, Const.Resolution);
            _spriteSheet = sheet;
        }

        private void Add(string name, Sprite sprite, string partName = null)
        {
            SpriteGroup group;
            if (_sprites.ContainsKey(name))
            {
                group = _sprites[name];
            }
            else
            {
                group = new SpriteGroup(this);
                _sprites.Add(name, group);

                if (currentSpriteGroup == null)
                {
                    currentSpriteGroup = group;
                }
            }

            group.Add(sprite, partName);

            if (group == currentSpriteGroup)
            {
                sprite.Play();
            }
        }

        /// <summary>
        /// This should get deprecated. Palettes should be changed globally
        /// by the callers knowing their names.
        /// </summary>
        public void ChangePalette(int index)
        {
            currentSpriteGroup.ChangePalette(index);
        }

        private void ChangeSprite(string name)
        {
            if (!_sprites.ContainsKey(name) || _sprites[name] == null)
            {
                throw new GameRunException(String.Format("A sprite with name {0} was not found in the entity {1}.", name, Parent.Name));
            }

            foreach (var sprite in currentSpriteGroup)
            {
                sprite.Stop();
            }

            currentSpriteGroup = _sprites[name];
            
            if (Playing)
            {
                foreach (var sprite in currentSpriteGroup)
                {
                    sprite.Play();
                }
            }
        }

        protected override void Update()
        {
            if (!Parent.Paused)
            {
                foreach (var sprite in currentSpriteGroup)
                {
                    sprite.Update();
                }
            }
        }

        public override void RegisterDependencies(Component component)
        {
            if (component is PositionComponent) PositionSrc = component as PositionComponent;
        }

        private bool evenframe = true;
        private void Instance_GameRender(GameRenderEventArgs e)
        {
            evenframe = !evenframe;
            foreach (var sprite in currentSpriteGroup)
            {
                RenderSprite(sprite, e);
            }
        }

        private void RenderSprite(Sprite currentSprite, GameRenderEventArgs e)
        {
            if (currentSprite.Layer < e.Layers.SpritesBatch.Length && (
                (currentSprite.Layer == 0 && Engine.Instance.SpritesOne) ||
                (currentSprite.Layer == 1 && Engine.Instance.SpritesTwo) ||
                (currentSprite.Layer == 2 && Engine.Instance.SpritesThree) ||
                (currentSprite.Layer == 3 && Engine.Instance.SpritesFour)
                ))
            {
                if (evenframe && Engine.Instance.Foreground)
                {
                    foreach (var meter in HealthMeter.AllMeters)
                    {
                        var bounds = new RectangleF(currentSprite.BoundBox.X, currentSprite.BoundBox.Y,
                            currentSprite.BoundBox.Width, currentSprite.BoundBox.Height);

                        bounds.Offset(-currentSprite.HotSpot.X, -currentSprite.HotSpot.Y);
                        bounds.Offset(PositionSrc.Position);
                        if (meter.Bounds.IntersectsWith(bounds))
                        {
                            Draw(currentSprite, e.Layers.ForegroundBatch, e.OpacityColor);
                            return;
                        }
                    }
                }
                Draw(currentSprite, e.Layers.SpritesBatch[currentSprite.Layer], e.OpacityColor);
            }
        }

        private void Draw(Sprite sprite, SpriteBatch batch, Microsoft.Xna.Framework.Color color)
        {
            if (PositionSrc == null) throw new InvalidOperationException("SpriteComponent has not been initialized with a position source.");
            float off_x = Parent.Screen.OffsetX;
            float off_y = Parent.Screen.OffsetY;
            if (sprite != null && Visible)
            {
                sprite.VerticalFlip = Parent.GravityFlip ? Game.CurrentGame.GravityFlip : verticalFlip;
                (sprite.Drawer as XnaSpriteDrawer).DrawXna(batch, color, PositionSrc.Position.X - off_x, PositionSrc.Position.Y - off_y);
            }
        }

        private class SpriteGroup : IEnumerable<Sprite>
        {
            private SpriteComponent _parent;

            private string _defaultKey = Guid.NewGuid().ToString();
            private Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

            public Dictionary<string, Sprite> NamedSprites
            {
                get
                {
                    return _sprites;
                }
            }

            public SpriteGroup(SpriteComponent parent)
            {
                _parent = parent;
            }

            public void Add(Sprite sprite, string partName = null)
            {
                if (partName == null)
                {
                    partName = _defaultKey;
                }

                if (_sprites.ContainsKey(partName))
                {
                    throw new GameRunException(String.Format("A sprite with the same part name already exists in the {0} entity.", _parent.Parent.Name));
                }

                _sprites.Add(partName, sprite);
            }

            public void ChangePalette(int index)
            {
                var paletteName = _sprites.Values.First().PaletteName;
                var palette = Palette.Get(paletteName);
                if (palette != null)
                {
                    palette.CurrentIndex = index;
                }
            }

            public IEnumerator<Sprite> GetEnumerator()
            {
                return _sprites.Values.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return _sprites.Values.GetEnumerator();
            }
        }
    }
}
