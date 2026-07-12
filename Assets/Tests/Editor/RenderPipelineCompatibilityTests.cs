using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CierzoArena.Tests.Editor
{
    /// <summary>Guards the prototype against assigning URP-only materials to its
    /// Built-in pipeline scenes, which Unity renders as the magenta error shader.</summary>
    public sealed class RenderPipelineCompatibilityTests
    {
        [Test]
        public void ArenaMaterialsUseShadersSupportedByTheConfiguredPipeline()
        {
            Assert.IsNull(GraphicsSettings.currentRenderPipeline,
                "CierzoArena currently uses the Built-in Render Pipeline; this guard must be updated before a pipeline migration.");

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" });
            Assert.IsNotEmpty(materialGuids);
            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(material.shader, $"{path} has no shader.");
                Assert.IsFalse(material.shader.name.StartsWith("Universal Render Pipeline/"),
                    $"{path} uses {material.shader.name} although the project is configured for Built-in rendering.");
                Assert.IsTrue(material.shader.isSupported, $"{path} uses unsupported shader {material.shader.name}.");
            }
        }
    }
}
