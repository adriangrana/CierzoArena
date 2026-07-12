#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CierzoArena.CameraSystem;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Navigation;
using CierzoArena.Netcode;
using CierzoArena.Structures;
using CierzoArena.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;

namespace CierzoArena.EditorTools
{
    /// <summary>
    /// Explicit, menu-driven builder for the first complete MOBA greybox (M3C). It is
    /// a technical, primitive-only layout (no final art) that expresses the intended
    /// Azure-vs-Ember identity purely through simple shapes and flat materials:
    ///
    /// - Azure base (south-west) and Ember base (north-east) on opposite corners,
    ///   each a fortified, team-coloured platform with a core and a lane-facing gate.
    /// - Three parallel lanes (north / mid / south) that read as top/mid/bottom at a
    ///   glance instead of a fan of crossing diagonals; the mid lane is the primary one.
    /// - A diagonal river band that is fully walkable ground (a distinct zone, not a
    ///   gap): units can cross it anywhere, and it hosts the neutral boss pit.
    /// - One highlighted bridge per lane marking where each lane meets the river
    ///   (visual emphasis / intended chokepoint), no longer the only crossing.
    /// - Two off-lane neutral jungle pockets (north-west and south-east) with markers.
    /// - Large obstacles in the gaps between lanes that force real pathfinding.
    /// - Tower markers (three per lane and team) and three base spawn/access points
    ///   per team, one per lane, expressing that each base is reached by three routes.
    ///
    /// It reuses the M3B navigation stack (<see cref="LargeNavMeshBootstrap"/> +
    /// <see cref="NavPathProbe"/>) and the existing gameplay systems. As of M4.4 the
    /// scene uses the real MOBA camera (<see cref="MobaCameraController"/> +
    /// <see cref="CameraWorldBounds"/> + <see cref="LocalHeroProvider"/>) instead of the
    /// M3A <see cref="IsometricCameraRig"/>, starting framed on and following the local
    /// Azure hero. It never touches PrototypeArena or NavigationScaleSpike, and never
    /// runs on editor load.
    /// </summary>
    public static class MobaGreyboxArenaBuilder
    {
        private const string ScenePath = "Assets/Scenes/MobaGreyboxArena.unity";
        private const int GroundLayer = 6;
        private const int SelectableLayer = 7;
        private const int AttackableLayer = 8;

        // Corners of the square arena. Azure at SW, Ember at NE (bottom-left /
        // top-right, like the classic MOBA layout). NW and SE are the outer lane
        // corners where the top and bottom lanes bend and cross the river.
        private static readonly Vector3 AzureBaseCenter = new Vector3(-60f, 0f, -60f);
        private static readonly Vector3 EmberBaseCenter = new Vector3(60f, 0f, 60f);
        private static readonly Vector3 NorthWestCorner = new Vector3(-60f, 0f, 60f);
        private static readonly Vector3 SouthEastCorner = new Vector3(60f, 0f, -60f);

        // Neutral boss pit, on the river's perpendicular-bisector axis (z = -x) between
        // the two bases, so straight-line distance from Azure and Ember is identical.
        // Kept off the mid crossing and clear of the jungle camps; the pit walls are
        // mirror-symmetric across that axis so both teams' approaches match.
        private static readonly Vector3 BossPitCenter = new Vector3(-18f, 0f, 18f);

        [MenuItem("Cierzo Arena/Create MOBA Greybox Arena")]
        public static void CreateMobaGreyboxArena()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MobaGreyboxArena";

            EnsureLayerName(GroundLayer, "Ground");
            EnsureLayerName(SelectableLayer, "Selectable");
            EnsureLayerName(AttackableLayer, "Attackable");

            Material groundMaterial = CreateMaterial("Assets/Materials/Prototype_Ground.mat", new Color(0.30f, 0.37f, 0.33f));
            Material routeMaterial = CreateMaterial("Assets/Materials/Prototype_Route.mat", new Color(0.68f, 0.58f, 0.34f));
            Material routeMidMaterial = CreateMaterial("Assets/Materials/Prototype_RouteMid.mat", new Color(0.87f, 0.76f, 0.46f));
            Material riverMaterial = CreateMaterial("Assets/Materials/Prototype_River.mat", new Color(0.03f, 0.10f, 0.26f));
            Material bridgeMaterial = CreateMaterial("Assets/Materials/Prototype_Bridge.mat", new Color(0.52f, 0.50f, 0.55f));
            Material obstacleMaterial = CreateMaterial("Assets/Materials/Prototype_Obstacle.mat", new Color(0.15f, 0.16f, 0.19f));
            Material neutralMaterial = CreateMaterial("Assets/Materials/Prototype_Neutral.mat", new Color(0.46f, 0.33f, 0.56f));
            Material boundaryMaterial = CreateMaterial("Assets/Materials/Prototype_Boundary.mat", new Color(0.09f, 0.09f, 0.11f));
            Material azureBaseMaterial = CreateMaterial("Assets/Materials/Prototype_AzureBase.mat", new Color(0.10f, 0.30f, 0.70f));
            Material emberBaseMaterial = CreateMaterial("Assets/Materials/Prototype_EmberBase.mat", new Color(0.70f, 0.17f, 0.10f));
            Material azureMaterial = CreateMaterial("Assets/Materials/Prototype_Azure.mat", new Color(0.20f, 0.55f, 1.0f));
            Material emberMaterial = CreateMaterial("Assets/Materials/Prototype_Ember.mat", new Color(0.96f, 0.26f, 0.18f));
            Material ringMaterial = CreateMaterial("Assets/Materials/Prototype_Selection.mat", new Color(0.95f, 0.86f, 0.24f));
            Material markerMaterial = CreateMaterial("Assets/Materials/Prototype_Marker.mat", new Color(0.95f, 0.86f, 0.24f));
            Material healthBackgroundMaterial = CreateMaterial("Assets/Materials/Prototype_HealthBackground.mat", new Color(0.08f, 0.08f, 0.08f));
            Material healthFillMaterial = CreateMaterial("Assets/Materials/Prototype_HealthFill.mat", new Color(0.2f, 0.85f, 0.3f));

            BuildGroundAndRiver(groundMaterial, riverMaterial);
            BuildBridges(bridgeMaterial);
            BuildLanes(routeMaterial, routeMidMaterial);
            GameObject matchController = CreateMatchController();
            BuildBases(azureBaseMaterial, emberBaseMaterial, markerMaterial, healthBackgroundMaterial, healthFillMaterial);
            CreateHeroSpawnPoint("Azure Hero Spawn", AzureBaseCenter + new Vector3(9f, 1f, 9f), TeamId.Azure);
            CreateHeroSpawnPoint("Ember Hero Spawn", EmberBaseCenter + new Vector3(-9f, 1f, -9f), TeamId.Ember);
            BuildObstacles(obstacleMaterial);
            BuildNeutralZones(neutralMaterial, markerMaterial);
            BuildTowersAndSpawns(azureBaseMaterial, emberBaseMaterial, markerMaterial, healthBackgroundMaterial, healthFillMaterial);
            BuildBoundary(boundaryMaterial);

            UnitDefinition azureDefinition = CreateOrLoadUnitDefinition(
                "Assets/Data/AzureVanguard.asset", 520f, 5.5f, 48f, 2.2f, 0.8f);
            UnitDefinition emberDefinition = CreateOrLoadUnitDefinition(
                "Assets/Data/EmberTarget.asset", 180f, 4.2f, 30f, 1.8f, 0.5f);
            ItemCatalog itemCatalog = CreateShopCatalog();
            AbilityDefinition[] abilityKit = CreateHeroAbilityKit();
            CreateShopZone("Azure Shop", AzureBaseCenter + new Vector3(9f, 0.05f, 9f), TeamId.Azure, itemCatalog, azureBaseMaterial);
            CreateShopZone("Ember Shop", EmberBaseCenter + new Vector3(-9f, 0.05f, -9f), TeamId.Ember, itemCatalog, emberBaseMaterial);

            GameObject azure = CreateUnit(
                "Azure Vanguard", AzureBaseCenter + new Vector3(9f, 1f, 9f), TeamId.Azure,
                azureMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, azureDefinition, abilityKit, startSelected: true);
            azure.AddComponent<NavPathProbe>();

            GameObject ember = CreateUnit(
                "Ember Skirmisher", EmberBaseCenter + new Vector3(-9f, 1f, -9f), TeamId.Ember,
                emberMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, emberDefinition, abilityKit, startSelected: false);

            BuildLaneCreeps(azureMaterial, emberMaterial, healthBackgroundMaterial, healthFillMaterial);
            BuildNeutralCamps(neutralMaterial, healthBackgroundMaterial, healthFillMaterial);
            BuildBossPit(obstacleMaterial, riverMaterial);
            BuildMajorObjective(neutralMaterial, healthBackgroundMaterial, healthFillMaterial);

            CreateNavMeshBootstrap();
            CreateLighting();
            CreateMobaCamera(azure.transform);
            CreateCommandController();
            CreateNetworkArenaBootstrap(matchController, azure, ember);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();

            EditorUtility.DisplayDialog(
                "Cierzo Arena",
                $"MOBA greybox arena created at {ScenePath}.\n\n" +
                "Press Play, select Azure (left click) and right-click along the top / mid / bottom " +
                "lanes, across the three bridges and toward Ember to exercise long-range navigation. " +
                "Watch the Console for [NavScale] path logs.",
                "OK");
        }

        // ----- Ground + river -------------------------------------------------

        private static void BuildGroundAndRiver(Material groundMaterial, Material riverMaterial)
        {
            GameObject terrain = new GameObject("Terrain");

            // One continuous walkable ground slab covering the whole square arena, so
            // the NavMesh is a single connected surface (no gap anywhere). It tucks
            // under the axis-aligned boundary walls (inner face at +/-82), which carve
            // the walkable edge cleanly. The river is now a distinct *walkable* zone.
            CreateGroundBox(terrain.transform, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(176f, 1f, 176f), 0f, groundMaterial);

            // River as a thin, walkable visual ribbon on the NW-SE anti-diagonal, laid
            // flat on top of the ground (not sunken). It is on the default layer with no
            // collider, so it never affects the NavMesh: units walk straight over it.
            // The darker material and the boss pit give it its distinct, shadowed read.
            GameObject river = GameObject.CreatePrimitive(PrimitiveType.Cube);
            river.name = "River (visual)";
            river.layer = 0;
            river.transform.SetParent(terrain.transform);
            river.transform.position = new Vector3(0f, 0.05f, 0f);
            river.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            river.transform.localScale = new Vector3(210f, 0.1f, 18f);
            river.GetComponent<Renderer>().sharedMaterial = riverMaterial;
            Object.DestroyImmediate(river.GetComponent<Collider>());
        }

