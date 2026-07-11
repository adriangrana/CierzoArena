using UnityEngine;

namespace CierzoArena.Units
{
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Cierzo Arena/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId = "item.id";
        [SerializeField] private string displayName = "Item";
        [TextArea, SerializeField] private string description;
        [SerializeField, Min(0)] private int purchasePrice = 50;
        [SerializeField, Min(0)] private int salePrice = 25;
        [SerializeField] private float maximumHealthBonus;
        [SerializeField] private float attackDamageBonus;
        [SerializeField] private float movementSpeedBonus;
        [SerializeField] private float attackSpeedBonus;
        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public int PurchasePrice => Mathf.Max(0, purchasePrice);
        public int SalePrice => Mathf.Clamp(salePrice, 0, PurchasePrice);
        public float MaximumHealthBonus => Mathf.Max(0, maximumHealthBonus);
        public float AttackDamageBonus => Mathf.Max(0, attackDamageBonus);
        public float MovementSpeedBonus => Mathf.Max(0, movementSpeedBonus);
        public float AttackSpeedBonus => Mathf.Max(0, attackSpeedBonus);

        private void OnValidate()
        {
            itemId = itemId?.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? "Item" : displayName.Trim();
            purchasePrice = Mathf.Max(0, purchasePrice);
            salePrice = Mathf.Clamp(salePrice, 0, purchasePrice);
            maximumHealthBonus = Mathf.Max(0f, maximumHealthBonus);
            attackDamageBonus = Mathf.Max(0f, attackDamageBonus);
            movementSpeedBonus = Mathf.Max(0f, movementSpeedBonus);
            attackSpeedBonus = Mathf.Max(0f, attackSpeedBonus);
        }
    }
}
