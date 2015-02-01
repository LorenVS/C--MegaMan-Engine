﻿using System.Collections.Generic;
using System.Linq;
using MegaMan.Common;

namespace MegaMan.Editor.Bll
{
    public interface IUndoableAction
    {
        void Execute();
        IUndoableAction Reverse();
    }

    public class TileChange
    {
        private readonly int tileX;
        private readonly int tileY;
        private readonly int oldTileId;
        private readonly int newTileId;

        public TileChange(int tx, int ty, int oldId, int newId)
        {
            tileX = tx;
            tileY = ty;
            oldTileId = oldId;
            newTileId = newId;
        }

        public TileChange Reverse()
        {
            return new TileChange(tileX, tileY, newTileId, oldTileId);
        }

        public void ApplyToScreen(ScreenDocument screen)
        {
            screen.ChangeTile(tileX, tileY, newTileId);
        }
    }

    public class DrawAction : IUndoableAction
    {
        private readonly List<TileChange> changes;
        private readonly ScreenDocument screen;
        private readonly string name;

        public DrawAction(string name, IEnumerable<TileChange> changes, ScreenDocument screen)
        {
            this.name = name;
            this.changes = new List<TileChange>(changes);
            this.screen = screen;
        }

        public override string ToString()
        {
            return name;
        }

        public void Execute()
        {
            foreach (TileChange change in changes)
            {
                change.ApplyToScreen(screen);
            }
        }

        public IUndoableAction Reverse()
        {
            List<TileChange> ch = new List<TileChange>(changes.Count);
            ch.AddRange(changes.Select(change => change.Reverse()));
            return new DrawAction(name, ch, screen);
        }
    }

    public class AddEntityAction : IUndoableAction
    {
        private readonly EntityPlacement entity;
        private readonly ScreenDocument screen;

        public AddEntityAction(EntityPlacement entity, ScreenDocument screen)
        {
            this.entity = entity;
            this.screen = screen;
        }

        public void Execute()
        {
            screen.AddEntity(entity);
        }

        public IUndoableAction Reverse()
        {
            return new RemoveEntityAction(entity, screen);
        }
    }

    public class RemoveEntityAction : IUndoableAction
    {
        private readonly EntityPlacement entity;
        private readonly ScreenDocument screen;

        public RemoveEntityAction(EntityPlacement entity, ScreenDocument screen)
        {
            this.entity = entity;
            this.screen = screen;
        }

        public void Execute()
        {
            screen.RemoveEntity(entity);
        }

        public IUndoableAction Reverse()
        {
            return new AddEntityAction(entity, screen);
        }
    }
}
