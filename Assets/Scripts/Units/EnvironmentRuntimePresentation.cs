using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Runtime-safe visual migration for existing authored arena scenes.
    /// It applies the M21 shared textured materials without touching colliders,
    /// NavMesh sources, match state or network ownership.</summary>
    public sealed class EnvironmentRuntimePresentation : MonoBehaviour
    {
        private static Material terrain;
        private static Material concrete;
        private static bool attempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAfterSceneLoad()
        {
            GameObject root = GameObject.Find("EnvironmentRoot");
            if (root == null) return;
            ApplyAll();
        }

        public static void Apply(GameObject root)
        {
            if (root == null || !EnsureMaterials()) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) ApplyRenderer(renderer);
        }

        public static void ApplyAll()
        {
            if (!EnsureMaterials()) return;
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)) ApplyRenderer(renderer);
        }

        private static void ApplyRenderer(Renderer renderer)
        {
            if (renderer == null) return;
            string objectName = renderer.gameObject.name;
            if (objectName == "Ground" || objectName.Contains("Jungle") || objectName.Contains("Pillar") || objectName.Contains("Boundary"))
            {
                renderer.sharedMaterial = terrain;
            }
            else if (objectName.Contains("Lane") || objectName.Contains("Bridge") || objectName.Contains("Base") || objectName.Contains("Tower") || objectName.Contains("Spawn") || objectName.Contains("Pad") || objectName.Contains("Shaft") || objectName.Contains("Cap"))
            {
                renderer.sharedMaterial = concrete;
            }
        }

        private static bool EnsureMaterials()
        {
            if (terrain != null && concrete != null) return true;
            if (attempted) return false;
            attempted = true;
            Shader shader = Shader.Find("Standard");
            Texture2D terrainTexture = Resources.Load<Texture2D>("Art/Environment/Materials/RockyTerrain/rocky_terrain_02_diff_4k");
            Texture2D terrainNormal = Resources.Load<Texture2D>("Art/Environment/Materials/RockyTerrain/rocky_terrain_02_nor_gl_4k");
            Texture2D concreteTexture = Resources.Load<Texture2D>("Art/Environment/Materials/ConcreteWall/concrete_wall_009_diff_4k");
            Texture2D concreteNormal = Resources.Load<Texture2D>("Art/Environment/Materials/ConcreteWall/concrete_wall_009_nor_gl_4k");
            if (shader == null || terrainTexture == null || concreteTexture == null) return false;
            terrain = MakeMaterial(shader, terrainTexture, terrainNormal, new Vector2(6f, 6f), .23f, "Runtime Rocky Terrain");
            concrete = MakeMaterial(shader, concreteTexture, concreteNormal, new Vector2(4f, 4f), .31f, "Runtime Concrete");
            return true;
        }

        private static Material MakeMaterial(Shader shader, Texture2D diffuse, Texture2D normal, Vector2 tiling, float smoothness, string materialName)
        {
            Material material = new Material(shader) { name = materialName, hideFlags = HideFlags.DontSave, color = Color.white };
            material.SetTexture("_MainTex", diffuse); material.SetTextureScale("_MainTex", tiling);
            material.SetFloat("_Glossiness", smoothness);
            if (normal != null) { material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); }
            return material;
        }
    }
}