        private static void BuildBridges(Material bridgeMaterial)
        {
            GameObject bridges = new GameObject("Bridges");

            // One highlighted crossing per lane where that lane meets the river: the top
            // lane at the NW corner, the mid lane at the centre, and the bottom lane at
            // the SE corner. With the river now walkable these are visual emphasis /
            // intended chokepoints, not the only way across. The 45-degree yaw aligns
            // each band with the river.
            CreateGroundBox(bridges.transform, "Top Bridge", new Vector3(-60f, -0.4f, 60f), new Vector3(22f, 1f, 26f), 45f, bridgeMaterial);
            CreateGroundBox(bridges.transform, "Mid Bridge", new Vector3(0f, -0.4f, 0f), new Vector3(16f, 1f, 26f), 45f, bridgeMaterial);
            CreateGroundBox(bridges.transform, "Bottom Bridge", new Vector3(60f, -0.4f, -60f), new Vector3(22f, 1f, 26f), 45f, bridgeMaterial);
        }

        // ----- Lanes (visual ribbons) ----------------------------------------

        private static void BuildLanes(Material routeMaterial, Material routeMidMaterial)
        {
            GameObject lanes = new GameObject("Lanes");

            // Classic MOBA topology with Azure (SW) and Ember (NE) in opposite corners:
            //   - Mid lane : the direct SW->NE diagonal (primary: wider + brighter),
            //                crossing the river at the centre.
            //   - Top lane : hugs the WEST edge up to the NW corner, then the NORTH
            //                edge across to Ember (an "L"), crossing at the NW corner.
            //   - Bottom lane: hugs the SOUTH edge to the SE corner, then the EAST edge
            //                up to Ember (a mirrored "L"), crossing at the SE corner.
            Vector3 azure = new Vector3(AzureBaseCenter.x, 0.06f, AzureBaseCenter.z);
            Vector3 ember = new Vector3(EmberBaseCenter.x, 0.06f, EmberBaseCenter.z);
            Vector3 nw = new Vector3(NorthWestCorner.x, 0.06f, NorthWestCorner.z);
            Vector3 se = new Vector3(SouthEastCorner.x, 0.06f, SouthEastCorner.z);

            CreateLaneSegment(lanes.transform, "Mid Lane", azure, ember, routeMidMaterial, 9f);

            CreateLaneSegment(lanes.transform, "Top Lane West", azure, nw, routeMaterial, 6f);
            CreateLaneSegment(lanes.transform, "Top Lane North", nw, ember, routeMaterial, 6f);

            CreateLaneSegment(lanes.transform, "Bottom Lane South", azure, se, routeMaterial, 6f);
            CreateLaneSegment(lanes.transform, "Bottom Lane East", se, ember, routeMaterial, 6f);
        }

        private static void CreateLaneSegment(Transform parent, string name, Vector3 from, Vector3 to, Material material, float width)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            float length = delta.magnitude;
            if (length < 0.01f)
            {
                return;
            }

            Vector3 dir = delta / length;
            float yawDeg = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;

