using UnityEngine;

namespace CierzoArena.Environment
{
    /// <summary>
    /// Serialized reference set for the subset of the OccaSoftware "Low Poly Fantasy
    /// Village Environment" package that CierzoArena actually uses (M23). It is a
    /// plain data asset: it holds prefab references only, never touches AssetDatabase
    /// at runtime, and is populated in the editor by <c>FantasyVillagePaletteBuilder</c>.
    /// Builders consume these serialized references so scene generation is
    /// deterministic and does not search the project by name every time.
    /// </summary>
    [CreateAssetMenu(fileName = "FantasyVillageEnvironmentPalette", menuName = "Cierzo Arena/Fantasy Village Environment Palette")]
    public sealed class FantasyVillageEnvironmentPalette : ScriptableObject
    {
        [Header("Buildings")]
        [Tooltip("Largest silhouette, used as the town-center / core visual anchor.")]
        [SerializeField] private GameObject mainBuilding;
        [SerializeField] private GameObject[] secondaryBuildings = new GameObject[0];
        [SerializeField] private GameObject[] smallHouses = new GameObject[0];

        [Header("Paths")]
        [SerializeField] private GameObject pathStraight;
        [SerializeField] private GameObject[] pathPieces = new GameObject[0];
        [SerializeField] private GameObject bridge;

        [Header("Vegetation")]
        [SerializeField] private GameObject[] trees = new GameObject[0];
        [SerializeField] private GameObject[] pineTrees = new GameObject[0];
        [SerializeField] private GameObject[] flowers = new GameObject[0];
        [SerializeField] private GameObject flowerPot;

        [Header("Terrain / boundary")]
        [SerializeField] private GameObject[] cliffs = new GameObject[0];
        [SerializeField] private GameObject[] mountains = new GameObject[0];
        [SerializeField] private GameObject[] rocks = new GameObject[0];

        [Header("Props")]
        [SerializeField] private GameObject bench;
        [SerializeField] private GameObject crate;
        [SerializeField] private GameObject fence;
        [SerializeField] private GameObject lantern;
        [SerializeField] private GameObject boat;

        public GameObject MainBuilding => mainBuilding;
        public GameObject[] SecondaryBuildings => secondaryBuildings;
        public GameObject[] SmallHouses => smallHouses;
        public GameObject PathStraight => pathStraight;
        public GameObject[] PathPieces => pathPieces;
        public GameObject Bridge => bridge;
        public GameObject[] Trees => trees;
        public GameObject[] PineTrees => pineTrees;
        public GameObject[] Flowers => flowers;
        public GameObject FlowerPot => flowerPot;
        public GameObject[] Cliffs => cliffs;
        public GameObject[] Mountains => mountains;
        public GameObject[] Rocks => rocks;
        public GameObject Bench => bench;
        public GameObject Crate => crate;
        public GameObject Fence => fence;
        public GameObject Lantern => lantern;
        public GameObject Boat => boat;

        /// <summary>Editor-only setter used by the palette builder. Kept here so the
        /// serialized fields stay private while the deterministic populate step lives
        /// in an editor assembly.</summary>
        public void SetAll(
            GameObject main, GameObject[] secondary, GameObject[] houses,
            GameObject straight, GameObject[] pieces, GameObject bridgePrefab,
            GameObject[] treeSet, GameObject[] pineSet, GameObject[] flowerSet, GameObject pot,
            GameObject[] cliffSet, GameObject[] mountainSet, GameObject[] rockSet,
            GameObject benchPrefab, GameObject cratePrefab, GameObject fencePrefab, GameObject lanternPrefab, GameObject boatPrefab)
        {
            mainBuilding = main; secondaryBuildings = secondary ?? new GameObject[0]; smallHouses = houses ?? new GameObject[0];
            pathStraight = straight; pathPieces = pieces ?? new GameObject[0]; bridge = bridgePrefab;
            trees = treeSet ?? new GameObject[0]; pineTrees = pineSet ?? new GameObject[0]; flowers = flowerSet ?? new GameObject[0]; flowerPot = pot;
            cliffs = cliffSet ?? new GameObject[0]; mountains = mountainSet ?? new GameObject[0]; rocks = rockSet ?? new GameObject[0];
            bench = benchPrefab; crate = cratePrefab; fence = fencePrefab; lantern = lanternPrefab; boat = boatPrefab;
        }

        /// <summary>
        /// The mandatory references M23 depends on. A palette missing any of these is
        /// not usable by the base builder. Returns true and an empty message when
        /// complete; otherwise false and a human-readable list of what is missing.
        /// </summary>
        public bool Validate(out string report)
        {
            var missing = new System.Collections.Generic.List<string>();
            if (mainBuilding == null) missing.Add("MainBuilding");
            if (smallHouses == null || smallHouses.Length == 0) missing.Add("SmallHouses");
            if (pathStraight == null && (pathPieces == null || pathPieces.Length == 0)) missing.Add("Path (straight or pieces)");
            if (trees == null || trees.Length == 0) missing.Add("Trees");
            if (flowers == null || flowers.Length == 0) missing.Add("Flowers");
            if (crate == null) missing.Add("Crate");
            if (lantern == null) missing.Add("Lantern");
            if (fence == null) missing.Add("Fence");
            if (cliffs == null || cliffs.Length == 0) missing.Add("Cliffs");

            report = missing.Count == 0 ? "Palette complete." : "Missing: " + string.Join(", ", missing);
            return missing.Count == 0;
        }
    }
}
