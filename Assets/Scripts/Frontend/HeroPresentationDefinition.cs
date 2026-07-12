using UnityEngine;

namespace CierzoArena.Frontend
{
    [CreateAssetMenu(fileName="HeroPresentation",menuName="Cierzo Arena/Hero Presentation")]
    public sealed class HeroPresentationDefinition : ScriptableObject
    {
        [SerializeField] private string heroName="Prototype Hero";
        [SerializeField] private string role="Skirmisher";
        [SerializeField] private string combatStyle="Melee";
        [SerializeField,Range(1,3)] private int difficulty=1;
        [TextArea,SerializeField] private string description;
        [TextArea,SerializeField] private string abilities;
        public string HeroName=>heroName; public string Role=>role; public string CombatStyle=>combatStyle; public int Difficulty=>difficulty; public string Description=>description; public string Abilities=>abilities;
    }
}
