using CierzoArena.Combat;
using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Visual-only adapter for the user-provided Free Animated Low Poly
    /// Goblin package. The gameplay root, collider, health, NavMeshAgent and NGO
    /// components remain untouched; only the placeholder primitive is replaced.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreepGoblinPresentation : MonoBehaviour
    {
        private const string ResourcePath = "Art/Units/Goblin/goblin";
        private static GameObject source;
        private static bool sourceAttempted;
        private bool built;
        private Transform visualRoot;
        private Vector3 anchoredLocalPosition;
        private Vector3 lastRootPosition;
        private BasicAttack attack;
        private AttackState lastAttackState;
        private Animation legacyAnimation;
        private AnimationClip attackClip;

        private void Awake() => Build();

        private void Update()
        {
            if (!built || visualRoot == null)
            {
                return;
            }

            // The model follows the NavMeshAgent-owned root rotation.  Its source
            // forward axis already matches Unity forward; the previous 180 degree
            // correction made every lane goblin visibly walk backwards.
            Vector3 displacement = transform.position - lastRootPosition;
            displacement.y = 0f;
            bool walking = displacement.sqrMagnitude > 0.000004f;
            lastRootPosition = transform.position;

            float attackPose = 0f;
            if (attack != null)
            {
                if (attack.State == AttackState.Windup && lastAttackState != AttackState.Windup)
                {
                    legacyAnimation?.Play("CierzoGoblinAttack");
                }

                attackPose = attack.State == AttackState.Windup ? 1f : 0f;
                lastAttackState = attack.State;
            }

            // The supplied model contains an attack take but no walk take. Keep
            // movement readable with a restrained procedural gait until a proper
            // walk clip is supplied; it changes only the visual child, never the
            // authoritative unit transform or navigation.
            float bob = walking ? Mathf.Sin(Time.time * 15f) * 0.035f : 0f;
            float lunge = attackPose * 0.04f;
            visualRoot.localPosition = anchoredLocalPosition + new Vector3(0f, bob, lunge);
            visualRoot.localRotation = Quaternion.identity;
        }

        public static void Ensure(GameObject creep)
        {
            if (creep == null || !creep.TryGetComponent(out CreepController _)) return;
            CreepGoblinPresentation presentation = creep.GetComponent<CreepGoblinPresentation>();
            if (presentation == null) presentation = creep.AddComponent<CreepGoblinPresentation>();
            presentation.Build();
        }

        private void Build()
        {
            if (built) return;
            source ??= LoadSource();
            if (source == null) return;
            built = true;

            GameObject visual = Instantiate(source, transform);
            visual.name = "Goblin Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * .9f;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true)) MakeBuiltInCompatible(renderer);

            Collider body = GetComponent<Collider>();
            Renderer[] visualRenderers = visual.GetComponentsInChildren<Renderer>(true);
            if (body != null && visualRenderers.Length > 0)
            {
                Bounds bounds = visualRenderers[0].bounds;
                for (int i = 1; i < visualRenderers.Length; i++) bounds.Encapsulate(visualRenderers[i].bounds);
                visual.transform.position += Vector3.up * (body.bounds.min.y - bounds.min.y);
            }

            visualRoot = visual.transform;
            anchoredLocalPosition = visualRoot.localPosition;
            lastRootPosition = transform.position;
            attack = GetComponent<BasicAttack>();
            lastAttackState = attack != null ? attack.State : AttackState.Idle;
            AttachPackageAttackClip(visual);

            // The root primitive is retained only as the gameplay collider. Remove
            // every root renderer (rather than merely tinting it) so a sphere/capsule
            // can never remain visible beneath the goblin.
            foreach (Renderer placeholder in GetComponents<Renderer>())
            {
                placeholder.enabled = false;
                Destroy(placeholder);
            }
            if (TryGetComponent(out CreepController controller)) controller.RefreshPresentation();
            if (TryGetComponent(out VisionVisibility visibility)) visibility.RefreshPresentation();
        }

        private void AttachPackageAttackClip(GameObject visual)
        {
            // The package exposes a single embedded "Armature|attack" take.  Load
            // it opportunistically: if an import setting omits it at runtime, the
            // gameplay attack still works and the procedural lunge remains intact.
            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(ResourcePath);
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name.ToLowerInvariant().Contains("attack"))
                {
                    attackClip = clips[i];
                    break;
                }
            }

            if (attackClip == null)
            {
                return;
            }

            legacyAnimation = visual.GetComponent<Animation>();
            if (legacyAnimation == null) legacyAnimation = visual.AddComponent<Animation>();
            legacyAnimation.AddClip(attackClip, "CierzoGoblinAttack");
            legacyAnimation.wrapMode = WrapMode.Once;
        }

        private static GameObject LoadSource()
        {
            if (sourceAttempted) return null;
            sourceAttempted = true;
            return Resources.Load<GameObject>(ResourcePath);
        }

        private static void MakeBuiltInCompatible(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material original = materials[i];
                if (original == null || (original.shader != null && original.shader.name == "Standard")) continue;
                Shader standard = Shader.Find("Standard");
                if (standard == null) continue;
                Color color = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.HasProperty("_Color") ? original.color : Color.white;
                materials[i] = new Material(standard) { name = original.name + " (Built-in)", hideFlags = HideFlags.DontSave, color = color };
            }
            renderer.sharedMaterials = materials;
        }
    }
}
