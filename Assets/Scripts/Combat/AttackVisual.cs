using UnityEngine;

namespace CierzoArena.Combat
{
    /// <summary>
    /// Lightweight provisional attack feedback. It only tints a renderer from the
    /// authoritative attack state; it never drives combat and never moves the root
    /// transform used by navigation or replication.
    /// </summary>
    [RequireComponent(typeof(BasicAttack))]
    public sealed class AttackVisual : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color windupColor = new(1f, 0.82f, 0.28f, 1f);
        [SerializeField] private Color backswingColor = new(1f, 0.5f, 0.18f, 1f);

        private BasicAttack attack;
        private MaterialPropertyBlock properties;
        private Color baseColor = Color.white;
        private AttackState previousState = AttackState.Idle;

        private void Awake()
        {
            attack = GetComponent<BasicAttack>();
            targetRenderer ??= GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            if (targetRenderer != null && targetRenderer.sharedMaterial != null)
            {
                baseColor = targetRenderer.sharedMaterial.color;
            }
        }

        private void Update()
        {
            if (attack == null || targetRenderer == null || attack.State == previousState)
            {
                return;
            }

            previousState = attack.State;
            Color color = previousState == AttackState.Windup
                ? windupColor
                : previousState == AttackState.Backswing ? backswingColor : baseColor;
            targetRenderer.GetPropertyBlock(properties);
            properties.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(properties);
        }
    }
}
