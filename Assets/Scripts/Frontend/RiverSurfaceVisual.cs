using UnityEngine;

namespace CierzoArena.Frontend
{
    /// <summary>Built-in compatible visual current. It uses a property block so the
    /// shared water material remains shared by every local/network observer.</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class RiverSurfaceVisual : MonoBehaviour
    {
        [SerializeField] private Vector2 currentSpeed = new Vector2(.012f,.028f);
        private Renderer targetRenderer; private Material material; private MaterialPropertyBlock properties; private Color baseColor;
        private void Awake() => Initialize();
        private void OnEnable() => Initialize();

        private bool Initialize()
        {
            targetRenderer ??= GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                return false;
            }

            material = targetRenderer.sharedMaterial;
            properties ??= new MaterialPropertyBlock();
            baseColor = material != null && material.HasProperty("_Color") ? material.color : new Color(.16f,.46f,.62f,1f);
            return material != null;
        }

        private void Update()
        {
            if (!Initialize()) return;
            float pulse=.05f*(.5f+.5f*Mathf.Sin(Time.time*.55f));Vector2 offset=currentSpeed*Time.time;
            // This component owns the visual material override. Clearing the block
            // avoids a null destination during script/domain reloads and avoids
            // inheriting stale overrides from a previous material assignment.
            properties.Clear();
            properties.SetColor("_Color",Color.Lerp(baseColor,Color.cyan,pulse));
            properties.SetVector("_MainTex_ST",new Vector4(4f,4f,offset.x,offset.y));
            targetRenderer.SetPropertyBlock(properties);
        }
    }
}
