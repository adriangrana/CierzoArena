using UnityEngine;
using CierzoArena.Core;

namespace CierzoArena.Units
{
    public sealed class SelectableUnit : MonoBehaviour
    {
        [SerializeField] private Renderer selectionRing;

        private TeamMember teamMember;
        private MaterialPropertyBlock ringPropertyBlock;
        private static Mesh ringMesh;

        public bool IsSelected { get; private set; }

        private void Awake()
        {
            teamMember = GetComponent<TeamMember>();
            EnsureRingIsAnOutline();
            AlignRingToGround();
            SetSelected(false);
        }

        private void LateUpdate()
        {
            // A hero visual may be applied after this component's Awake, and the
            // unit can move every frame. Keep the ring on the actual model base,
            // not at the old capsule placeholder's hard-coded height.
            AlignRingToGround();
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (selectionRing != null)
            {
                ApplyTeamRingColor();
                selectionRing.enabled = selected;
            }
        }

        private void ApplyTeamRingColor()
        {
            if (selectionRing == null) return;
            teamMember ??= GetComponent<TeamMember>();
            Color color = teamMember != null && teamMember.Team == TeamId.Ember
                ? new Color(.95f, .28f, .18f, 1f)
                : new Color(.12f, .68f, 1f, 1f);
            ringPropertyBlock ??= new MaterialPropertyBlock();
            selectionRing.GetPropertyBlock(ringPropertyBlock);
            ringPropertyBlock.SetColor("_Color", color);
            ringPropertyBlock.SetColor("_BaseColor", color);
            selectionRing.SetPropertyBlock(ringPropertyBlock);
        }

        /// <summary>The old selection marker was a flattened cylinder, so it read as
        /// a solid team-colour disk under the model. Use one shared annulus mesh:
        /// it has a genuinely empty centre and remains readable over terrain.</summary>
        private void EnsureRingIsAnOutline()
        {
            if (selectionRing == null) return;
            MeshFilter filter = selectionRing.GetComponent<MeshFilter>();
            if (filter == null) return;
            filter.sharedMesh = GetRingMesh();
        }

        private static Mesh GetRingMesh()
        {
            if (ringMesh != null) return ringMesh;

            const int segments = 48;
            const float outerRadius = .5f;
            const float innerRadius = .37f;
            Vector3[] vertices = new Vector3[segments * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                int outer = i * 2;
                vertices[outer] = new Vector3(cosine * outerRadius, 0f, sine * outerRadius);
                vertices[outer + 1] = new Vector3(cosine * innerRadius, 0f, sine * innerRadius);
                uv[outer] = new Vector2((cosine + 1f) * .5f, (sine + 1f) * .5f);
                uv[outer + 1] = uv[outer];

                int next = ((i + 1) % segments) * 2;
                int triangle = i * 6;
                triangles[triangle] = outer;
                triangles[triangle + 1] = outer + 1;
                triangles[triangle + 2] = next;
                triangles[triangle + 3] = next;
                triangles[triangle + 4] = outer + 1;
                triangles[triangle + 5] = next + 1;
            }

            ringMesh = new Mesh { name = "Hero Selection Ring (Runtime)", hideFlags = HideFlags.DontSave };
            ringMesh.vertices = vertices;
            ringMesh.uv = uv;
            ringMesh.triangles = triangles;
            ringMesh.RecalculateNormals();
            ringMesh.RecalculateBounds();
            return ringMesh;
        }

        private void AlignRingToGround()
        {
            if (selectionRing == null) return;

            float groundY;
            HeroVisualController visual = GetComponent<HeroVisualController>();
            GameObject model = visual != null ? visual.ActiveVisualInstance : null;
            if (model != null && TryGetRendererBounds(model, out Bounds modelBounds))
            {
                groundY = modelBounds.min.y;
            }
            else if (TryGetComponent(out Collider collider))
            {
                groundY = collider.bounds.min.y;
            }
            else
            {
                groundY = transform.position.y;
            }

            Vector3 position = selectionRing.transform.position;
            position.x = transform.position.x;
            position.y = groundY + .025f;
            position.z = transform.position.z;
            selectionRing.transform.position = position;
        }

        private static bool TryGetRendererBounds(GameObject model, out Bounds bounds)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);
            }
            return true;
        }
    }
}
