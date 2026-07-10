using UnityEngine;

namespace CierzoArena.Combat
{
    public sealed class DamageNumber : MonoBehaviour
    {
        private TextMesh textMesh;
        private float lifetime;
        private float riseDistance;
        private float elapsed;
        private Color initialColor;

        public void Initialize(TextMesh sourceText, float duration, float rise)
        {
            textMesh = sourceText;
            lifetime = Mathf.Max(0.1f, duration);
            riseDistance = Mathf.Max(0f, rise);
            initialColor = textMesh.color;
        }

        private void Update()
        {
            if (textMesh == null)
            {
                Destroy(gameObject);
                return;
            }

            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / lifetime);
            transform.position += Vector3.up * (riseDistance / lifetime) * Time.deltaTime;

            Color color = initialColor;
            color.a = 1f - normalized;
            textMesh.color = color;

            if (normalized >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            Camera targetCamera = Camera.main;
            if (targetCamera != null)
            {
                transform.rotation = targetCamera.transform.rotation;
            }
        }
    }
}
