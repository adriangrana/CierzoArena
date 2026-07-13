#if UNITY_EDITOR
using CierzoArena.EditorTools;
using CierzoArena.Frontend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CierzoArena.Tests.Editor
{
    public sealed class EnvironmentArtPipelineTests
    {
        [Test]
        public void BuiltInPipelineAndEnvironmentMaterialsAreValid()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.Null);
            Assert.That(EnvironmentArtValidator.Validate(), Is.Empty);
        }

        [Test]
        public void RockyConcreteAndWaterUseSharedConfiguredTextures()
        {
            EnvironmentArtPipeline.ArtSet set = EnvironmentArtPipeline.EnsureAssets();
            Assert.That(set.Rocky.GetTexture("_MainTex"), Is.Not.Null);
            Assert.That(set.Rocky.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(set.Concrete.GetTexture("_MainTex"), Is.Not.Null);
            Assert.That(set.Concrete.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(set.Water.GetTexture("_MainTex"), Is.Not.Null);
            Assert.That(set.Water.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(set.Water.shader.name, Is.EqualTo("Standard"));
        }

        [Test]
        public void WaterVisualDoesNotCreateBlockingCollider()
        {
            GameObject water = new GameObject("VisualWater", typeof(MeshRenderer), typeof(MeshFilter), typeof(RiverSurfaceVisual));
            Assert.That(water.GetComponent<Collider>(), Is.Null);
            Object.DestroyImmediate(water);
        }

        [Test]
        public void DataTexturesAreLinearAndCappedAtTwoK()
        {
            foreach (string path in new[] { EnvironmentArtPipeline.RockyNormalPath, EnvironmentArtPipeline.RockyRoughnessPath, EnvironmentArtPipeline.ConcreteNormalPath, EnvironmentArtPipeline.ConcreteRoughnessPath, EnvironmentArtPipeline.WaterNormalPath, EnvironmentArtPipeline.WaterRoughnessPath })
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.sRGBTexture, Is.False, path);
                Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(2048), path);
            }
        }
    }
}
#endif
