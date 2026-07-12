using UnityEngine;
using System.Collections.Generic;

namespace CierzoArena.Frontend
{
    /// <summary>Compatibility pass for the project's Built-in render pipeline. It
    /// preserves the authored colour while replacing unsupported materials before
    /// they can fall back to Unity's magenta error shader.</summary>
    public static class ArenaVisualPass
    {
        private static Shader standard;
        private static readonly Dictionary<Material,Material> replacements=new();
        public static void Repair(GameObject root)
        {
            if(root==null)return;
            standard ??= Shader.Find("Standard");
            if(standard==null)return;
            foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material source=renderer.sharedMaterial;if(source==null)continue;
                bool usesUrpShader=source.shader!=null&&source.shader.name.StartsWith("Universal Render Pipeline/");
                if(source.shader==null||usesUrpShader||!source.shader.isSupported)
                {
                    if(!replacements.TryGetValue(source,out Material repaired))
                    {
                        Color color=ResolveSliceColor(source.name,source.HasProperty("_Color")?source.color:Color.white);
                        repaired=new Material(standard){name=source.name+" (Built-in)"};
                        repaired.color=color;
                        replacements[source]=repaired;
                    }
                    renderer.sharedMaterial=repaired;
                }
                if(renderer.gameObject.name.Contains("River")&&renderer.GetComponent<RiverSurfaceVisual>()==null)renderer.gameObject.AddComponent<RiverSurfaceVisual>();
            }
        }
        private static Color ResolveSliceColor(string name,Color fallback)
        {
            if(string.IsNullOrEmpty(name))return fallback;
            if(name.Contains("River"))return new Color(.025f,.16f,.31f);
            if(name.Contains("AzureBase"))return new Color(.06f,.22f,.48f);
            if(name.Contains("EmberBase"))return new Color(.46f,.11f,.07f);
            if(name.Contains("Azure"))return new Color(.16f,.62f,.94f);
            if(name.Contains("Ember"))return new Color(.88f,.22f,.12f);
            if(name.Contains("Ground"))return new Color(.16f,.25f,.24f);
            if(name.Contains("RouteMid"))return new Color(.56f,.46f,.28f);
            if(name.Contains("Route"))return new Color(.37f,.31f,.22f);
            if(name.Contains("Obstacle")||name.Contains("Boundary"))return new Color(.10f,.14f,.18f);
            return fallback;
        }
    }
}
