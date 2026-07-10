using System.Reflection;
using CierzoArena.CameraSystem;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    /// <summary>
    /// M4.4 coverage for <see cref="SceneLocalHeroRegistrar"/>, the scene glue that
    /// registers a specific hero into a <see cref="LocalHeroProvider"/> through the
    /// provider's existing public API. The serialized references are set the same way
    /// the builder wires them; registration is exercised via the public
    /// <see cref="SceneLocalHeroRegistrar.Register"/> seam, so no play mode is needed.
    /// </summary>
    public sealed class SceneLocalHeroRegistrarTests
    {
        [Test]
        public void RegisterRegistersConfiguredHeroIntoProvider()
        {
            LocalHeroProvider provider = NewProvider(out GameObject providerHost);
            Transform hero = new GameObject("Hero").transform;
            SceneLocalHeroRegistrar registrar = NewRegistrar(out GameObject registrarHost, provider, hero);

            registrar.Register();

            Assert.That(provider.CurrentHero, Is.EqualTo(hero));
            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(registrarHost);
            Object.DestroyImmediate(providerHost);
        }

        [Test]
        public void RegisterWithoutProviderDoesNotThrow()
        {
            Transform hero = new GameObject("Hero").transform;
            SceneLocalHeroRegistrar registrar = NewRegistrar(out GameObject registrarHost, null, hero);

            Assert.DoesNotThrow(() => registrar.Register());

            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(registrarHost);
        }

        [Test]
        public void RegisterWithoutHeroDoesNotRegisterNorThrow()
        {
            LocalHeroProvider provider = NewProvider(out GameObject providerHost);
            SceneLocalHeroRegistrar registrar = NewRegistrar(out GameObject registrarHost, provider, null);

            Assert.DoesNotThrow(() => registrar.Register());
            Assert.That(provider.CurrentHero, Is.Null);

            Object.DestroyImmediate(registrarHost);
            Object.DestroyImmediate(providerHost);
        }

        private static LocalHeroProvider NewProvider(out GameObject host)
        {
            host = new GameObject("LocalHeroProvider");
            return host.AddComponent<LocalHeroProvider>();
        }

        private static SceneLocalHeroRegistrar NewRegistrar(out GameObject host, LocalHeroProvider provider, Transform hero)
        {
            host = new GameObject("Registrar");
            SceneLocalHeroRegistrar registrar = host.AddComponent<SceneLocalHeroRegistrar>();
            SetPrivateField(registrar, "provider", provider);
            SetPrivateField(registrar, "hero", hero);
            return registrar;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Serialized field '{fieldName}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
