#if UNITY_EDITOR
using CierzoArena.Combat;
using CierzoArena.EditorTools;
using CierzoArena.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace CierzoArena.Tests.Editor
{
    /// <summary>Validates the first optional hero-specific visual without requiring a
    /// live match. The builder is idempotent, so tests verify its serialized output
    /// rather than relying on a manual Inspector assignment.</summary>
    public sealed class HeroVisualPresentationTests
    {
        private HeroVisualCatalog catalog;
        private GameObject stormWardenPrefab;

        [SetUp]
        public void BuildVisualAssets()
        {
            StormWardenVisualAssetBuilder.Build();
            catalog = AssetDatabase.LoadAssetAtPath<HeroVisualCatalog>(StormWardenVisualAssetBuilder.CatalogPath);
            stormWardenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StormWardenVisualAssetBuilder.PrefabPath);
        }

        [Test]
        public void StormWardenHasAnOptionalVisualPrefabConfiguredByItsStableHeroId()
        {
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet("storm_warden", out HeroVisualDefinition visual), Is.True);
            Assert.That(visual.VisualPrefab, Is.EqualTo(stormWardenPrefab));
            Assert.That(visual.HidePlaceholder, Is.True);
        }

        [Test]
        public void StormWardenVisualPrefabIsVisualOnly()
        {
            Assert.That(stormWardenPrefab, Is.Not.Null);
            Assert.That(stormWardenPrefab.GetComponentInChildren<Health>(true), Is.Null);
            Assert.That(stormWardenPrefab.GetComponentInChildren<ClickMover>(true), Is.Null);
            Assert.That(stormWardenPrefab.GetComponentInChildren<HeroAbilities>(true), Is.Null);
            Assert.That(stormWardenPrefab.GetComponentInChildren<NavMeshAgent>(true), Is.Null);
            Assert.That(stormWardenPrefab.GetComponentInChildren<Camera>(true), Is.Null);
            Assert.That(stormWardenPrefab.GetComponentsInChildren<Collider>(true), Is.Empty);

            // Avoid a hard test-assembly dependency on NGO types while still proving
            // the visual prefab does not carry networking components.
            Assert.That(HasComponentNamed(stormWardenPrefab, "NetworkObject"), Is.False);
            Assert.That(HasComponentNamed(stormWardenPrefab, "NetworkTransform"), Is.False);
        }

        [Test]
        public void StormWardenVisualUsesBuiltInStandardAndNormalMapImport()
        {
            Renderer[] renderers = stormWardenPrefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0));
            foreach (Renderer renderer in renderers)
            {
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Standard"));
                Assert.That(renderer.sharedMaterial.GetTexture("_MainTex"), Is.Not.Null);
                Assert.That(renderer.sharedMaterial.GetTexture("_BumpMap"), Is.Not.Null);
                Assert.That(renderer.sharedMaterial.GetTexture("_MetallicGlossMap"), Is.Not.Null);
            }

            TextureImporter normal = AssetImporter.GetAtPath(StormWardenVisualAssetBuilder.NormalPath) as TextureImporter;
            Assert.That(normal, Is.Not.Null);
            Assert.That(normal.textureType, Is.EqualTo(TextureImporterType.NormalMap));
        }

        [Test]
        public void StormWardenModelImporterIsStaticAndDoesNotGenerateGameplayColliders()
        {
            ModelImporter model = AssetImporter.GetAtPath(StormWardenVisualAssetBuilder.ModelPath) as ModelImporter;
            Assert.That(model, Is.Not.Null);
            Assert.That(model.importCameras, Is.False);
            Assert.That(model.importLights, Is.False);
            Assert.That(model.isReadable, Is.False);
            Assert.That(model.addCollider, Is.False);
            Assert.That(model.importAnimation, Is.False);
            Assert.That(model.animationType, Is.EqualTo(ModelImporterAnimationType.None));
        }

        [Test]
        public void OtherRosterHeroesRemainAllowedToUseThePlaceholder()
        {
            foreach (HeroDefinition hero in HeroCatalog.Shared.Heroes)
            {
                if (hero.HeroId == "storm_warden") continue;
                Assert.That(catalog.Resolve(hero.HeroId), Is.Null, $"{hero.HeroId} must retain the current placeholder until its own visual is authored.");
            }
        }

        [Test]
        public void ResolverUsesHeroIdRatherThanDisplayName()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                HeroVisualController controller = root.AddComponent<HeroVisualController>();
                HeroDefinition wrongId = CreateDefinition("unmodelled_id", "Storm Warden");
                controller.Apply(wrongId, catalog);
                Assert.That(controller.ActiveVisualInstance, Is.Null);

                HeroDefinition rightId = CreateDefinition("storm_warden", "A completely different visible name");
                controller.Apply(rightId, catalog);
                Assert.That(controller.ActiveVisualInstance, Is.Not.Null);
                Object.DestroyImmediate(wrongId);
                Object.DestroyImmediate(rightId);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ResolverDoesNotDuplicateAndClearRestoresPlaceholder()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                Renderer placeholder = root.GetComponent<Renderer>();
                HeroVisualController controller = root.AddComponent<HeroVisualController>();
                HeroDefinition storm = CreateDefinition("storm_warden", "Storm Warden");
                controller.Apply(storm, catalog);
                GameObject first = controller.ActiveVisualInstance;
                controller.Apply(storm, catalog);
                Assert.That(controller.ActiveVisualInstance, Is.SameAs(first));
                Assert.That(root.transform.Find(HeroVisualController.VisualRootName).GetComponentsInChildren<HeroVisualPrefabMetadata>(true).Length, Is.EqualTo(1));
                Assert.That(placeholder.enabled, Is.False);

                controller.ClearVisual();
                Assert.That(controller.ActiveVisualInstance, Is.Null);
                Assert.That(placeholder.enabled, Is.True);
                Object.DestroyImmediate(storm);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void VisualRootLeavesSelectionRingAndHealthBarAsGameplaySiblings()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            GameObject ring = new GameObject("Selection Ring");
            GameObject bar = new GameObject("Health Bar");
            try
            {
                ring.transform.SetParent(root.transform);
                bar.transform.SetParent(root.transform);
                HeroVisualController controller = root.AddComponent<HeroVisualController>();
                HeroDefinition storm = CreateDefinition("storm_warden", "Storm Warden");
                controller.Apply(storm, catalog);
                Assert.That(root.transform.Find("Selection Ring"), Is.EqualTo(ring.transform));
                Assert.That(root.transform.Find("Health Bar"), Is.EqualTo(bar.transform));
                Assert.That(root.transform.Find(HeroVisualController.VisualRootName), Is.Not.Null);
                Object.DestroyImmediate(storm);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void BuilderReferenceAndNormalizedBoundsSurviveRebuild()
        {
            StormWardenVisualAssetBuilder.Build();
            HeroVisualCatalog rebuiltCatalog = AssetDatabase.LoadAssetAtPath<HeroVisualCatalog>(StormWardenVisualAssetBuilder.CatalogPath);
            Assert.That(rebuiltCatalog.TryGet("storm_warden", out HeroVisualDefinition visual), Is.True);
            Assert.That(visual.VisualPrefab, Is.Not.Null);
            HeroVisualPrefabMetadata metadata = visual.VisualPrefab.GetComponent<HeroVisualPrefabMetadata>();
            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.RendererBounds.size.y, Is.EqualTo(2.15f).Within(0.02f));
            Assert.That(metadata.RendererBounds.min.y, Is.EqualTo(0f).Within(0.02f));
        }

        private static HeroDefinition CreateDefinition(string heroId, string displayName)
        {
            HeroDefinition definition = ScriptableObject.CreateInstance<HeroDefinition>();
            definition.ConfigureRuntime(heroId, displayName, string.Empty, string.Empty, HeroRole.Mage, HeroRole.Controller,
                HeroAttackStyle.Ranged, 1, Color.white, new HeroStats(1f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 1f, 0f, 1f, 1f, 0f, 0f, 1f), new AbilityDefinition[4]);
            return definition;
        }

        private static bool HasComponentNamed(GameObject root, string typeName)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == typeName) return true;
            }
            return false;
        }
    }
}
#endif