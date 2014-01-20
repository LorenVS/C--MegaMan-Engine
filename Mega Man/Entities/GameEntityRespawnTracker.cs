﻿using MegaMan.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MegaMan.Engine.Entities
{
    public class GameEntityRespawnTracker : IEntityRespawnTracker
    {
        private readonly Dictionary<EntityPlacement, bool> _respawnableEntities = new Dictionary<EntityPlacement, bool>();

        public void Track(EntityPlacement placement, IEntity entity)
        {
            entity.Removed += () => DisableRespawn(placement, entity);
        }

        private void DisableRespawn(EntityPlacement placement, IEntity entity)
        {
            if (placement.respawn != RespawnBehavior.Offscreen)
                _respawnableEntities[placement] = false;

            entity.Removed -= () => DisableRespawn(placement, entity);
        }

        public void ResetStage()
        {
            foreach (var placement in _respawnableEntities.Keys.Where(p => p.respawn == RespawnBehavior.Stage))
            {
                _respawnableEntities[placement] = true;
            }
        }

        public void ResetDeath()
        {
            foreach (var placement in _respawnableEntities.Keys.Where(p => p.respawn == RespawnBehavior.Death))
            {
                _respawnableEntities[placement] = true;
            }
        }

        public bool IsRespawnable(EntityPlacement placement)
        {
            if (_respawnableEntities.ContainsKey(placement))
                return _respawnableEntities[placement];

            return true;
        }
    }
}
