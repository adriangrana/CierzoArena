using System;
using System.Collections.Generic;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Stable central roster shared by menu, local play and server spawn.
    /// The development fallback is data-driven so the prototype is playable before
    /// final authored assets replace it.</summary>
    [CreateAssetMenu(fileName = "HeroCatalog", menuName = "Cierzo Arena/Hero Catalog")]
    public sealed class HeroCatalog : ScriptableObject
    {
        [SerializeField] private HeroDefinition[] heroes = Array.Empty<HeroDefinition>();
        private readonly Dictionary<string, HeroDefinition> byId = new(StringComparer.Ordinal);
        private bool indexed;
        private static HeroCatalog shared;

        public static HeroCatalog Shared
        {
            get
            {
                if (shared != null) return shared;
                shared = Resources.Load<HeroCatalog>("Heroes/HeroCatalog");
                if (shared == null) shared = HeroRosterFactory.CreateDevelopmentCatalog();
                shared.Reindex(); return shared;
            }
        }

        public IReadOnlyList<HeroDefinition> Heroes { get { Reindex(); return heroes; } }
        public HeroDefinition DefaultHero { get { Reindex(); return heroes != null && heroes.Length > 0 ? heroes[0] : null; } }

        public bool TryGet(string heroId, out HeroDefinition definition)
        {
            definition=null;Reindex();
            return !string.IsNullOrWhiteSpace(heroId) && byId.TryGetValue(heroId, out definition) && definition != null;
        }
        public HeroDefinition ResolveOrFallback(string heroId)
        {
            if (TryGet(heroId, out HeroDefinition definition)) return definition;
            return DefaultHero;
        }
        public void BindDevelopmentPrefab(GameObject prefab)
        {
            if (prefab == null) return;
            Reindex();
            for (int i=0;i<heroes.Length;i++) if (heroes[i] != null && heroes[i].Prefab == null) heroes[i].SetPrefab(prefab);
        }
        public bool Validate(out string reason)
        {
            reason=string.Empty;Reindex();
            if (heroes == null || heroes.Length != 6) { reason="The first roster must contain exactly six heroes."; return false; }
            HashSet<string> ids=new(StringComparer.Ordinal);
            for(int i=0;i<heroes.Length;i++)
            {
                if(heroes[i]==null){reason="Catalog contains a null hero definition.";return false;}
                if(!heroes[i].IsValid(false,out reason)) return false;
                if(!ids.Add(heroes[i].HeroId)){reason=$"Duplicate HeroId: {heroes[i].HeroId}.";return false;}
            }
            reason=string.Empty;return true;
        }
        public void SetDefinitionsForTests(HeroDefinition[] definitions) { heroes=definitions ?? Array.Empty<HeroDefinition>(); indexed=false; Reindex(); }
        private void Reindex()
        {
            if(indexed)return;byId.Clear();
            if(heroes!=null) for(int i=0;i<heroes.Length;i++)
            {
                HeroDefinition hero=heroes[i];
                if(hero==null||string.IsNullOrWhiteSpace(hero.HeroId)||byId.ContainsKey(hero.HeroId)) continue;
                byId.Add(hero.HeroId,hero);
            }
            indexed=true;
        }
    }

    internal static class HeroRosterFactory
    {
        public static HeroCatalog CreateDevelopmentCatalog()
        {
            HeroCatalog catalog=ScriptableObject.CreateInstance<HeroCatalog>();
            HeroDefinition[] roster =
            {
                Create("stone_aegis","Stone Aegis","Bastion of the windbreak","A resilient initiator who holds ground and scatters attackers.",HeroRole.Vanguard,HeroRole.Controller,HeroAttackStyle.Melee,1,new Color(.32f,.66f,.72f),new HeroStats(680,105,220,18,3,4,42,5,4.7f,.12f,2.5f,1.35f,.24f,.36f,12f), new[]{Kit("rampart_strike","Rampart Strike",AbilityTargeting.PointTarget,AbilityEffect.AreaDamage,35,7,55,5,2.1f,0),Kit("windward_guard","Windward Guard",AbilityTargeting.NoTarget,AbilityEffect.SelfShield,40,12,120,0,0,4),Kit("grounding_ring","Grounding Ring",AbilityTargeting.PointTarget,AbilityEffect.AreaSlow,45,11,.42f,5,2.8f,2.5f),Kit("citadel_crash","Citadel Crash",AbilityTargeting.PointTarget,AbilityEffect.StrongAreaStun,90,70,130,6,3.4f,1.8f)}),
                Create("rift_duelist","Rift Duelist","Blade at the gale's edge","A close-range skirmisher who converts tempo into pressure.",HeroRole.Duelist,HeroRole.Carry,HeroAttackStyle.Melee,2,new Color(.86f,.34f,.22f),new HeroStats(540,82,240,20,2,5,57,8,5.9f,.2f,2.35f,1.05f,.18f,.28f,13f), new[]{Kit("rift_lunge","Rift Lunge",AbilityTargeting.PointTarget,AbilityEffect.AreaDamage,35,6,62,5,1.4f,0),Kit("duelist_wind","Duelist Wind",AbilityTargeting.NoTarget,AbilityEffect.SelfMoveSpeed,35,12,1.4f,0,0,4),Kit("counterveil","Counterveil",AbilityTargeting.NoTarget,AbilityEffect.SelfShield,45,14,100,0,0,3),Kit("redline","Redline",AbilityTargeting.PointTarget,AbilityEffect.StrongAreaDamage,90,65,155,5,2.4f,0)}),
                Create("skyline_marksman","Skyline Marksman","Arrow of the open current","A ranged carry with precise projectile pressure.",HeroRole.Carry,HeroRole.Controller,HeroAttackStyle.Ranged,2,new Color(.94f,.78f,.25f),new HeroStats(470,65,260,22,1.6f,6,54,9,5.35f,.16f,8.5f,1.12f,.22f,.3f,20f), new[]{Kit("piercing_gale","Piercing Gale",AbilityTargeting.UnitTarget,AbilityEffect.ProjectileDamage,40,7,70,10,0,0),Kit("tailwind","Tailwind",AbilityTargeting.NoTarget,AbilityEffect.SelfMoveSpeed,35,13,1.1f,0,0,4),Kit("updraft_step","Updraft Step",AbilityTargeting.NoTarget,AbilityEffect.SelfMoveSpeed,45,15,1.8f,0,0,1.5f),Kit("horizon_breaker","Horizon Breaker",AbilityTargeting.UnitTarget,AbilityEffect.ProjectileDamage,95,70,175,14,0,0)}),
                Create("storm_warden","Storm Warden","Keeper of the high current","A battlefield mage who punishes clustered foes with charged weather.",HeroRole.Mage,HeroRole.Controller,HeroAttackStyle.Ranged,2,new Color(.20f,.62f,1f),new HeroStats(455,60,330,32,1.4f,7.5f,38,6,5.1f,.12f,7.5f,1.3f,.25f,.32f,18f), new[]{Kit("arc_bolt","Arc Bolt",AbilityTargeting.UnitTarget,AbilityEffect.ProjectileDamage,35,6,68,10,0,0),Kit("storm_mark","Storm Mark",AbilityTargeting.PointTarget,AbilityEffect.AreaDamage,45,10,72,8,2.7f,0),Kit("gale_step","Gale Step",AbilityTargeting.NoTarget,AbilityEffect.SelfMoveSpeed,30,14,1.5f,0,0,3),Kit("tempest_fall","Tempest Fall",AbilityTargeting.PointTarget,AbilityEffect.StrongAreaStun,90,70,145,10,3.5f,1.4f)}),
                Create("cairn_warden","Cairn Warden","Shelter for the stormbound","A protective support who trades damage for durable protection.",HeroRole.Support,HeroRole.Vanguard,HeroAttackStyle.Ranged,1,new Color(.48f,.88f,.58f),new HeroStats(560,88,310,30,2.7f,7,35,4,5.0f,.13f,6.5f,1.38f,.28f,.35f,16f), new[]{Kit("kindling_orb","Kindling Orb",AbilityTargeting.UnitTarget,AbilityEffect.ProjectileDamage,30,6,42,8,0,0),Kit("cairn_barrier","Cairn Barrier",AbilityTargeting.NoTarget,AbilityEffect.SelfShield,45,11,145,0,0,5),Kit("restoring_draft","Restoring Draft",AbilityTargeting.NoTarget,AbilityEffect.SelfMoveSpeed,35,13,.9f,0,0,5),Kit("sanctuary_field","Sanctuary Field",AbilityTargeting.PointTarget,AbilityEffect.AreaSlow,85,70,.35f,8,4,4)}),
                Create("tempest_arbiter","Tempest Arbiter","Voice between thunderheads","A zone controller whose storms deny routes and isolate targets.",HeroRole.Controller,HeroRole.Mage,HeroAttackStyle.Ranged,3,new Color(.70f,.38f,.94f),new HeroStats(490,70,300,34,1.8f,7.2f,43,6,5.25f,.14f,7.2f,1.27f,.25f,.33f,17f), new[]{Kit("pressure_drop","Pressure Drop",AbilityTargeting.PointTarget,AbilityEffect.AreaSlow,35,7,.48f,8,2.5f,2.5f),Kit("static_lattice","Static Lattice",AbilityTargeting.PointTarget,AbilityEffect.StrongAreaStun,50,13,50,8,2.5f,1),Kit("crosswind","Crosswind",AbilityTargeting.NoTarget,AbilityEffect.SelfMoveSpeed,35,14,1.2f,0,0,4),Kit("eye_of_tempest","Eye of Tempest",AbilityTargeting.PointTarget,AbilityEffect.StrongAreaDamage,95,72,165,10,4.2f,0)})
            };
            catalog.SetDefinitionsForTests(roster);return catalog;
        }
        private static HeroDefinition Create(string id,string name,string title,string description,HeroRole primary,HeroRole secondary,HeroAttackStyle style,int difficulty,Color color,HeroStats stats,AbilityDefinition[] kit)
        { HeroDefinition definition=ScriptableObject.CreateInstance<HeroDefinition>();definition.ConfigureRuntime(id,name,title,description,primary,secondary,style,difficulty,color,stats,kit);Texture2D portrait=Resources.Load<Texture2D>($"Art/UI/HeroPortraits/{PortraitFileName(id)}") ?? CreatePortrait(color,primary);definition.SetPresentation(portrait,portrait);return definition; }
        private static AbilityDefinition Kit(string id,string name,AbilityTargeting target,AbilityEffect effect,float mana,float cooldown,float value,float range,float radius,float duration)
        { AbilityDefinition definition=ScriptableObject.CreateInstance<AbilityDefinition>();definition.ConfigureRuntime(id,name,name+" — first roster ability.",target,effect,mana,cooldown,value,range,radius,duration);return definition; }
        private static Texture2D CreatePortrait(Color color,HeroRole role)
        {
            const int size=48;Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=role+" Portrait"};Color dark=Color.Lerp(color,Color.black,.72f);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++){float dx=(x-size*.5f)/(size*.5f),dy=(y-size*.5f)/(size*.5f);float radius=Mathf.Sqrt(dx*dx+dy*dy);texture.SetPixel(x,y,Color.Lerp(color,dark,Mathf.Clamp01(radius))+(Mathf.Abs(dx)<.16f&&dy<.35f?Color.white*.12f:Color.black));}texture.Apply(false,true);return texture;
        }
        private static string PortraitFileName(string id)
        {
            return id switch
            {
                "stone_aegis" => "StoneAegisPortrait",
                "rift_duelist" => "RiftDuelistPortrait",
                "skyline_marksman" => "SkylineMarksmanPortrait",
                "storm_warden" => "StormWardenPortrait",
                "cairn_warden" => "CairnWardenPortrait",
                "tempest_arbiter" => "TempestArbiterPortrait",
                _ => string.Empty
            };
        }
    }
}
