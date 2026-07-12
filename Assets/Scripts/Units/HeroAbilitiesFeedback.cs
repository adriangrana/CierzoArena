using CierzoArena.CameraSystem;
using CierzoArena.Frontend;
using UnityEngine;

namespace CierzoArena.Units
{
    [RequireComponent(typeof(HeroAbilities))]
    [RequireComponent(typeof(HeroMana))]
    public sealed class HeroAbilitiesFeedback : MonoBehaviour
    {
        private HeroAbilities abilities; private HeroMana mana; private IHeroAbilityRequestGateway gateway;
        private GUIStyle style; private GUIStyle title;
        private void Awake()
        {
            abilities = GetComponent<HeroAbilities>(); mana = GetComponent<HeroMana>(); FindGateway();
        }
        private void OnGUI()
        {
            if (LocalHeroProvider.Active == null || LocalHeroProvider.Active.CurrentHero != transform) return;
            EnsureStyles();
            float scale = HudLayout.Scale; Matrix4x4 old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));Rect panel=HudLayout.Abilities;
            GUI.Box(new Rect(panel.x,panel.y,panel.width,48), $"MANA {mana.CurrentMana:0}/{mana.MaximumMana:0}   Regen {mana.RegenerationPerSecond:0.0}/s   Skill points {abilities.SkillPoints}", style);
            string[] keys = { "Q", "W", "E", "R" };
            for (int i = 0; i < 4; i++)
            {
                AbilityDefinition definition = abilities.GetDefinition(i); float x = panel.x + i * 143;
                string text = definition == null ? "Empty" : $"{keys[i]} {definition.DisplayName}\nLv {abilities.GetLevel(i)}/{definition.MaximumLevel}  CD {abilities.GetCooldown(i):0.0}\nMana {definition.ManaCost(Mathf.Max(1, abilities.GetLevel(i))):0}";
                GUI.Box(new Rect(x, panel.y+56, 135, 86), text, style);
                if (definition != null && GUI.Button(new Rect(x + 32, panel.y+118, 70, 21), "+ Level")) Upgrade(i);
            }
            if (abilities.CastState == AbilityCastState.Casting) GUI.Label(new Rect(panel.x-250, panel.y, 240, 40), "CASTING... (right click cancels)", title);
            GUI.matrix = old;
        }
        private void Upgrade(int slot)
        {
            if ((gateway ?? FindAndReturnGateway()) != null && gateway.IsReady) gateway.RequestUpgrade(slot); else abilities.TryUpgrade(slot);
        }
        private void FindGateway()
        {
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>()) if (component is IHeroAbilityRequestGateway found) { gateway = found; break; }
        }
        private IHeroAbilityRequestGateway FindAndReturnGateway() { FindGateway(); return gateway; }
        private void EnsureStyles()
        {
            if (style != null) return;
            style = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
            title = new GUIStyle(style) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, .86f, .25f) } };
        }
    }
}