            GameObject ribbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ribbon.name = name;
            ribbon.layer = 0;
            ribbon.transform.SetParent(parent);
            ribbon.transform.position = new Vector3((from.x + to.x) * 0.5f, 0.06f, (from.z + to.z) * 0.5f);
            ribbon.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            ribbon.transform.localScale = new Vector3(length, 0.04f, width);
            ribbon.GetComponent<Renderer>().sharedMaterial = material;
            // Visual only: never block navigation or clicks-to-move on the ground.
            Object.DestroyImmediate(ribbon.GetComponent<Collider>());
        }

        // ----- Bases ----------------------------------------------------------

        private static void BuildBases(Material azureBaseMaterial, Material emberBaseMaterial, Material markerMaterial, Material healthBackgroundMaterial, Material healthFillMaterial)
        {
            GameObject bases = new GameObject("Bases");

            BuildBase(bases.transform, "Azure Base", AzureBaseCenter, TeamId.Azure, azureBaseMaterial, markerMaterial, healthBackgroundMaterial, healthFillMaterial);
            BuildBase(bases.transform, "Ember Base", EmberBaseCenter, TeamId.Ember, emberBaseMaterial, markerMaterial, healthBackgroundMaterial, healthFillMaterial);
        }

        private static void BuildBase(Transform parent, string name, Vector3 center, TeamId team, Material baseMaterial, Material markerMaterial, Material healthBackgroundMaterial, Material healthFillMaterial)
        {
            GameObject baseRoot = new GameObject(name);
            baseRoot.transform.SetParent(parent);

            // Team-coloured diamond platform in the base corner, flush with the ground,
            // so the whole footprint reads as "this team's base" from the isometric
            // camera. It is the largest, most dominant structure on the map by design.
            CreateGroundBox(baseRoot.transform, name + " Platform", new Vector3(center.x, -0.30f, center.z), new Vector3(30f, 1f, 30f), 45f, baseMaterial);

            // Tall solid central core structure (the nucleus to defend in later
            // milestones). Kept high so "this is the base" is unmistakable at play zoom.
            // The three lanes leave the base around this core, so it never blocks exits.
            GameObject core = CreateObstacle(baseRoot.transform, name + " Core", new Vector3(center.x, 7f, center.z), new Vector3(9f, 14f, 9f), baseMaterial, 45f);
            ConfigureStructure(core, team, StructureKind.Core, StructureLane.None, StructureTier.Core, 2000f, healthBackgroundMaterial, healthFillMaterial, 15.5f);

            // Bright cap crowning the core so the base nucleus pops from above.
            CreateMarker(baseRoot.transform, name + " Core Cap (marker)", new Vector3(center.x, 14.3f, center.z), markerMaterial, 6f);
        }

        // ----- Obstacles ------------------------------------------------------

        private static void BuildObstacles(Material obstacleMaterial)
        {
            GameObject obstacles = new GameObject("Obstacles");

            // Obstacles fill the four jungle triangles between the mid lane and the two
            // side lanes (never on a lane), so units must weave through jungle without
            // ever losing the walkable lanes. They sit clear of the river band and the
            // boss pit too.
            //   - Azure side (SW half, x + z < 0): north and south jungles.
            //   - Ember side (NE half, x + z > 0): north and south jungles.
            CreateObstacle(obstacles.transform, "Azure North Jungle", new Vector3(-44f, 3f, 6f), new Vector3(11f, 6f, 11f), obstacleMaterial, 45f);
            CreateObstacle(obstacles.transform, "Azure South Jungle", new Vector3(6f, 3f, -44f), new Vector3(11f, 6f, 11f), obstacleMaterial, 45f);
            CreateObstacle(obstacles.transform, "Ember North Jungle", new Vector3(-6f, 3f, 44f), new Vector3(11f, 6f, 11f), obstacleMaterial, 45f);
            CreateObstacle(obstacles.transform, "Ember South Jungle", new Vector3(44f, 3f, -6f), new Vector3(11f, 6f, 11f), obstacleMaterial, 45f);
            CreateObstacle(obstacles.transform, "Azure Mid Pillar", new Vector3(-22f, 3f, -6f), new Vector3(9f, 6f, 9f), obstacleMaterial, 45f);
            CreateObstacle(obstacles.transform, "Ember Mid Pillar", new Vector3(22f, 3f, 6f), new Vector3(9f, 6f, 9f), obstacleMaterial, 45f);
        }

        // ----- Neutral zones --------------------------------------------------

        private static void BuildNeutralZones(Material neutralMaterial, Material markerMaterial)
        {
            GameObject neutral = new GameObject("Neutral Zones");

            // Two off-lane jungle camps, one per team's jungle (Azure north jungle and
            // Ember south jungle), each a small walkable coloured pad with low border
            // walls and a camp marker, so they read as neutral ground off the lanes.
            BuildNeutralPocket(neutral.transform, "Azure Jungle Camp", new Vector3(-34f, 0f, 24f), neutralMaterial, markerMaterial,
                new Vector3(-46f, 3f, 24f), new Vector3(-34f, 3f, 36f));
            BuildNeutralPocket(neutral.transform, "Ember Jungle Camp", new Vector3(34f, 0f, -24f), neutralMaterial, markerMaterial,
                new Vector3(46f, 3f, -24f), new Vector3(34f, 3f, -36f));
        }

        private static void BuildNeutralPocket(Transform parent, string name, Vector3 center, Material neutralMaterial, Material markerMaterial, Vector3 wallA, Vector3 wallB)
        {
            GameObject pocket = new GameObject(name);
            pocket.transform.SetParent(parent);

            // Small walkable coloured pad, flush with the ground, marks the camp
            // footprint. Kept smaller and flatter than a base so it never competes
            // with the two team bases for visual dominance.
            CreateGroundBox(pocket.transform, name + " Pad", new Vector3(center.x, -0.28f, center.z), new Vector3(15f, 1f, 15f), 45f, neutralMaterial);

            // Low border walls on the outer flank, leaving the lane-facing side open.
            CreateObstacle(pocket.transform, name + " Wall A", wallA, new Vector3(13f, 3f, 3f), neutralMaterial, 45f);
            CreateObstacle(pocket.transform, name + " Wall B", wallB, new Vector3(3f, 3f, 13f), neutralMaterial, 45f);

            CreateMarker(pocket.transform, name + " Marker", new Vector3(center.x, 0.5f, center.z), markerMaterial, 3f);
        }

        // ----- Towers + spawn points -----------------------------------------

        private static void BuildTowersAndSpawns(Material azureMaterial, Material emberMaterial, Material capMaterial, Material healthBackgroundMaterial, Material healthFillMaterial)
        {
            GameObject towers = new GameObject("Towers");
            GameObject spawns = new GameObject("Spawn Points");

            // Each lane is a polyline from Azure (SW) to Ember (NE). Towers and spawns
            // are placed by fraction of the lane's arc-length, so they follow the
            // bends of the L-shaped side lanes and mirror perfectly between the teams.
            Vector3[] midLane = { AzureBaseCenter, EmberBaseCenter };
            Vector3[] topLane = { AzureBaseCenter, NorthWestCorner, EmberBaseCenter };
            Vector3[] botLane = { AzureBaseCenter, SouthEastCorner, EmberBaseCenter };
            (StructureLane Lane, Vector3[] Path)[] lanes =
            {
                (StructureLane.Top, topLane),
                (StructureLane.Mid, midLane),
                (StructureLane.Bottom, botLane),
            };

            // Three tiers per lane and team: gate (near base), inner, and outer (near
            // the river). Azure measures from the start, Ember mirrors from the end.
            // Kept back from the river so the two teams' outer towers can never be in
            // range of each other across the crossing (outer at 0.40 => 20% mid gap).
            float[] towerFraction = { 0.16f, 0.28f, 0.40f };
            string[] towerTier = { "Gate", "Inner", "Outer" };
            const float spawnFraction = 0.09f;

            foreach ((StructureLane lane, Vector3[] path) in lanes)
            {
                for (int i = 0; i < towerFraction.Length; i++)
                {
                    StructureTier tier = i == 0 ? StructureTier.Gate : i == 1 ? StructureTier.Inner : StructureTier.Outer;
                    CreateTowerMarker(towers.transform, $"Azure {lane} Tower ({towerTier[i]})", PointAlong(path, towerFraction[i]), TeamId.Azure, lane, tier, azureMaterial, capMaterial, healthBackgroundMaterial, healthFillMaterial);
                    CreateTowerMarker(towers.transform, $"Ember {lane} Tower ({towerTier[i]})", PointAlong(path, 1f - towerFraction[i]), TeamId.Ember, lane, tier, emberMaterial, capMaterial, healthBackgroundMaterial, healthFillMaterial);
                }

                CreateSpawnPoint(spawns.transform, $"Azure {lane} Spawn", PointAlong(path, spawnFraction), azureMaterial, capMaterial);
                CreateSpawnPoint(spawns.transform, $"Ember {lane} Spawn", PointAlong(path, 1f - spawnFraction), emberMaterial, capMaterial);
            }
        }

        // Returns the point at the given fraction (0..1) of a polyline's total length.
        private static Vector3 PointAlong(Vector3[] path, float fraction)
        {
            float total = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                total += Vector3.Distance(path[i], path[i + 1]);
            }

            float target = Mathf.Clamp01(fraction) * total;
            float walked = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                float segment = Vector3.Distance(path[i], path[i + 1]);
                if (walked + segment >= target)
                {
                    float t = segment < 0.001f ? 0f : (target - walked) / segment;
                    Vector3 p = Vector3.Lerp(path[i], path[i + 1], t);
                    return new Vector3(p.x, 0f, p.z);
                }

                walked += segment;
            }

            Vector3 last = path[path.Length - 1];
            return new Vector3(last.x, 0f, last.z);
        }

        private static void CreateTowerMarker(Transform parent, string name, Vector3 groundPos, TeamId team, StructureLane lane, StructureTier tier, Material teamMaterial, Material capMaterial, Material healthBackgroundMaterial, Material healthFillMaterial)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = groundPos;

            // Team-coloured footpad so tower ownership reads on the ground.
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = name + " Pad";
            pad.layer = 0;
            pad.transform.SetParent(root.transform);
            pad.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            pad.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            pad.transform.localScale = new Vector3(6f, 0.12f, 6f);
            pad.GetComponent<Renderer>().sharedMaterial = teamMaterial;
            Object.DestroyImmediate(pad.GetComponent<Collider>());

            // Tall shaft. Visual-only (layer 0, no collider) so tower markers never
            // block navigation; they only communicate where towers will stand.
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = name + " Shaft";
            shaft.layer = 0;
            shaft.transform.SetParent(root.transform);
            shaft.transform.localPosition = new Vector3(0f, 4f, 0f);
            shaft.transform.localScale = new Vector3(3f, 4f, 3f);
            shaft.GetComponent<Renderer>().sharedMaterial = teamMaterial;
            Object.DestroyImmediate(shaft.GetComponent<Collider>());

            // Bright cap so tower tops pop from the isometric camera.
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = name + " Cap";
            cap.layer = 0;
            cap.transform.SetParent(root.transform);
            cap.transform.localPosition = new Vector3(0f, 8.2f, 0f);
            cap.transform.localScale = new Vector3(3.6f, 0.4f, 3.6f);
            cap.GetComponent<Renderer>().sharedMaterial = capMaterial;
            Object.DestroyImmediate(cap.GetComponent<Collider>());

            CreateTowerNavigationBlocker(root.transform);
            ConfigureStructure(root, team, StructureKind.Tower, lane, tier, 650f, healthBackgroundMaterial, healthFillMaterial, 9.2f);
            TowerController tower = root.AddComponent<TowerController>();
            root.AddComponent<DefensiveAggroResponder>();
            // Tuned for the one-hero local validation scene: Azure can destroy a
            // tower while it is firing, making the destruction/victory path testable
            // without a second local player or temporary cheats.
            SetFloat(tower, "searchInterval", 0.2f);
            // Towers defend against heroes (Selectable) and lane creeps (Default).
            SetInt(tower, "targetMask", ~0);
            ConfigureAttack(root.GetComponent<BasicAttack>(), AttackDelivery.Ranged, 9f, 20f, 1f, 0.35f, 0.35f);
            AttackVisual towerVisual = root.AddComponent<AttackVisual>();
            SetObjectReference(towerVisual, "targetRenderer", shaft.GetComponent<Renderer>());
        }

        private static void CreateSpawnPoint(Transform parent, string name, Vector3 groundPos, Material teamMaterial, Material capMaterial)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = groundPos;

            // Flat team-coloured pad marking one of the base's three lane accesses.
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = name + " Pad";
            pad.layer = 0;
            pad.transform.SetParent(root.transform);
            pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            pad.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            pad.transform.localScale = new Vector3(8f, 0.1f, 8f);
            pad.GetComponent<Renderer>().sharedMaterial = teamMaterial;
            Object.DestroyImmediate(pad.GetComponent<Collider>());

            // Central pip so the spawn origin is unmistakable from above.
            CreateMarker(root.transform, name + " Pip", groundPos + new Vector3(0f, 0.35f, 0f), capMaterial, 2.4f);
        }

        // ----- Boundary -------------------------------------------------------

        private static void BuildBoundary(Material boundaryMaterial)
        {
            GameObject boundary = new GameObject("Boundary");

            // Axis-aligned containment ring around the square arena. Tall Ground-layer
            // walls so the NavMesh ends cleanly at the play-area edge.
            CreateObstacle(boundary.transform, "Boundary North", new Vector3(0f, 3f, 84f), new Vector3(180f, 6f, 4f), boundaryMaterial);
            CreateObstacle(boundary.transform, "Boundary South", new Vector3(0f, 3f, -84f), new Vector3(180f, 6f, 4f), boundaryMaterial);
            CreateObstacle(boundary.transform, "Boundary East", new Vector3(84f, 3f, 0f), new Vector3(4f, 6f, 180f), boundaryMaterial);
            CreateObstacle(boundary.transform, "Boundary West", new Vector3(-84f, 3f, 0f), new Vector3(4f, 6f, 180f), boundaryMaterial);
        }

        // ----- Primitive helpers ---------------------------------------------

        private static void CreateGroundBox(Transform parent, string name, Vector3 center, Vector3 size, float yaw, Material material)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = name;
            ground.layer = GroundLayer;
            ground.transform.SetParent(parent);
            ground.transform.position = center;
            ground.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            ground.transform.localScale = size;
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static GameObject CreateObstacle(Transform parent, string name, Vector3 center, Vector3 size, Material material, float yaw = 0f)
        {
            // Tall cubes on the Ground layer: their steep sides exceed the agent slope,
            // so the NavMesh carves out their footprint and the agent routes around.
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = name;
            obstacle.layer = GroundLayer;
            obstacle.transform.SetParent(parent);
            obstacle.transform.position = center;
            obstacle.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            obstacle.transform.localScale = size;
            obstacle.GetComponent<Renderer>().sharedMaterial = material;
            return obstacle;
        }

        private static void CreateTowerNavigationBlocker(Transform tower)
        {
            // The old M3C marker intentionally had no collider. M5 towers are real
            // structures: include a compact Ground-layer blocker before the runtime
            // NavMesh bake so units route around them instead of walking through.
            GameObject blocker = new GameObject("Navigation Blocker");
            blocker.layer = GroundLayer;
            blocker.transform.SetParent(tower);
            blocker.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            CapsuleCollider collider = blocker.AddComponent<CapsuleCollider>();
            collider.radius = 1.45f;
            collider.height = 7f;

            // NavMeshAgent does not collide physically with a CapsuleCollider. A
            // carved obstacle is therefore the authoritative navigation block and
            // keeps routes outside the tower footprint after the runtime bake.
            NavMeshObstacle obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = collider.radius;
            obstacle.height = collider.height;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }

        private static void CreateMarker(Transform parent, string name, Vector3 position, Material material, float diameter)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.layer = 0;
            marker.transform.SetParent(parent);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(diameter, 0.4f, diameter);
            marker.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        // ----- Units ----------------------------------------------------------

        private static GameObject CreateUnit(string name, Vector3 position, TeamId team, Material bodyMaterial, Material ringMaterial, Material healthBackgroundMaterial, Material healthFillMaterial, UnitDefinition definition, AbilityDefinition[] abilityKit, bool startSelected)
        {
            GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unit.name = name;
            unit.layer = SelectableLayer;
            unit.transform.position = position;
            unit.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            UnitDefinitionProvider definitionProvider = unit.AddComponent<UnitDefinitionProvider>();
            SetObjectReference(definitionProvider, "definition", definition);

            TeamMember teamMember = unit.AddComponent<TeamMember>();
            SetEnum(teamMember, "team", (int)team);
            unit.AddComponent<HeroUnit>();
            HeroMatchIdentity matchIdentity = unit.AddComponent<HeroMatchIdentity>();
            matchIdentity.Configure(0, name);
            unit.AddComponent<HeroMatchStatistics>();
            unit.AddComponent<VisionSource>();
            unit.AddComponent<VisionVisibility>();

            Health health = unit.AddComponent<Health>();
            SetFloat(health, "maxHealth", definition.MaxHealth);

            DamageFlash damageFlash = unit.AddComponent<DamageFlash>();
            SetObjectReference(damageFlash, "targetRenderer", unit.GetComponent<Renderer>());

            unit.AddComponent<DamageNumberSpawner>();
            CreateHealthBar(unit.transform, health, healthBackgroundMaterial, healthFillMaterial);

            // Both units are fully controllable so a distant target can be moved
            // manually to exercise dynamic long-range pursuit.
            unit.AddComponent<ClickMover>();
            BasicAttack attack = unit.AddComponent<BasicAttack>();
            ConfigureAttack(attack, team == TeamId.Azure ? AttackDelivery.Melee : AttackDelivery.Ranged,
                team == TeamId.Azure ? 3.25f : 7f,
                definition.AttackDamage,
                team == TeamId.Azure ? 1.25f : 1.4f,
                0.3f,
                0.35f);
            AttackVisual attackVisual = unit.AddComponent<AttackVisual>();
            SetObjectReference(attackVisual, "targetRenderer", unit.GetComponent<Renderer>());
            unit.AddComponent<UnitOrderController>();
            HeroProgression progression = unit.AddComponent<HeroProgression>();
            ConfigureHeroProgression(progression);
            ExperienceReward heroReward = unit.AddComponent<ExperienceReward>();
            SetInt(heroReward, "experienceReward", 300);
            SetInt(heroReward, "goldReward", 0);
            unit.AddComponent<HeroEconomy>();
            unit.AddComponent<HeroInventory>();
            unit.AddComponent<HeroMana>();
            unit.AddComponent<StatusEffectController>();
            unit.AddComponent<StatusEffectFeedback>();
            HeroAbilities heroAbilities = unit.AddComponent<HeroAbilities>();
            SetObjectArray(heroAbilities, "abilities", abilityKit);
            unit.AddComponent<HeroProgressionFeedback>();
            unit.AddComponent<HeroShopFeedback>();
            unit.AddComponent<HeroAbilitiesFeedback>();
            unit.AddComponent<HeroLifeCycle>();
            unit.AddComponent<HeroRespawnFeedback>();

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Selection Ring";
            ring.layer = SelectableLayer;
            ring.transform.SetParent(unit.transform);
            ring.transform.localPosition = new Vector3(0f, -0.72f, 0f);
            ring.transform.localScale = new Vector3(1.35f, 0.03f, 1.35f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMaterial;
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            SelectableUnit selectableUnit = unit.AddComponent<SelectableUnit>();
            SetObjectReference(selectableUnit, "selectionRing", ring.GetComponent<Renderer>());
            selectableUnit.SetSelected(startSelected);

            DeathVisibility deathVisibility = unit.AddComponent<DeathVisibility>();
            SerializedObject deathObject = new SerializedObject(deathVisibility);
            SerializedProperty renderers = deathObject.FindProperty("renderersToDisable");
            renderers.arraySize = 2;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = unit.GetComponent<Renderer>();
            renderers.GetArrayElementAtIndex(1).objectReferenceValue = ring.GetComponent<Renderer>();
            SerializedProperty colliders = deathObject.FindProperty("collidersToDisable");
            colliders.arraySize = 1;
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = unit.GetComponent<Collider>();
            deathObject.ApplyModifiedPropertiesWithoutUndo();

            return unit;
        }

        private static void CreateHeroSpawnPoint(string name, Vector3 position, TeamId team)
        {
            GameObject spawnObject = new GameObject(name);
            spawnObject.transform.position = position;
            HeroSpawnPoint spawn = spawnObject.AddComponent<HeroSpawnPoint>();
            spawn.SetTeam(team);
        }

        // ----- M7 lane creeps ------------------------------------------------

        private static void BuildNeutralCamps(Material neutralMaterial, Material healthBackgroundMaterial, Material healthFillMaterial)
        {
            EnsureFolder("Assets", "Prefabs"); EnsureFolder("Assets/Prefabs", "Neutrals");
            GameObject small=CreateNeutralPrefab("NeutralSmallMelee",NeutralCampCategory.Small,PrimitiveType.Capsule,neutralMaterial,healthBackgroundMaterial,healthFillMaterial,260f,22f,1.9f,1.15f,70,45);
            GameObject medium=CreateNeutralPrefab("NeutralMediumRanged",NeutralCampCategory.Medium,PrimitiveType.Sphere,neutralMaterial,healthBackgroundMaterial,healthFillMaterial,330f,28f,5.8f,1.35f,90,60);
            GameObject large=CreateNeutralPrefab("NeutralLargeBrute",NeutralCampCategory.Large,PrimitiveType.Cube,neutralMaterial,healthBackgroundMaterial,healthFillMaterial,620f,42f,2.1f,1.5f,150,95);
            GameObject root=new GameObject("Neutral Jungle Camps");
            CreateNeutralCamp(root.transform,"azure.small.north",new Vector3(-38f,0f,26f),new[]{new NeutralSpawnEntry(NeutralCampCategory.Small,small,Vector3.zero,2)});
            CreateNeutralCamp(root.transform,"azure.small.south",new Vector3(4f,0f,-38f),new[]{new NeutralSpawnEntry(NeutralCampCategory.Small,small,Vector3.zero,2)});
            CreateNeutralCamp(root.transform,"azure.medium.north",new Vector3(-48f,0f,12f),new[]{new NeutralSpawnEntry(NeutralCampCategory.Medium,medium,Vector3.zero,1),new NeutralSpawnEntry(NeutralCampCategory.Small,small,new Vector3(2f,0f,1f),1)});
            CreateNeutralCamp(root.transform,"azure.medium.south",new Vector3(16f,0f,-48f),new[]{new NeutralSpawnEntry(NeutralCampCategory.Medium,medium,Vector3.zero,1),new NeutralSpawnEntry(NeutralCampCategory.Small,small,new Vector3(-2f,0f,1f),1)});
            CreateNeutralCamp(root.transform,"azure.large",new Vector3(-26f,0f,38f),new[]{new NeutralSpawnEntry(NeutralCampCategory.Large,large,Vector3.zero,1)});
            CreateNeutralCamp(root.transform,"ember.small.north",new Vector3(-4f,0f,38f),new[]{new NeutralSpawnEntry(NeutralCampCategory.Small,small,Vector3.zero,2)});
            CreateNeutralCamp(root.transform,"ember.small.south",new Vector3(38f,0f,-26f),new[]{new NeutralSpawnEntry(NeutralCampCategory.Small,small,Vector3.zero,2)});
            CreateNeutralCamp(root.transform,"ember.medium.north",new Vector3(-16f,0f,48f),new[]{new NeutralSpawnEntry(NeutralCampCategory.Medium,medium,Vector3.zero,1),new NeutralSpawnEntry(NeutralCampCategory.Small,small,new Vector3(2f,0f,-1f),1)});
            CreateNeutralCamp(root.transform,"ember.medium.south",new Vector3(48f,0f,-12f),new[]{new NeutralSpawnEntry(NeutralCampCategory.Medium,medium,Vector3.zero,1),new NeutralSpawnEntry(NeutralCampCategory.Small,small,new Vector3(-2f,0f,-1f),1)});
            CreateNeutralCamp(root.transform,"ember.large",new Vector3(26f,0f,-38f),new[]{new NeutralSpawnEntry(NeutralCampCategory.Large,large,Vector3.zero,1)});
        }

        private static void CreateNeutralCamp(Transform parent,string id,Vector3 position,NeutralSpawnEntry[] composition)
        {
            GameObject item=new GameObject($"Camp {id}");item.transform.SetParent(parent);item.transform.position=position;NeutralCamp camp=item.AddComponent<NeutralCamp>();camp.Configure(id,composition,8f,15f,1.5f,35f);
        }

        private static GameObject CreateNeutralPrefab(string assetName,NeutralCampCategory category,PrimitiveType primitive,Material material,Material healthBackgroundMaterial,Material healthFillMaterial,float maxHealth,float damage,float range,float interval,int experience,int gold)
        {
            string path=$"Assets/Prefabs/Neutrals/{assetName}.prefab";GameObject neutral=GameObject.CreatePrimitive(primitive);neutral.name=assetName;neutral.layer=AttackableLayer;neutral.transform.localScale=category==NeutralCampCategory.Large?Vector3.one*1.15f:Vector3.one*.72f;neutral.GetComponent<Renderer>().sharedMaterial=material;
            TeamMember member=neutral.AddComponent<TeamMember>();SetEnum(member,"team",(int)TeamId.Neutral);Health health=neutral.AddComponent<Health>();SetFloat(health,"maxHealth",maxHealth);neutral.AddComponent<StatusEffectController>();neutral.AddComponent<VisionVisibility>();CreateHealthBar(neutral.transform,health,healthBackgroundMaterial,healthFillMaterial,category==NeutralCampCategory.Large?2.3f:1.65f,1.1f);neutral.AddComponent<ClickMover>();BasicAttack attack=neutral.AddComponent<BasicAttack>();ConfigureAttack(attack,range>3f?AttackDelivery.Ranged:AttackDelivery.Melee,range,damage,interval,.25f,.3f);AttackVisual visual=neutral.AddComponent<AttackVisual>();SetObjectReference(visual,"targetRenderer",neutral.GetComponent<Renderer>());neutral.AddComponent<NeutralUnitController>();ExperienceReward reward=neutral.AddComponent<ExperienceReward>();SetInt(reward,"experienceReward",experience);SetFloat(reward,"experienceRadius",14f);SetInt(reward,"goldReward",gold);SetBool(reward,"shareExperienceWithNearbyHeroes",true);PrefabUtility.SaveAsPrefabAsset(neutral,path);Object.DestroyImmediate(neutral);return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void BuildBossPit(Material wallMaterial, Material riverMaterial)
        {
            GameObject pit = new GameObject("Boss Pit");

            // Shadowed pit floor: a small dark, flush, walkable pad centred on the boss,
            // on the default layer with no collider so it never blocks navigation.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Boss Pit Floor (visual)";
            floor.layer = 0;
            floor.transform.SetParent(pit.transform);
            floor.transform.position = new Vector3(BossPitCenter.x, 0.06f, BossPitCenter.z);
            floor.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            floor.transform.localScale = new Vector3(13f, 0.12f, 13f);
            floor.GetComponent<Renderer>().sharedMaterial = riverMaterial;
            Object.DestroyImmediate(floor.GetComponent<Collider>());

            // Two flank walls along the river axis (u = NW-SE), leaving both base-facing
            // sides (across the river) open. They are mirror-symmetric across the z = -x
            // axis, so the Azure (SW) and Ember (NE) approaches into the pit are
            // identical, and they give exactly two entrances (never a single choke).
            Vector3 u = new Vector3(0.70710678f, 0f, -0.70710678f);
            Vector3 nw = BossPitCenter + u * 9f;
            Vector3 se = BossPitCenter - u * 9f;
            CreateObstacle(pit.transform, "Boss Pit Wall NW", new Vector3(nw.x, 2.5f, nw.z), new Vector3(3f, 5f, 10f), wallMaterial, 45f);
            CreateObstacle(pit.transform, "Boss Pit Wall SE", new Vector3(se.x, 2.5f, se.z), new Vector3(3f, 5f, 10f), wallMaterial, 45f);
        }

        private static void BuildMajorObjective(Material material,Material healthBackgroundMaterial,Material healthFillMaterial)
        {
            GameObject boss=GameObject.CreatePrimitive(PrimitiveType.Cylinder);boss.name="Cierzo Guardian";boss.layer=AttackableLayer;boss.transform.position=BossPitCenter;boss.transform.localScale=Vector3.one*1.6f;boss.GetComponent<Renderer>().sharedMaterial=material;TeamMember member=boss.AddComponent<TeamMember>();SetEnum(member,"team",(int)TeamId.Neutral);Health health=boss.AddComponent<Health>();SetFloat(health,"maxHealth",2800f);boss.AddComponent<StatusEffectController>();boss.AddComponent<BossAnnouncementFeedback>();boss.AddComponent<VisionVisibility>();CreateHealthBar(boss.transform,health,healthBackgroundMaterial,healthFillMaterial,3.2f,2.5f);boss.AddComponent<ClickMover>();BasicAttack attack=boss.AddComponent<BasicAttack>();ConfigureAttack(attack,AttackDelivery.Melee,2.5f,70f,1.35f,.35f,.35f);AttackVisual visual=boss.AddComponent<AttackVisual>();SetObjectReference(visual,"targetRenderer",boss.GetComponent<Renderer>());GameObject telegraph=GameObject.CreatePrimitive(PrimitiveType.Cylinder);telegraph.name="Guardian Strike Telegraph";telegraph.transform.SetParent(boss.transform);telegraph.transform.localPosition=new Vector3(0f,-.95f,0f);telegraph.transform.localScale=new Vector3(5f,.02f,5f);telegraph.GetComponent<Renderer>().sharedMaterial=CreateMaterial("Assets/Materials/Prototype_BossTelegraph.mat",new Color(.95f,.25f,.08f,.45f));Object.DestroyImmediate(telegraph.GetComponent<Collider>());telegraph.GetComponent<Renderer>().enabled=false;NeutralBossController controller=boss.AddComponent<NeutralBossController>();controller.Configure(boss.transform.position,14f,22f,1.5f,180f);SerializedObject data=new SerializedObject(controller);data.FindProperty("telegraphRenderer").objectReferenceValue=telegraph.GetComponent<Renderer>();SerializedProperty renders=data.FindProperty("presentationRenderers");renders.arraySize=1;renders.GetArrayElementAtIndex(0).objectReferenceValue=boss.GetComponent<Renderer>();SerializedProperty colliders=data.FindProperty("presentationColliders");colliders.arraySize=1;colliders.GetArrayElementAtIndex(0).objectReferenceValue=boss.GetComponent<Collider>();data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildLaneCreeps(Material azureMaterial, Material emberMaterial, Material healthBackgroundMaterial, Material healthFillMaterial)
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Creeps");
            GameObject azureMelee = CreateCreepPrefab("AzureMeleeCreep", TeamId.Azure, CreepArchetype.Melee, azureMaterial, healthBackgroundMaterial, healthFillMaterial);
            GameObject azureRanged = CreateCreepPrefab("AzureRangedCreep", TeamId.Azure, CreepArchetype.Ranged, azureMaterial, healthBackgroundMaterial, healthFillMaterial);
            GameObject emberMelee = CreateCreepPrefab("EmberMeleeCreep", TeamId.Ember, CreepArchetype.Melee, emberMaterial, healthBackgroundMaterial, healthFillMaterial);
            GameObject emberRanged = CreateCreepPrefab("EmberRangedCreep", TeamId.Ember, CreepArchetype.Ranged, emberMaterial, healthBackgroundMaterial, healthFillMaterial);

            GameObject root = new GameObject("Lane Creeps");
            GameObject routes = new GameObject("Lane Routes");
            routes.transform.SetParent(root.transform);
            Vector3[] top = { AzureBaseCenter, NorthWestCorner, EmberBaseCenter };
            Vector3[] mid = { AzureBaseCenter, EmberBaseCenter };
            Vector3[] bottom = { AzureBaseCenter, SouthEastCorner, EmberBaseCenter };
            const float spawnFraction = 0.09f;
            LaneRoute topAzure = CreateRoute(routes.transform, "Top Azure to Ember", new[] { PointAlong(top, spawnFraction), NorthWestCorner, PointAlong(top, 1f - spawnFraction) }, Color.cyan);
            LaneRoute midAzure = CreateRoute(routes.transform, "Mid Azure to Ember", new[] { PointAlong(mid, spawnFraction), Vector3.zero, PointAlong(mid, 1f - spawnFraction) }, Color.cyan);
            LaneRoute bottomAzure = CreateRoute(routes.transform, "Bottom Azure to Ember", new[] { PointAlong(bottom, spawnFraction), SouthEastCorner, PointAlong(bottom, 1f - spawnFraction) }, Color.cyan);
            LaneRoute topEmber = CreateRoute(routes.transform, "Top Ember to Azure", new[] { PointAlong(top, 1f - spawnFraction), NorthWestCorner, PointAlong(top, spawnFraction) }, Color.red);
            LaneRoute midEmber = CreateRoute(routes.transform, "Mid Ember to Azure", new[] { PointAlong(mid, 1f - spawnFraction), Vector3.zero, PointAlong(mid, spawnFraction) }, Color.red);
            LaneRoute bottomEmber = CreateRoute(routes.transform, "Bottom Ember to Azure", new[] { PointAlong(bottom, 1f - spawnFraction), SouthEastCorner, PointAlong(bottom, spawnFraction) }, Color.red);

            GameObject spawners = new GameObject("Wave Spawners");
            spawners.transform.SetParent(root.transform);
            CreateWaveSpawner(spawners.transform, "Azure Top Waves", topAzure, azureMelee, azureRanged);
            CreateWaveSpawner(spawners.transform, "Azure Mid Waves", midAzure, azureMelee, azureRanged);
            CreateWaveSpawner(spawners.transform, "Azure Bottom Waves", bottomAzure, azureMelee, azureRanged);
            CreateWaveSpawner(spawners.transform, "Ember Top Waves", topEmber, emberMelee, emberRanged);
            CreateWaveSpawner(spawners.transform, "Ember Mid Waves", midEmber, emberMelee, emberRanged);
            CreateWaveSpawner(spawners.transform, "Ember Bottom Waves", bottomEmber, emberMelee, emberRanged);
        }

        private static LaneRoute CreateRoute(Transform parent, string name, Vector3[] points, Color color)
        {
            GameObject routeObject = new GameObject(name);
            routeObject.transform.SetParent(parent);
            LaneRoute route = routeObject.AddComponent<LaneRoute>();
            Transform[] waypoints = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                GameObject point = new GameObject($"Waypoint {i + 1}");
                point.transform.SetParent(routeObject.transform);
                point.transform.position = points[i] + Vector3.up;
                waypoints[i] = point.transform;
            }

            SetObjectArray(route, "waypoints", waypoints);
            SerializedObject serialized = new SerializedObject(route);
            serialized.FindProperty("gizmoColor").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return route;
        }

        private static void CreateWaveSpawner(Transform parent, string name, LaneRoute route, GameObject melee, GameObject ranged)
        {
            GameObject source = new GameObject(name);
            source.transform.SetParent(parent);
            CreepWaveSpawner spawner = source.AddComponent<CreepWaveSpawner>();
            SetObjectReference(spawner, "route", route);
            SetObjectReference(spawner, "meleePrefab", melee);
            SetObjectReference(spawner, "rangedPrefab", ranged);
            SetFloat(spawner, "initialDelay", 4f);
            SetFloat(spawner, "waveInterval", 25f);
            SetInt(spawner, "meleeCount", 2);
            SetInt(spawner, "rangedCount", 1);
            SetFloat(spawner, "spawnSpacing", 1.3f);
        }

        private static GameObject CreateCreepPrefab(string assetName, TeamId team, CreepArchetype archetype, Material material, Material healthBackgroundMaterial, Material healthFillMaterial)
        {
            string path = $"Assets/Prefabs/Creeps/{assetName}.prefab";
            GameObject creep = GameObject.CreatePrimitive(archetype == CreepArchetype.Melee ? PrimitiveType.Capsule : PrimitiveType.Sphere);
            creep.name = assetName;
            creep.layer = AttackableLayer;
            creep.transform.localScale = archetype == CreepArchetype.Melee ? Vector3.one * 0.75f : Vector3.one * 0.65f;
            creep.GetComponent<Renderer>().sharedMaterial = material;
            TeamMember member = creep.AddComponent<TeamMember>();
            SetEnum(member, "team", (int)team);
            Health health = creep.AddComponent<Health>();
            SetFloat(health, "maxHealth", archetype == CreepArchetype.Melee ? 220f : 150f);
            creep.AddComponent<StatusEffectController>();
            creep.AddComponent<VisionSource>();
            creep.AddComponent<VisionVisibility>();
            CreateHealthBar(creep.transform, health, healthBackgroundMaterial, healthFillMaterial, 1.65f, 1.05f);
            creep.AddComponent<ClickMover>();
            BasicAttack attack = creep.AddComponent<BasicAttack>();
            ConfigureAttack(attack,
                archetype == CreepArchetype.Melee ? AttackDelivery.Melee : AttackDelivery.Ranged,
                archetype == CreepArchetype.Melee ? 1.8f : 6f,
                archetype == CreepArchetype.Melee ? 16f : 13f,
                archetype == CreepArchetype.Melee ? 1.1f : 1.35f,
                0.25f,
                0.3f);
            AttackVisual visual = creep.AddComponent<AttackVisual>();
            SetObjectReference(visual, "targetRenderer", creep.GetComponent<Renderer>());
            CreepController controller = creep.AddComponent<CreepController>();
            SetEnum(controller, "archetype", (int)archetype);
            SetFloat(controller, "detectionRange", archetype == CreepArchetype.Melee ? 6.5f : 8f);
            SetFloat(controller, "leashRange", 15f);
            creep.AddComponent<DefensiveAggroResponder>();
            ExperienceReward reward = creep.AddComponent<ExperienceReward>();
            SetInt(reward, "experienceReward", archetype == CreepArchetype.Melee ? 60 : 75);
            SetFloat(reward, "experienceRadius", 14f);
            SetInt(reward, "goldReward", archetype == CreepArchetype.Melee ? 40 : 55);
            SetBool(reward, "shareExperienceWithNearbyHeroes", true);
            PrefabUtility.SaveAsPrefabAsset(creep, path);
            Object.DestroyImmediate(creep);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void ConfigureHeroProgression(HeroProgression progression)
        {
            SetInt(progression, "startingLevel", 1);
            SetInt(progression, "maximumLevel", 10);
            SetInt(progression, "baseExperienceForNextLevel", 100);
            SetFloat(progression, "experienceGrowth", 1.25f);
            SetFloat(progression, "maximumHealthPerLevel", 80f);
            SetFloat(progression, "damagePerLevel", 8f);
            SetFloat(progression, "movementSpeedPerLevel", 0.2f);
        }

        private static void CreateHealthBar(Transform unit, Health health, Material backgroundMaterial, Material fillMaterial, float localHeight = 2.35f, float width = 1.5f, bool worldSpaceAnchor = false)
        {
            GameObject bar = new GameObject("Health Bar");
            bar.layer = 2;
            if (worldSpaceAnchor)
            {
                // Structures are static but cores use a large scaled transform. Keep
                // their UI in world units so it neither scales to a giant size nor
                // ends up hundreds of units above the core.
                bar.transform.SetParent(unit.parent);
                bar.transform.position = unit.position + new Vector3(0f, localHeight, 0f);
                bar.transform.rotation = Quaternion.identity;
            }
            else
            {
                bar.transform.SetParent(unit);
                bar.transform.localPosition = new Vector3(0f, localHeight, 0f);
                bar.transform.localRotation = Quaternion.identity;
            }

            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Health Bar Background";
            background.layer = 2;
            background.transform.SetParent(bar.transform);
            background.transform.localPosition = Vector3.zero;
            background.transform.localScale = new Vector3(width, 0.18f, 0.03f);
            background.GetComponent<Renderer>().sharedMaterial = backgroundMaterial;
            Object.DestroyImmediate(background.GetComponent<Collider>());

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Health Bar Fill";
            fill.layer = 2;
            fill.transform.SetParent(bar.transform);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            fill.transform.localScale = new Vector3(width, 0.12f, 0.03f);
            fill.GetComponent<Renderer>().sharedMaterial = fillMaterial;
            Object.DestroyImmediate(fill.GetComponent<Collider>());

            WorldHealthBar healthBar = bar.AddComponent<WorldHealthBar>();
            SerializedObject barObject = new SerializedObject(healthBar);
            barObject.FindProperty("health").objectReferenceValue = health;
            barObject.FindProperty("fill").objectReferenceValue = fill.transform;
            barObject.FindProperty("width").floatValue = width;
            barObject.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----- Multiplayer integration (M18) ---------------------------------

        private static void CreateNetworkArenaBootstrap(GameObject localMatch, GameObject azure, GameObject ember)
        {
            NetworkObject azurePrefab=LoadNetworkPrefab("Assets/Prefabs/Network/AzureVanguardNetwork.prefab");
            NetworkObject emberPrefab=LoadNetworkPrefab("Assets/Prefabs/Network/EmberSkirmisherNetwork.prefab");
            NetworkObject matchPrefab=LoadNetworkPrefab("Assets/Prefabs/Network/MatchStateNetwork.prefab");
            NetworkObject azureTower=LoadNetworkPrefab("Assets/Prefabs/Network/AzureTowerNetwork.prefab");
            NetworkObject emberTower=LoadNetworkPrefab("Assets/Prefabs/Network/EmberTowerNetwork.prefab");
            NetworkObject azureCore=LoadNetworkPrefab("Assets/Prefabs/Network/AzureCoreNetwork.prefab");
            NetworkObject emberCore=LoadNetworkPrefab("Assets/Prefabs/Network/EmberCoreNetwork.prefab");
            NetworkPrefabsList prefabs=AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/Data/SpikeNetworkPrefabs.asset");
            if(azurePrefab==null||emberPrefab==null||matchPrefab==null||prefabs==null)return;

            GameObject managerObject=new GameObject("Network Manager");
            NetworkManager manager=managerObject.AddComponent<NetworkManager>();
            UnityTransport transport=managerObject.AddComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",7777);transport.MaxPacketQueueSize=1024;
            manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=true,ConnectionApproval=true,SpawnTimeout=10f};
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs);
            GameObject projectileSpawnerObject=new GameObject("Network Projectile Spawner");
            NetworkProjectileSpawner projectileSpawner=projectileSpawnerObject.AddComponent<NetworkProjectileSpawner>();
            SetObjectReference(projectileSpawner,"projectilePrefab",AssetDatabase.LoadAssetAtPath<NetworkProjectileVisual>("Assets/Prefabs/Network/AttackProjectileNetwork.prefab"));

            NetworkObject azureMelee=LoadNetworkPrefab("Assets/Prefabs/Network/AzureMeleeCreepNetwork.prefab");
            NetworkObject azureRanged=LoadNetworkPrefab("Assets/Prefabs/Network/AzureRangedCreepNetwork.prefab");
            NetworkObject emberMelee=LoadNetworkPrefab("Assets/Prefabs/Network/EmberMeleeCreepNetwork.prefab");
            NetworkObject emberRanged=LoadNetworkPrefab("Assets/Prefabs/Network/EmberRangedCreepNetwork.prefab");
            NetworkCreepWaveSpawner[] waves=Object.FindObjectsByType<CreepWaveSpawner>()
                .Select(item=>ConfigureNetworkWaveSpawner(item.gameObject.AddComponent<NetworkCreepWaveSpawner>(),item.gameObject.name.StartsWith("Azure"),azureMelee,azureRanged,emberMelee,emberRanged)).ToArray();
            NetworkObject neutralSmall=LoadNetworkPrefab("Assets/Prefabs/Network/NeutralSmallNetwork.prefab");
            NetworkObject neutralMedium=LoadNetworkPrefab("Assets/Prefabs/Network/NeutralMediumNetwork.prefab");
            NetworkObject neutralLarge=LoadNetworkPrefab("Assets/Prefabs/Network/NeutralLargeNetwork.prefab");
            NetworkNeutralCampSpawner[] camps=Object.FindObjectsByType<NeutralCamp>()
                .Select(item=>ConfigureNetworkCampSpawner(item.gameObject.AddComponent<NetworkNeutralCampSpawner>(),neutralSmall,neutralMedium,neutralLarge)).ToArray();
            NeutralBossController localBoss=Object.FindAnyObjectByType<NeutralBossController>();
            GameObject bossObject=new GameObject("Network Guardian Spawner");
            NetworkNeutralBossSpawner boss=bossObject.AddComponent<NetworkNeutralBossSpawner>();
            SerializedObject bossSerialized=new SerializedObject(boss);
            bossSerialized.FindProperty("bossPrefab").objectReferenceValue=LoadNetworkPrefab("Assets/Prefabs/Network/CierzoGuardianNetwork.prefab");
            bossSerialized.FindProperty("spawnPosition").vector3Value=localBoss!=null?localBoss.transform.position:BossPitCenter;
            bossSerialized.ApplyModifiedPropertiesWithoutUndo();

            MobaNetworkMatchBootstrap bootstrap=managerObject.AddComponent<MobaNetworkMatchBootstrap>();
            List<GameObject> localActors=new List<GameObject>{localMatch,azure,ember};
            GameObject localCommands=GameObject.Find("Player Command Controller");
            if(localCommands!=null)localActors.Add(localCommands);
            if(localBoss!=null)localActors.Add(localBoss.gameObject);
            // The registrar is local-only, but its GameObject also owns
            // LocalHeroProvider. Keep that provider active for network ownership
            // callbacks; MobaNetworkMatchBootstrap disables only the registrar.
            bootstrap.Configure(azurePrefab,emberPrefab,matchPrefab,azureTower,emberTower,azureCore,emberCore,
                azure.transform.position,ember.transform.position,localActors.ToArray(),Object.FindObjectsByType<StructureEntity>(),waves,camps,boss);

            // M18 starts with no match running. Local actors remain authored for a
            // fast development match, but only the bootstrap may activate them after
            // the player explicitly chooses Start Local. Network mode never revives
            // these objects; it spawns the authoritative prefab counterparts instead.
            foreach(GameObject actor in localActors)if(actor!=null)actor.SetActive(false);
            foreach(StructureEntity structure in Object.FindObjectsByType<StructureEntity>())if(structure!=null)structure.gameObject.SetActive(false);
            foreach(NetworkCreepWaveSpawner wave in waves)if(wave!=null)wave.GetComponent<CreepWaveSpawner>().SetSimulationEnabled(false);
            foreach(NetworkNeutralCampSpawner camp in camps)if(camp!=null)camp.GetComponent<NeutralCamp>().SetSimulationEnabled(false);
            EditorUtility.SetDirty(manager);
        }

        private static NetworkObject LoadNetworkPrefab(string path)
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab!=null?prefab.GetComponent<NetworkObject>():null;
        }

        private static NetworkCreepWaveSpawner ConfigureNetworkWaveSpawner(NetworkCreepWaveSpawner bridge,bool azure,NetworkObject azureMelee,NetworkObject azureRanged,NetworkObject emberMelee,NetworkObject emberRanged)
        {
            SetObjectReference(bridge,"meleePrefab",azure?azureMelee:emberMelee);
            SetObjectReference(bridge,"rangedPrefab",azure?azureRanged:emberRanged);
            return bridge;
        }

        private static NetworkNeutralCampSpawner ConfigureNetworkCampSpawner(NetworkNeutralCampSpawner bridge,NetworkObject small,NetworkObject medium,NetworkObject large)
        {
            SetObjectReference(bridge,"smallPrefab",small);SetObjectReference(bridge,"mediumPrefab",medium);SetObjectReference(bridge,"largePrefab",large);
            return bridge;
        }

        // ----- Support objects ------------------------------------------------

        private static void CreateNavMeshBootstrap()
        {
            GameObject bootstrapObject = new GameObject("Large NavMesh Bootstrap");
            LargeNavMeshBootstrap bootstrap = bootstrapObject.AddComponent<LargeNavMeshBootstrap>();

            SerializedObject serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("sourceMask").intValue = 1 << GroundLayer;
            serialized.FindProperty("mapCenter").vector3Value = Vector3.zero;
            serialized.FindProperty("mapSize").vector3Value = new Vector3(200f, 40f, 200f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateMatchController()
        {
            GameObject matchObject = new GameObject("Match State Controller");
            matchObject.AddComponent<MatchStateController>();
            matchObject.AddComponent<MatchStatisticsController>();
            matchObject.AddComponent<MatchScoreboardController>();
            matchObject.AddComponent<StructureProgressionController>();
            matchObject.AddComponent<MatchVictoryDisplay>();
            return matchObject;
        }

        private static void ConfigureStructure(GameObject structureObject, TeamId team, StructureKind kind, StructureLane lane, StructureTier tier, float maxHealth, Material healthBackgroundMaterial, Material healthFillMaterial, float healthBarHeight)
        {
            TeamMember teamMember = structureObject.AddComponent<TeamMember>();
            SetEnum(teamMember, "team", (int)team);

            Health health = structureObject.AddComponent<Health>();
            SetFloat(health, "maxHealth", maxHealth);
            health.RestoreFull();

            StructureEntity structure = structureObject.AddComponent<StructureEntity>();
            structureObject.AddComponent<VisionSource>();
            structureObject.AddComponent<VisionVisibility>();
            SetEnum(structure, "kind", (int)kind);
            SetEnum(structure, "lane", (int)lane);
            SetEnum(structure, "tier", (int)tier);

            Renderer[] renderers = structureObject.GetComponentsInChildren<Renderer>();
            Collider[] colliders = structureObject.GetComponentsInChildren<Collider>();
            SerializedObject structureData = new SerializedObject(structure);
            SerializedProperty rendererProperty = structureData.FindProperty("renderersToDisable");
            rendererProperty.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
            {
                rendererProperty.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            }
            SerializedProperty colliderProperty = structureData.FindProperty("collidersToDisable");
            colliderProperty.arraySize = colliders.Length;
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderProperty.GetArrayElementAtIndex(i).objectReferenceValue = colliders[i];
            }
            structureData.ApplyModifiedPropertiesWithoutUndo();

            // Targeting lives on a child Selectable collider: the original structure
            // collider remains on Ground for the baked navigation footprint.
            GameObject target = new GameObject("Structure Target Collider");
            target.layer = SelectableLayer;
            target.transform.SetParent(structureObject.transform);
            if (kind == StructureKind.Core)
            {
                // The visual core is a heavily scaled cube. Counter-scale the child
                // collider so it stays on the visible mesh instead of inheriting a
                // giant offset/radius from the root transform.
                Vector3 scale = structureObject.transform.localScale;
                target.transform.localPosition = Vector3.zero;
                target.transform.localScale = new Vector3(
                    scale.x > 0f ? 1f / scale.x : 1f,
                    scale.y > 0f ? 1f / scale.y : 1f,
                    scale.z > 0f ? 1f / scale.z : 1f);
                BoxCollider targetCollider = target.AddComponent<BoxCollider>();
                targetCollider.size = new Vector3(9f, 14f, 9f);
            }
            else
            {
                target.transform.localPosition = new Vector3(0f, healthBarHeight * 0.45f, 0f);
                SphereCollider targetCollider = target.AddComponent<SphereCollider>();
                targetCollider.radius = 2.2f;
            }

            // Include the newly created selectable child in the destruction set too.
            colliders = structureObject.GetComponentsInChildren<Collider>();
            structureData.Update();
            colliderProperty = structureData.FindProperty("collidersToDisable");
            colliderProperty.arraySize = colliders.Length;
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderProperty.GetArrayElementAtIndex(i).objectReferenceValue = colliders[i];
            }
            structureData.ApplyModifiedPropertiesWithoutUndo();

            CreateHealthBar(structureObject.transform, health, healthBackgroundMaterial, healthFillMaterial, healthBarHeight, kind == StructureKind.Core ? 3.4f : 2.4f, worldSpaceAnchor: true);
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Sun Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.52f);
        }

        // ----- MOBA camera (M4.4) --------------------------------------------

        // Real camera bounds for the greybox. The boundary walls are centred at +/-84
        // with thickness 4, so their outer face is at +/-86; using +/-86 lets the
        // camera reach exactly the wall face without ever revealing empty exterior.
        private const float CameraBound = 86f;

        // Camera geometry. Height above the ground plane (y = 0) and the 55-degree
        // pitch fix how far ahead (+Z) the view centre sits: centreZ = height * cot,
        // with cot = forward.z / -forward.y for the pitch. Pulling the follow pivot
        // back by that amount frames the hero centred instead of near the bottom edge,
        // and it is independent of zoom, so it holds at every orthographic size.
        private const float CameraHeight = 35f;
        private const float CameraPitchDeg = 55f;

        /// <summary>
        /// Builds the M4.4 MOBA camera rig for the greybox: a tilted orthographic
        /// <see cref="MobaCameraController"/> with real <see cref="CameraWorldBounds"/>,
        /// a scene <see cref="LocalHeroProvider"/> and a
        /// <see cref="SceneLocalHeroRegistrar"/> that registers Azure as the local hero
        /// at runtime. This replaces the M3A <see cref="IsometricCameraRig"/> in this
        /// scene only; the rig stays available for the spike scenes and its tests.
        /// </summary>
        private static void CreateMobaCamera(Transform azure)
        {
            // Framing offset that centres the hero for the fixed pitch/height (see the
            // constants above). Negative Z pulls the pivot back along the view.
            float pitchRad = CameraPitchDeg * Mathf.Deg2Rad;
            float centreZ = CameraHeight * (Mathf.Cos(pitchRad) / Mathf.Sin(pitchRad));
            Vector2 followOffset = new Vector2(0f, -centreZ);

            // Decoupled local-hero source, plus explicit runtime registration of Azure
            // through the provider's existing API (Ember is never registered).
            GameObject providerObject = new GameObject("Local Hero Provider");
            LocalHeroProvider provider = providerObject.AddComponent<LocalHeroProvider>();

            SceneLocalHeroRegistrar registrar = providerObject.AddComponent<SceneLocalHeroRegistrar>();
            SetObjectReference(registrar, "provider", provider);
            SetObjectReference(registrar, "hero", azure);

            GameObject cameraObject = new GameObject("MOBA Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 22f;
            camera.farClipPlane = 500f;

            // Start already framed on Azure so there is no first-frame jump: pivot XZ =
            // hero XZ + framing offset, at the configured height, with the iso pitch.
            cameraObject.transform.position = new Vector3(
                azure.position.x + followOffset.x,
                CameraHeight,
                azure.position.z + followOffset.y);
            cameraObject.transform.rotation = Quaternion.Euler(CameraPitchDeg, 0f, 0f);

            CameraWorldBounds bounds = cameraObject.AddComponent<CameraWorldBounds>();
            SetFloat(bounds, "minX", -CameraBound);
            SetFloat(bounds, "maxX", CameraBound);
            SetFloat(bounds, "minZ", -CameraBound);
            SetFloat(bounds, "maxZ", CameraBound);

            MobaCameraController controller = cameraObject.AddComponent<MobaCameraController>();
            cameraObject.AddComponent<MinimapFeedback>();
            cameraObject.AddComponent<FogOfWarOverlay>();
            SerializedObject controllerObject = new SerializedObject(controller);
            controllerObject.FindProperty("keyboardPanSpeed").floatValue = 50f;
            controllerObject.FindProperty("edgeScrollingEnabled").boolValue = true;
            controllerObject.FindProperty("edgePanSpeed").floatValue = 50f;
            controllerObject.FindProperty("edgeBorderPixels").intValue = 12;
            controllerObject.FindProperty("zoomSpeed").floatValue = 3.5f;
            controllerObject.FindProperty("minOrthographicSize").floatValue = 12f;
            controllerObject.FindProperty("maxOrthographicSize").floatValue = 55f;
            controllerObject.FindProperty("targetCamera").objectReferenceValue = camera;
            controllerObject.FindProperty("worldBounds").objectReferenceValue = bounds;
            controllerObject.FindProperty("groundPlaneY").floatValue = 0f;
            controllerObject.FindProperty("heroProvider").objectReferenceValue = provider;
            controllerObject.FindProperty("followPlaneOffset").vector2Value = followOffset;
            controllerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateCommandController()
        {
            GameObject controller = new GameObject("Player Command Controller");
            PlayerCommandController commandController = controller.AddComponent<PlayerCommandController>();

            SerializedObject commandObject = new SerializedObject(commandController);
            commandObject.FindProperty("commandCamera").objectReferenceValue = Camera.main;
            commandObject.FindProperty("groundMask").intValue = 1 << GroundLayer;
            commandObject.FindProperty("selectableMask").intValue = 1 << SelectableLayer;
            commandObject.FindProperty("attackableMask").intValue = (1 << SelectableLayer) | (1 << AttackableLayer);
            commandObject.ApplyModifiedPropertiesWithoutUndo();

            // The network controller is inert until Host/Client starts, then sends
            // requests only through the hero owned by this local connection.
            GameObject networkController=new GameObject("Network Player Command Controller");
            NetworkPlayerCommandController networkCommands=networkController.AddComponent<NetworkPlayerCommandController>();
            SerializedObject networkObject=new SerializedObject(networkCommands);
            networkObject.FindProperty("commandCamera").objectReferenceValue=Camera.main;
            networkObject.FindProperty("groundMask").intValue=1<<GroundLayer;
            networkObject.FindProperty("selectableMask").intValue=1<<SelectableLayer;
            networkObject.FindProperty("attackableMask").intValue=(1<<SelectableLayer)|(1<<AttackableLayer);
            networkObject.ApplyModifiedPropertiesWithoutUndo();
        }

        // ----- Assets / helpers ----------------------------------------------

        private static UnitDefinition CreateOrLoadUnitDefinition(string path, float maxHealth, float movementSpeed, float attackDamage, float attackRange, float attacksPerSecond)
        {
            EnsureFolder("Assets", "Data");

            UnitDefinition definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<UnitDefinition>();
                AssetDatabase.CreateAsset(definition, path);

                SerializedObject definitionObject = new SerializedObject(definition);
                definitionObject.FindProperty("maxHealth").floatValue = maxHealth;
                definitionObject.FindProperty("movementSpeed").floatValue = movementSpeed;
                definitionObject.FindProperty("attackDamage").floatValue = attackDamage;
                definitionObject.FindProperty("attackRange").floatValue = attackRange;
                definitionObject.FindProperty("attacksPerSecond").floatValue = attacksPerSecond;
                definitionObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }

            return definition;
        }

        private static ItemCatalog CreateShopCatalog()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Items");
            ItemDefinition[] items =
            {
                CreateOrLoadItemDefinition("Assets/Data/Items/BastionPlating.asset", "bastion.plating", "Bastion Plating", "A sturdy plate that reinforces a hero's vital reserve.", 40, 20, 120f, 0f, 0f, 0f),
                CreateOrLoadItemDefinition("Assets/Data/Items/GaleEdge.asset", "gale.edge", "Gale Edge", "A honed edge that adds direct striking force.", 45, 22, 0f, 15f, 0f, 0f),
                CreateOrLoadItemDefinition("Assets/Data/Items/WindstepBoots.asset", "windstep.boots", "Windstep Boots", "Light boots for faster movement between lanes.", 35, 17, 0f, 0f, 0.8f, 0f),
                CreateOrLoadItemDefinition("Assets/Data/Items/TempestCog.asset", "tempest.cog", "Tempest Cog", "A simple mechanism that improves attack cadence.", 50, 25, 0f, 0f, 0f, 0.25f),
                CreateOrLoadItemDefinition("Assets/Data/Items/CierzoAlloy.asset", "cierzo.alloy", "Cierzo Alloy", "A balanced alloy providing both endurance and force.", 55, 27, 60f, 8f, 0f, 0f)
            };

            GameObject catalogObject = new GameObject("Item Catalog");
            ItemCatalog catalog = catalogObject.AddComponent<ItemCatalog>();
            SetObjectArray(catalog, "items", items);
            catalog.Rebuild();
            return catalog;
        }

        private static AbilityDefinition[] CreateHeroAbilityKit()
        {
            EnsureFolder("Assets", "Data"); EnsureFolder("Assets/Data", "Abilities");
            return new[]
            {
                CreateOrLoadAbility("Assets/Data/Abilities/ArcBolt.asset", "arc.bolt", "Arc Bolt", "Q: targeted projectile damage.", AbilityTargeting.UnitTarget, AbilityEffect.ProjectileDamage, 8f, 0.25f, 2.5f, 0f, 14f, new[] { 35f, 45f, 55f, 65f }, new[] { 45f, 70f, 95f, 120f }, new[] { 1, 1, 1, 1 }),
                CreateOrLoadAbility("Assets/Data/Abilities/StormMark.asset", "storm.mark", "Storm Mark", "W: slows enemies in a chosen area.", AbilityTargeting.PointTarget, AbilityEffect.AreaSlow, 7f, 0.3f, 2.5f, 2.5f, 14f, new[] { 45f, 55f, 65f, 75f }, new[] { .25f, .35f, .45f, .55f }, new[] { 1, 1, 1, 1 }),
                CreateOrLoadAbility("Assets/Data/Abilities/GaleStep.asset", "gale.step", "Gale Step", "E: temporary self movement boost.", AbilityTargeting.NoTarget, AbilityEffect.SelfMoveSpeed, 0f, 0.1f, 0f, 3f, 14f, new[] { 30f, 35f, 40f, 45f }, new[] { 1f, 1.4f, 1.8f, 2.2f }, new[] { 1, 1, 1, 1 }),
                CreateOrLoadAbility("Assets/Data/Abilities/TempestFall.asset", "tempest.fall", "Tempest Fall", "R: powerful area stun.", AbilityTargeting.PointTarget, AbilityEffect.StrongAreaStun, 9f, 0.45f, 4f, 1.25f, 14f, new[] { 90f, 120f, 150f, 180f }, new[] { 150f, 240f, 330f, 420f }, new[] { 6, 12, 18, 18 })
            };
        }

        private static AbilityDefinition CreateOrLoadAbility(string path, string id, string displayName, string description, AbilityTargeting targeting, AbilityEffect effect, float range, float castPoint, float radius, float duration, float projectileSpeed, float[] costs, float[] values, int[] requiredLevels)
        {
            AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            if (ability == null) { ability = ScriptableObject.CreateInstance<AbilityDefinition>(); AssetDatabase.CreateAsset(ability, path); }
            SerializedObject serialized = new SerializedObject(ability);
            serialized.FindProperty("abilityId").stringValue = id; serialized.FindProperty("displayName").stringValue = displayName; serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("targeting").enumValueIndex = (int)targeting; serialized.FindProperty("effect").enumValueIndex = (int)effect; serialized.FindProperty("range").floatValue = range; serialized.FindProperty("castPoint").floatValue = castPoint; serialized.FindProperty("areaRadius").floatValue = radius; serialized.FindProperty("duration").floatValue = duration; serialized.FindProperty("projectileSpeed").floatValue = projectileSpeed;
            SetFloatArray(serialized.FindProperty("manaCosts"), costs); SetFloatArray(serialized.FindProperty("effectValues"), values); SetIntArray(serialized.FindProperty("requiredHeroLevels"), requiredLevels);
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(ability); return ability;
        }

        private static ItemDefinition CreateOrLoadItemDefinition(string path, string id, string displayName, string description, int purchasePrice, int salePrice, float health, float damage, float movement, float attackSpeed)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("purchasePrice").intValue = purchasePrice;
            serialized.FindProperty("salePrice").intValue = salePrice;
            serialized.FindProperty("maximumHealthBonus").floatValue = health;
            serialized.FindProperty("attackDamageBonus").floatValue = damage;
            serialized.FindProperty("movementSpeedBonus").floatValue = movement;
            serialized.FindProperty("attackSpeedBonus").floatValue = attackSpeed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void CreateShopZone(string name, Vector3 position, TeamId team, ItemCatalog catalog, Material material)
        {
            GameObject zoneObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            zoneObject.name = name;
            zoneObject.transform.position = position;
            zoneObject.transform.localScale = new Vector3(13f, 0.08f, 13f);
            zoneObject.GetComponent<Renderer>().sharedMaterial = material;
            zoneObject.GetComponent<Collider>().isTrigger = true;
            ShopZone zone = zoneObject.AddComponent<ShopZone>();
            SetEnum(zone, "team", (int)team);
            SetObjectReference(zone, "catalog", catalog);
        }

        private static void EnsureFolder(string parent, string folder)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{folder}"))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static Material CreateMaterial(string path, Color color)
        {
            Shader shader = Shader.Find("Standard");
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Material material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureSceneInBuildSettings()
        {
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (buildScene.path == ScenePath)
                {
                    return;
                }
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            System.Array.Resize(ref scenes, scenes.Length + 1);
            scenes[scenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes;
        }

        private static void EnsureLayerName(int layer, string name)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layerProperty = layers.GetArrayElementAtIndex(layer);
            layerProperty.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Object target, string propertyName, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloatArray(SerializedProperty property, float[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        private static void SetIntArray(SerializedProperty property, int[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).intValue = values[i];
        }

        private static void SetInt(Object target, string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string propertyName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureAttack(BasicAttack attack, AttackDelivery delivery, float range, float damage, float interval, float attackPoint, float backswing)
        {
            SerializedObject serialized = new SerializedObject(attack);
            serialized.FindProperty("delivery").enumValueIndex = (int)delivery;
            serialized.FindProperty("range").floatValue = range;
            serialized.FindProperty("damage").floatValue = damage;
            serialized.FindProperty("attackInterval").floatValue = interval;
            serialized.FindProperty("attackPoint").floatValue = attackPoint;
            serialized.FindProperty("backswing").floatValue = backswing;
            serialized.FindProperty("projectileSpeed").floatValue = 15f;
            serialized.FindProperty("projectileLifetime").floatValue = 4f;
            serialized.FindProperty("useUnitDefinition").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
