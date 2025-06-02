using System;
using System.Collections.Generic;
using BazaarGameShared.Domain.Core;
using BazaarGameShared.Domain.Core.Types;

namespace BazaarPlannerMod;

public class RunInfo
{
    public String Hero; 
    public List<SkillInfo> Skills;
    public List<SkillInfo> OppSkills;
    public List<CardInfo> Cards; 
    public List<CardInfo> Stash; 
    public List<CardInfo> OppCards;
    public List<CardInfo> OppStash;
    public int? OppHealth;
    public int? OppRegen;
    public int? OppGold;
    public int? OppIncome;
    public int? OppShield;
    public int? OppLevel;
    public string OppName;
    public uint Wins;
    public uint Losses;
    public int Day;
    public string Version = "0.0.1";
    public int? Health;
    public int? Shield;
    public int? Regen { get; set; }
    public int? Level { get; set; }
    public int? Gold { get; set; }
    public int? Income { get; set; }
    public string Name;
    public string RunId;
    public string OppHero;
    public int? Prestige;
    public int? OppPrestige;
    public bool PlayMode;

    public class SkillInfo
    {
        public ETier Tier;
        public Guid TemplateId;
        public string Name;
        public Dictionary<ECardAttributeType, int> Attributes { get; set; } = new Dictionary<ECardAttributeType, int>();
    }
    public class CardInfo
    {
        public ETier Tier;
        public string Name;
        public Guid TemplateId;
        public EContainerSocketId? Left;
        public InstanceId Instance;
        public HashSet<ECardTag> Tags;
        public Dictionary<ECardAttributeType, int> Attributes { get; set; } = new Dictionary<ECardAttributeType, int>();
        public string Enchant;
    }
    
}