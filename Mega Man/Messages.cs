﻿using System.Collections.Generic;

namespace MegaMan.Engine
{
    public interface IGameMessage
    {
        GameEntity Source { get; }
    }

    public class DamageMessage : IGameMessage
    {
        public GameEntity Source { get; private set; }
        public float Damage { get; private set; }

        public DamageMessage(GameEntity source, float damage)
        {
            Source = source;
            Damage = damage;
        }
    }

    public class HealMessage : IGameMessage
    {
        public GameEntity Source { get; private set; }
        public float Health { get; private set; }

        public HealMessage(GameEntity source, float health)
        {
            Source = source;
            Health = health;
        }
    }

    public class SoundMessage : IGameMessage
    {
        public GameEntity Source { get; private set; }
        public string SoundName { get; private set; }
        public bool Playing { get; private set; }

        public SoundMessage(GameEntity source, string soundname, bool playing)
        {
            Source = source;
            SoundName = soundname;
            Playing = playing;
        }
    }

    public class StateMessage : IGameMessage
    {
        public GameEntity Source { get; private set; }
        public string StateName { get; private set; }

        public StateMessage(GameEntity source, string statename)
        {
            Source = source;
            StateName = statename;
        }
    }

    public class HitBoxMessage : IGameMessage
    {
        public GameEntity Source { get; private set; }
        public List<CollisionBox> AddBoxes { get; private set; }
        public HashSet<string> EnableBoxes { get; private set; }
        public bool Clear { get; set; }

        public HitBoxMessage(GameEntity source, List<CollisionBox> newboxes, HashSet<string> enable, bool clear)
        {
            Source = source;
            AddBoxes = newboxes;
            EnableBoxes = enable;
            Clear = clear;
        }
    }
}
