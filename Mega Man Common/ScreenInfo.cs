using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using MegaMan.Common.Geometry;

namespace MegaMan.Common
{
    public struct TeleportInfo
    {
        public Point From;
        public Point To;
        public string TargetScreen;
    }
    
    public class ScreenInfo
    {
        #region Properties
        public List<SceneCommandInfo> Commands { get; set; }
        public List<ScreenLayerInfo> Layers { get; private set; }
        public List<BlockPatternInfo> BlockPatterns { get; private set; }
        public List<TeleportInfo> Teleports { get; private set; } 
        
        public string Name { get; set; }
        public int Width { get { return Layers[0].Tiles.Width; } }
        public int Height { get { return Layers[0].Tiles.Height; } }
        public int PixelWidth { get { return Layers[0].Tiles.PixelWidth; } }
        public int PixelHeight { get { return Layers[0].Tiles.PixelHeight; } }
        public Tileset Tileset { get; set; }

        #endregion Properties

        public ScreenInfo(string name, Tileset tileset)
        {
            this.Name = name;
            this.Tileset = tileset;

            BlockPatterns = new List<BlockPatternInfo>();
            Teleports = new List<TeleportInfo>();
            Layers = new List<ScreenLayerInfo>();
            Commands = new List<SceneCommandInfo>();
        }

        public void Save(XmlTextWriter writer, FilePath stagePath)
        {
            writer.WriteStartElement("Screen");
            writer.WriteAttributeString("id", Name);

            foreach (var command in Commands)
            {
                command.Save(writer);
            }

            foreach (var info in Layers[0].Entities)
            {
                info.Save(writer);
            }

            foreach (var layer in Layers.Skip(1))
            {
                layer.Save(writer);
            }

            foreach (BlockPatternInfo pattern in BlockPatterns)
            {
                //pattern.Save(writer);
            }

            foreach (TeleportInfo teleport in Teleports)
            {
                writer.WriteStartElement("Teleport");
                writer.WriteAttributeString("from_x", teleport.From.X.ToString());
                writer.WriteAttributeString("from_y", teleport.From.Y.ToString());
                writer.WriteAttributeString("to_screen", teleport.TargetScreen);
                writer.WriteAttributeString("to_x", teleport.To.X.ToString());
                writer.WriteAttributeString("to_y", teleport.To.Y.ToString());
                writer.WriteEndElement();
            }

            writer.WriteEndElement();

            foreach (var layer in Layers)
            {
                layer.Tiles.Save(Path.Combine(stagePath.Absolute, Name + ".scn"));
            }
        }
    }
}
