#if UNITY_EDITOR
using CierzoArena.CameraSystem;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Navigation;
using CierzoArena.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    /// - A diagonal technical ravine/river that splits the map into two halves.
    /// - One walkable bridge (chokepoint) per lane crossing the river.
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

        // Corners of the square arena. Azure at SW, Ember at NE (bottom-left /
        // top-right, like the classic MOBA layout). NW and SE are the outer lane
        // corners where the top and bottom lanes bend and cross the river.
        private static readonly Vector3 AzureBaseCenter = new Vector3(-60f, 0f, -60f);
        private static readonly Vector3 EmberBaseCenter = new Vector3(60f, 0f, 60f);
        private static readonly Vector3 NorthWestCorner = new Vector3(-60f, 0f, 60f);
        private static readonly Vector3 SouthEastCorner = new Vector3(60f, 0f, -60f);

        [MenuItem("Cierzo Arena/Create MOBA Greybox Arena")]
        public static void CreateMobaGreyboxArena()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MobaGreyboxArena";

            EnsureLayerName(GroundLayer, "Ground");
            EnsureLayerName(SelectableLayer, "Selectable");

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
            BuildBases(azureBaseMaterial, emberBaseMaterial, markerMaterial);
            BuildObstacles(obstacleMaterial);
            BuildNeutralZones(neutralMaterial, markerMaterial);
            BuildTowersAndSpawns(azureBaseMaterial, emberBaseMaterial, markerMaterial);
            BuildBoundary(boundaryMaterial);

            UnitDefinition azureDefinition = CreateOrLoadUnitDefinition(
                "Assets/Data/AzureVanguard.asset", 520f, 5.5f, 48f, 2.2f, 0.8f);
            UnitDefinition emberDefinition = CreateOrLoadUnitDefinition(
                "Assets/Data/EmberTarget.asset", 180f, 4.2f, 30f, 1.8f, 0.5f);

            GameObject azure = CreateUnit(
                "Azure Vanguard", AzureBaseCenter + new Vector3(9f, 1f, 9f), TeamId.Azure,
                azureMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, azureDefinition, startSelected: true);
            azure.AddComponent<NavPathProbe>();

            CreateUnit(
                "Ember Skirmisher", EmberBaseCenter + new Vector3(-9f, 1f, -9f), TeamId.Ember,
                emberMaterial, ringMaterial, healthBackgroundMaterial, healthFillMaterial, emberDefinition, startSelected: false);

            CreateNavMeshBootstrap();
            CreateLighting();
            CreateMobaCamera(azure.transform);
            CreateCommandController();

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

            // The arena is a square (corners at Azure SW, Ember NE, plus NW and SE).
            // The river runs along the NW-SE anti-diagonal and splits the square into
            // two triangular halves. Each half is a large slab rotated 45 degrees and
            // offset along the SW-NE main diagonal, leaving a diagonal gap (the river)
            // that only the three bridges reconnect for navigation.
            CreateGroundBox(terrain.transform, "Ground Azure Half", new Vector3(-41f, -0.5f, -41f), new Vector3(200f, 1f, 100f), 45f, groundMaterial);
            CreateGroundBox(terrain.transform, "Ground Ember Half", new Vector3(41f, -0.5f, 41f), new Vector3(200f, 1f, 100f), 45f, groundMaterial);

            // Visual-only sunken river filling the diagonal gap. It is on the default
            // layer so it is never collected into the NavMesh; the gap in the ground is
            // what actually blocks crossing anywhere except the bridges.
            GameObject river = GameObject.CreatePrimitive(PrimitiveType.Cube);
            river.name = "River (visual)";
            river.layer = 0;
            river.transform.SetParent(terrain.transform);
            river.transform.position = new Vector3(0f, -2.5f, 0f);
            river.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            river.transform.localScale = new Vector3(210f, 3f, 18f);
            river.GetComponent<Renderer>().sharedMaterial = riverMaterial;
            Object.DestroyImmediate(river.GetComponent<Collider>());
        }

        private static void BuildBridges(Material bridgeMaterial)
        {
            GameObject bridges = new GameObject("Bridges");

            // One chokepoint per lane, each spanning the river gap where that lane
            // crosses it: the top lane at the NW corner, the mid lane at the centre,
            // and the bottom lane at the SE corner. The 45-degree yaw spans the gap.
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

        private static void BuildBases(Material azureBaseMaterial, Material emberBaseMaterial, Material markerMaterial)
        {
            GameObject bases = new GameObject("Bases");

            BuildBase(bases.transform, "Azure Base", AzureBaseCenter, azureBaseMaterial, markerMaterial);
            BuildBase(bases.transform, "Ember Base", EmberBaseCenter, emberBaseMaterial, markerMaterial);
        }

        private static void BuildBase(Transform parent, string name, Vector3 center, Material baseMaterial, Material markerMaterial)
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
            CreateObstacle(baseRoot.transform, name + " Core", new Vector3(center.x, 7f, center.z), new Vector3(9f, 14f, 9f), baseMaterial, 45f);

            // Bright cap crowning the core so the base nucleus pops from above.
            CreateMarker(baseRoot.transform, name + " Core Cap (marker)", new Vector3(center.x, 14.3f, center.z), markerMaterial, 6f);
        }

        // ----- Obstacles ------------------------------------------------------

        private static void BuildObstacles(Material obstacleMaterial)
        {
            GameObject obstacles = new GameObject("Obstacles");

            // Obstacles fill the four jungle triangles between the mid lane and the two
            // side lanes (never on a lane), so units must weave through jungle without
            // ever losing the walkable lanes. They sit clear of the river gap too.
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

        private static void BuildTowersAndSpawns(Material azureMaterial, Material emberMaterial, Material capMaterial)
        {
            GameObject towers = new GameObject("Towers");
            GameObject spawns = new GameObject("Spawn Points");

            // Each lane is a polyline from Azure (SW) to Ember (NE). Towers and spawns
            // are placed by fraction of the lane's arc-length, so they follow the
            // bends of the L-shaped side lanes and mirror perfectly between the teams.
            Vector3[] midLane = { AzureBaseCenter, EmberBaseCenter };
            Vector3[] topLane = { AzureBaseCenter, NorthWestCorner, EmberBaseCenter };
            Vector3[] botLane = { AzureBaseCenter, SouthEastCorner, EmberBaseCenter };
            (string Lane, Vector3[] Path)[] lanes =
            {
                ("Top", topLane),
                ("Mid", midLane),
                ("Bottom", botLane),
            };

            // Three tiers per lane and team: gate (near base), inner, and outer (near
            // the river). Azure measures from the start, Ember mirrors from the end.
            // Kept back from the river so the two teams' outer towers can never be in
            // range of each other across the crossing (outer at 0.40 => 20% mid gap).
            float[] towerFraction = { 0.16f, 0.28f, 0.40f };
            string[] towerTier = { "Gate", "Inner", "Outer" };
            const float spawnFraction = 0.09f;

            foreach ((string lane, Vector3[] path) in lanes)
            {
                for (int i = 0; i < towerFraction.Length; i++)
                {
                    CreateTowerMarker(towers.transform, $"Azure {lane} Tower ({towerTier[i]})", PointAlong(path, towerFraction[i]), azureMaterial, capMaterial);
                    CreateTowerMarker(towers.transform, $"Ember {lane} Tower ({towerTier[i]})", PointAlong(path, 1f - towerFraction[i]), emberMaterial, capMaterial);
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

        private static void CreateTowerMarker(Transform parent, string name, Vector3 groundPos, Material teamMaterial, Material capMaterial)
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

        private static void CreateObstacle(Transform parent, string name, Vector3 center, Vector3 size, Material material, float yaw = 0f)
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

        private static GameObject CreateUnit(string name, Vector3 position, TeamId team, Material bodyMaterial, Material ringMaterial, Material healthBackgroundMaterial, Material healthFillMaterial, UnitDefinition definition, bool startSelected)
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

            Health health = unit.AddComponent<Health>();
            SetFloat(health, "maxHealth", definition.MaxHealth);

            DamageFlash damageFlash = unit.AddComponent<DamageFlash>();
            SetObjectReference(damageFlash, "targetRenderer", unit.GetComponent<Renderer>());

            unit.AddComponent<DamageNumberSpawner>();
            CreateHealthBar(unit.transform, health, healthBackgroundMaterial, healthFillMaterial);

            // Both units are fully controllable so a distant target can be moved
            // manually to exercise dynamic long-range pursuit.
            unit.AddComponent<ClickMover>();
            unit.AddComponent<BasicAttack>();
            unit.AddComponent<UnitOrderController>();

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

        private static void CreateHealthBar(Transform unit, Health health, Material backgroundMaterial, Material fillMaterial)
        {
            GameObject bar = new GameObject("Health Bar");
            bar.layer = 2;
            bar.transform.SetParent(unit);
            bar.transform.localPosition = new Vector3(0f, 2.35f, 0f);
            bar.transform.localRotation = Quaternion.identity;

            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Health Bar Background";
            background.layer = 2;
            background.transform.SetParent(bar.transform);
            background.transform.localPosition = Vector3.zero;
            background.transform.localScale = new Vector3(1.5f, 0.18f, 0.03f);
            background.GetComponent<Renderer>().sharedMaterial = backgroundMaterial;
            Object.DestroyImmediate(background.GetComponent<Collider>());

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Health Bar Fill";
            fill.layer = 2;
            fill.transform.SetParent(bar.transform);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            fill.transform.localScale = new Vector3(1.5f, 0.12f, 0.03f);
            fill.GetComponent<Renderer>().sharedMaterial = fillMaterial;
            Object.DestroyImmediate(fill.GetComponent<Collider>());

            WorldHealthBar healthBar = bar.AddComponent<WorldHealthBar>();
            SerializedObject barObject = new SerializedObject(healthBar);
            barObject.FindProperty("health").objectReferenceValue = health;
            barObject.FindProperty("fill").objectReferenceValue = fill.transform;
            barObject.ApplyModifiedPropertiesWithoutUndo();
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
            commandObject.ApplyModifiedPropertiesWithoutUndo();
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

        private static void SetEnum(Object target, string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
