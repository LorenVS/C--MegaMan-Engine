﻿using MegaMan.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace MegaMan.Engine.Entities
{
    class GameEntitySource : IEntitySource
    {
        private readonly Dictionary<string, GameEntity> entities = new Dictionary<string, GameEntity>();
        private readonly Dictionary<string, TileProperties> entityProperties = new Dictionary<string, TileProperties>();

        public GameEntitySource()
        {
            entityProperties["Default"] = TileProperties.Default;
        }

        public GameEntity GetOriginalEntity(string name)
        {
            if (!entities.ContainsKey(name)) throw new GameRunException("Someone requested an entity named \"" + name + "\", but I couldn't find it!\n" +
                "You need to make sure it's defined in one of the included XML files.");

            return entities[name];
        }

        public TileProperties GetProperties(string name)
        {
            if (entityProperties.ContainsKey(name)) return entityProperties[name];
            return TileProperties.Default;
        }

        public void LoadEntities(XElement doc)
        {
            // properties
            XElement propHead = doc.Element("Properties");
            if (propHead != null)
            {
                foreach (XElement propNode in propHead.Elements("Properties"))
                {
                    TileProperties p = new TileProperties(propNode);
                    entityProperties[p.Name] = p;
                }
            }

            foreach (XElement entity in doc.Elements("Entity"))
            {
                LoadEntity(entity);
            }
        }

        private void LoadEntity(XElement xml)
        {
            GameEntity entity = new GameEntity();
            string name = xml.RequireAttribute("name").Value;

            if (entities.ContainsKey(name)) throw new GameXmlException(xml, "You have defined two entities both named \"" + name + "\".");

            entity.Name = name;
            entity.MaxAlive = xml.TryAttribute<int>("limit", 50);

            SpriteComponent spritecomp = null;
            PositionComponent poscomp = null;
            StateComponent statecomp = new StateComponent();
            entity.AddComponent(statecomp);

            try
            {
                foreach (XElement xmlComp in xml.Elements())
                {
                    switch (xmlComp.Name.LocalName)
                    {
                        case "Tilesheet":
                            if (spritecomp == null)
                            {
                                spritecomp = new SpriteComponent();
                                entity.AddComponent(spritecomp);
                            }
                            if (poscomp == null)
                            {
                                poscomp = new PositionComponent();
                                entity.AddComponent(poscomp);
                            }
                            spritecomp.LoadTilesheet(xmlComp);
                            break;

                        case "Trigger":
                            statecomp.LoadStateTrigger(xmlComp);
                            break;

                        case "Sprite":
                            if (spritecomp == null)
                            {
                                spritecomp = new SpriteComponent();
                                entity.AddComponent(spritecomp);
                            }
                            if (poscomp == null)
                            {
                                poscomp = new PositionComponent();
                                entity.AddComponent(poscomp);
                            }
                            spritecomp.LoadXml(xmlComp);
                            break;

                        case "Position":
                            if (poscomp == null)
                            {
                                poscomp = new PositionComponent();
                                entity.AddComponent(poscomp);
                            }
                            poscomp.LoadXml(xmlComp);
                            break;

                        case "Death":
                            entity.OnDeath += EffectParser.LoadTriggerEffect(xmlComp);
                            break;

                        case "GravityFlip":
                            entity.GravityFlip = xmlComp.GetValue<bool>();
                            break;

                        default:
                            entity.GetOrCreateComponent(xmlComp.Name.LocalName).LoadXml(xmlComp);
                            break;
                    }
                }
            }
            catch (GameXmlException ex)
            {
                ex.Entity = name;
                throw;
            }

            entities.Add(name, entity);
        }

        public void Unload()
        {
            entities.Clear();
        }
    }
}
