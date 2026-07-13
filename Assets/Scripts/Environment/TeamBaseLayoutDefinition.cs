using UnityEngine;

namespace CierzoArena.Environment
{
    /// <summary>
    /// Single reusable, orientation-relative layout for a team base (M23). Offsets are
    /// expressed in a base-local frame so Azure and Ember share the exact same
    /// competitive footprint, only mirrored/rotated by their orientation:
    ///
    /// - <b>forward</b> points from the base toward the map centre (the origin),
    /// - <b>right</b> is the in-plane perpendicular,
    /// - <b>back</b> = -forward points toward the safe rear (spawn courtyard).
    ///
    /// Each offset is (forward, right) in metres. The hero therefore spawns behind the
    /// core (negative forward), the shop sits to one rear side, the Core Guards stand
    /// in front (positive forward), and the gateways are further front toward the
    /// lanes. There is deliberately only one layout: never two hand-authored bases.
    /// </summary>
    [CreateAssetMenu(fileName = "TeamBaseLayoutDefinition", menuName = "Cierzo Arena/Team Base Layout Definition")]
    public sealed class TeamBaseLayoutDefinition : ScriptableObject
    {
        [Header("Core & rear (base-local: x=forward toward centre, y=right)")]
        [SerializeField] private Vector2 coreOffset = new Vector2(0f, 0f);
        [SerializeField] private Vector2 heroSpawnOffset = new Vector2(-14f, 0f);
        [SerializeField] private Vector2 respawnOffset = new Vector2(-14f, 0f);
        [SerializeField] private Vector2 cameraStartOffset = new Vector2(-14f, 0f);

        [Header("Shop")]
        [SerializeField] private Vector2 shopOffset = new Vector2(-11f, 11f);
        [SerializeField] private Vector2 shopkeeperOffset = new Vector2(-11.5f, 13f);

        [Header("Core Guards (front, spread left/right)")]
        [SerializeField] private Vector2 coreGuardLeftOffset = new Vector2(8.5f, -8f);
        [SerializeField] private Vector2 coreGuardRightOffset = new Vector2(8.5f, 8f);
        [SerializeField] private Vector2 coreDefenseApproachOffset = new Vector2(12.5f, 0f);
        [SerializeField] private Vector2 coreApproachOffset = new Vector2(4.5f, 0f);

        [Header("Gateways & interior waypoints (front toward lanes)")]
        [SerializeField] private Vector2 topGatewayOffset = new Vector2(26f, -20f);
        [SerializeField] private Vector2 midGatewayOffset = new Vector2(30f, 0f);
        [SerializeField] private Vector2 bottomGatewayOffset = new Vector2(26f, 20f);
        [SerializeField] private Vector2 topInteriorOffset = new Vector2(19f, -13f);
        [SerializeField] private Vector2 midInteriorOffset = new Vector2(21f, 0f);
        [SerializeField] private Vector2 bottomInteriorOffset = new Vector2(19f, 13f);

        public Vector2 CoreOffset => coreOffset;
        public Vector2 HeroSpawnOffset => heroSpawnOffset;
        public Vector2 CoreGuardLeftOffset => coreGuardLeftOffset;
        public Vector2 CoreGuardRightOffset => coreGuardRightOffset;

        /// <summary>Resolved world-space anchors for one base. Forward is computed from
        /// the base centre toward <paramref name="mapCenter"/>, so the same definition
        /// mirrors perfectly between Azure and Ember.</summary>
        public readonly struct Resolved
        {
            public readonly Vector3 Forward;
            public readonly Vector3 Right;
            public readonly Vector3 Core;
            public readonly Vector3 HeroSpawn;
            public readonly Vector3 Respawn;
            public readonly Vector3 CameraStart;
            public readonly Vector3 Shop;
            public readonly Vector3 Shopkeeper;
            public readonly Vector3 CoreGuardLeft;
            public readonly Vector3 CoreGuardRight;
            public readonly Vector3 CoreDefenseApproach;
            public readonly Vector3 CoreApproach;
            public readonly Vector3 TopGateway;
            public readonly Vector3 MidGateway;
            public readonly Vector3 BottomGateway;
            public readonly Vector3 TopInterior;
            public readonly Vector3 MidInterior;
            public readonly Vector3 BottomInterior;

            public Resolved(Vector3 forward, Vector3 right, Vector3 core, Vector3 heroSpawn, Vector3 respawn, Vector3 cameraStart,
                Vector3 shop, Vector3 shopkeeper, Vector3 coreGuardLeft, Vector3 coreGuardRight, Vector3 coreDefenseApproach, Vector3 coreApproach,
                Vector3 topGateway, Vector3 midGateway, Vector3 bottomGateway, Vector3 topInterior, Vector3 midInterior, Vector3 bottomInterior)
            {
                Forward = forward; Right = right; Core = core; HeroSpawn = heroSpawn; Respawn = respawn; CameraStart = cameraStart;
                Shop = shop; Shopkeeper = shopkeeper; CoreGuardLeft = coreGuardLeft; CoreGuardRight = coreGuardRight;
                CoreDefenseApproach = coreDefenseApproach; CoreApproach = coreApproach;
                TopGateway = topGateway; MidGateway = midGateway; BottomGateway = bottomGateway;
                TopInterior = topInterior; MidInterior = midInterior; BottomInterior = bottomInterior;
            }
        }

        public Resolved Resolve(Vector3 baseCenter) => Resolve(baseCenter, Vector3.zero);

        public Resolved Resolve(Vector3 baseCenter, Vector3 mapCenter)
        {
            Vector3 flatCenter = new Vector3(baseCenter.x, 0f, baseCenter.z);
            Vector3 toCenter = new Vector3(mapCenter.x - baseCenter.x, 0f, mapCenter.z - baseCenter.z);
            Vector3 forward = toCenter.sqrMagnitude > 1e-4f ? toCenter.normalized : Vector3.forward;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            Vector3 World(Vector2 o) => flatCenter + forward * o.x + right * o.y;

            return new Resolved(
                forward, right,
                World(coreOffset), World(heroSpawnOffset), World(respawnOffset), World(cameraStartOffset),
                World(shopOffset), World(shopkeeperOffset), World(coreGuardLeftOffset), World(coreGuardRightOffset),
                World(coreDefenseApproachOffset), World(coreApproachOffset),
                World(topGatewayOffset), World(midGatewayOffset), World(bottomGatewayOffset),
                World(topInteriorOffset), World(midInteriorOffset), World(bottomInteriorOffset));
        }
    }
}
