using CierzoArena.Environment;
using CierzoArena.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    /// <summary>M23: deterministic checks for the reusable base layout and the
    /// Fantasy Village palette. These do not require Play Mode or the package.</summary>
    public sealed class FantasyVillageM23Tests
    {
        private static readonly Vector3 AzureBase = new Vector3(-60f, 0f, -60f);
        private static readonly Vector3 EmberBase = new Vector3(60f, 0f, 60f);

        private static TeamBaseLayoutDefinition Layout() => ScriptableObject.CreateInstance<TeamBaseLayoutDefinition>();

        [Test]
        public void HeroSpawnIsBehindCoreForBothTeams()
        {
            TeamBaseLayoutDefinition layout = Layout();
            foreach (Vector3 baseCenter in new[] { AzureBase, EmberBase })
            {
                TeamBaseLayoutDefinition.Resolved r = layout.Resolve(baseCenter);
                Vector3 toSpawn = r.HeroSpawn - r.Core; toSpawn.y = 0f;
                Assert.That(Vector3.Dot(toSpawn, r.Forward), Is.LessThan(0f), "Spawn must be behind the core (away from map centre).");
            }
            Object.DestroyImmediate(layout);
        }

        [Test]
        public void CoreGuardsAndGatewaysAreInFront()
        {
            TeamBaseLayoutDefinition layout = Layout();
            TeamBaseLayoutDefinition.Resolved r = layout.Resolve(AzureBase);
            Assert.That(Vector3.Dot(r.CoreGuardLeft - r.Core, r.Forward), Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(r.CoreGuardRight - r.Core, r.Forward), Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(r.MidGateway - r.Core, r.Forward), Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(r.TopGateway - r.Core, r.Forward), Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(r.BottomGateway - r.Core, r.Forward), Is.GreaterThan(0f));
            Object.DestroyImmediate(layout);
        }

        [Test]
        public void AzureAndEmberShareTheSameRelativeFootprint()
        {
            TeamBaseLayoutDefinition layout = Layout();
            TeamBaseLayoutDefinition.Resolved a = layout.Resolve(AzureBase);
            TeamBaseLayoutDefinition.Resolved e = layout.Resolve(EmberBase);

            float ForwardAmount(TeamBaseLayoutDefinition.Resolved r, Vector3 p) => Vector3.Dot(p - r.Core, r.Forward);
            float RightAmount(TeamBaseLayoutDefinition.Resolved r, Vector3 p) => Vector3.Dot(p - r.Core, r.Right);

            Assert.That(ForwardAmount(a, a.HeroSpawn), Is.EqualTo(ForwardAmount(e, e.HeroSpawn)).Within(1e-3f));
            Assert.That(ForwardAmount(a, a.CoreGuardLeft), Is.EqualTo(ForwardAmount(e, e.CoreGuardLeft)).Within(1e-3f));
            Assert.That(RightAmount(a, a.CoreGuardLeft), Is.EqualTo(RightAmount(e, e.CoreGuardLeft)).Within(1e-3f));
            Assert.That(RightAmount(a, a.CoreGuardRight), Is.EqualTo(RightAmount(e, e.CoreGuardRight)).Within(1e-3f));
            Object.DestroyImmediate(layout);
        }

        [Test]
        public void ResolveIsDeterministic()
        {
            TeamBaseLayoutDefinition layout = Layout();
            TeamBaseLayoutDefinition.Resolved a = layout.Resolve(AzureBase);
            TeamBaseLayoutDefinition.Resolved b = layout.Resolve(AzureBase);
            Assert.That(a.HeroSpawn, Is.EqualTo(b.HeroSpawn));
            Assert.That(a.MidGateway, Is.EqualTo(b.MidGateway));
            Object.DestroyImmediate(layout);
        }

        [Test]
        public void EmptyPaletteIsInvalidAndPopulatedPaletteIsValid()
        {
            FantasyVillageEnvironmentPalette palette = ScriptableObject.CreateInstance<FantasyVillageEnvironmentPalette>();
            Assert.That(palette.Validate(out _), Is.False, "An empty palette must be invalid.");

            GameObject dummy = new GameObject("Dummy");
            GameObject[] one = { dummy };
            palette.SetAll(
                main: dummy, secondary: one, houses: one,
                straight: dummy, pieces: one, bridgePrefab: dummy,
                treeSet: one, pineSet: one, flowerSet: one, pot: dummy,
                cliffSet: one, mountainSet: one, rockSet: one,
                benchPrefab: dummy, cratePrefab: dummy, fencePrefab: dummy, lanternPrefab: dummy, boatPrefab: dummy);

            Assert.That(palette.Validate(out string report), Is.True, report);

            Object.DestroyImmediate(dummy);
            Object.DestroyImmediate(palette);
        }

        [Test]
        public void BuiltInVariantPreservesPackageAtlasAndUsesGuidMapping()
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/OccaSoftware/Low Poly Fantasy Village/Materials/Color.mat");
            Assert.That(source, Is.Not.Null, "The audited package material must be present.");

            Material variant = FantasyVillageMaterialRemapper.GetVariant(source);
            Assert.That(variant, Is.Not.Null);
            Assert.That(variant.shader.name, Is.EqualTo("Standard"));
            Assert.That(variant.GetTexture("_MainTex"), Is.Not.Null,
                "The Built-in variant must copy the source _BaseMap atlas to _MainTex.");
            Assert.That(FantasyVillageMaterialRemapper.IsVariantFor(source, variant), Is.True,
                "The persistent variant mapping must be keyed by original GUID, never only its name.");
        }

        [Test]
        public void BuiltInVariantPreservesSourceColourWithoutMutatingOriginal()
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/OccaSoftware/Low Poly Fantasy Village/Materials/Light.mat");
            Assert.That(source, Is.Not.Null);

            Texture atlasBefore = FantasyVillageMaterialRemapper.GetSourceMainTexture(source);
            Color colourBefore = FantasyVillageMaterialRemapper.GetSourceBaseColor(source);
            string shaderBefore = source.shader == null ? string.Empty : source.shader.name;

            Material variant = FantasyVillageMaterialRemapper.GetVariant(source);

            Assert.That(variant.GetTexture("_MainTex"), Is.EqualTo(atlasBefore));
            Assert.That((variant.GetColor("_Color") - colourBefore).maxColorComponent, Is.LessThan(0.001f));
            Assert.That(FantasyVillageMaterialRemapper.GetSourceMainTexture(source), Is.EqualTo(atlasBefore));
            Assert.That((FantasyVillageMaterialRemapper.GetSourceBaseColor(source) - colourBefore).maxColorComponent, Is.LessThan(0.001f));
            Assert.That(source.shader == null ? string.Empty : source.shader.name, Is.EqualTo(shaderBefore),
                "Generating a variant must not edit the original package material.");
        }

        [Test]
        public void RemapInstanceIncludesInactiveChildrenAndKeepsMaterialSlotCount()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FantasyVillagePaletteBuilder.PackagePrefabRoot + "/House_3.prefab");
            Assert.That(prefab, Is.Not.Null);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                Renderer[] before = instance.GetComponentsInChildren<Renderer>(true);
                int[] slotCounts = new int[before.Length];
                for (int i = 0; i < before.Length; i++) slotCounts[i] = before[i].sharedMaterials.Length;
                Renderer sourceRenderer = before.Length > 0 ? PrefabUtility.GetCorrespondingObjectFromSource(before[0]) : null;
                Material[] originalMaterials = sourceRenderer != null ? (Material[])sourceRenderer.sharedMaterials.Clone() : new Material[0];
                instance.SetActive(false);

                FantasyVillageMaterialRemapper.RemapInstance(instance);

                Renderer[] after = instance.GetComponentsInChildren<Renderer>(true);
                Assert.That(after.Length, Is.EqualTo(before.Length));
                for (int i = 0; i < after.Length; i++)
                {
                    Assert.That(after[i].sharedMaterials.Length, Is.EqualTo(slotCounts[i]));
                    foreach (Material material in after[i].sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null);
                        Assert.That(FantasyVillageMaterialRemapper.NeedsRemap(material), Is.False);
                    }
                }
                if (sourceRenderer != null)
                {
                    Assert.That(sourceRenderer.sharedMaterials, Is.EqualTo(originalMaterials),
                        "Remapping an instance must never write back into the package prefab source.");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
