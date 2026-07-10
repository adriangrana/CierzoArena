using CierzoArena.CameraSystem;
using NUnit.Framework;
using UnityEngine;

namespace CierzoArena.Tests.Editor
{
    /// <summary>
    /// M4.3 coverage: the decoupled <see cref="LocalHeroProvider"/> and the MOBA
    /// camera's Follow/Free tracking logic. Provider behavior and the pure decision
    /// functions need no Unity lifecycle, and the controller seams used here
    /// (<see cref="MobaCameraController.SetHeroProvider"/>,
    /// <see cref="MobaCameraController.RecenterOnHero"/>,
    /// <see cref="MobaCameraController.ApplyManualPan"/>) do not depend on Awake/Update
    /// or on real keyboard/mouse. Per-frame follow and real input are validated in play
    /// mode. No real ownership or Netcode is involved.
    /// </summary>
    public sealed class MobaCameraFollowTests
    {
        private const float Tolerance = 1e-4f;

        // ----- LocalHeroProvider ---------------------------------------------

        [Test]
        public void ProviderStartsWithoutHero()
        {
            LocalHeroProvider provider = NewProvider(out GameObject host);
            Assert.That(provider.CurrentHero, Is.Null);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void RegisterSetsCurrentHero()
        {
            LocalHeroProvider provider = NewProvider(out GameObject host);
            Transform hero = NewTransform("Hero");

            provider.Register(hero);

            Assert.That(provider.CurrentHero, Is.EqualTo(hero));
            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void RegisteringSameHeroEmitsNoDuplicateEvents()
        {
            LocalHeroProvider provider = NewProvider(out GameObject host);
            Transform hero = NewTransform("Hero");
            int changes = 0;
            provider.HeroChanged += _ => changes++;

            provider.Register(hero);
            provider.Register(hero);

            Assert.That(changes, Is.EqualTo(1));
            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void RegisteringAnotherHeroReplacesThePrevious()
        {
            LocalHeroProvider provider = NewProvider(out GameObject host);
            Transform first = NewTransform("First");
            Transform second = NewTransform("Second");

            provider.Register(first);
            provider.Register(second);

            Assert.That(provider.CurrentHero, Is.EqualTo(second));
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void UnregisteringCurrentHeroClearsTheReference()
        {
            LocalHeroProvider provider = NewProvider(out GameObject host);
            Transform hero = NewTransform("Hero");

            provider.Register(hero);
            provider.Unregister(hero);

            Assert.That(provider.CurrentHero, Is.Null);
            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void UnregisteringADifferentHeroDoesNotAffectTheCurrentOne()
        {
            LocalHeroProvider provider = NewProvider(out GameObject host);
            Transform hero = NewTransform("Hero");
            Transform other = NewTransform("Other");

            provider.Register(hero);
            provider.Unregister(other);

            Assert.That(provider.CurrentHero, Is.EqualTo(hero));
            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(other.gameObject);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void RegisteringNullDoesNotThrowNorCorruptState()
        {
            LocalHeroProvider provider = NewProvider(out GameObject host);
            Transform hero = NewTransform("Hero");
            provider.Register(hero);

            Assert.DoesNotThrow(() => provider.Register(null));

            Assert.That(provider.CurrentHero, Is.EqualTo(hero));
            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void EventReportsRegistrationAndRemoval()
        {
            LocalHeroProvider provider = NewProvider(out GameObject host);
            Transform hero = NewTransform("Hero");
            Transform reported = null;
            int changes = 0;
            provider.HeroChanged += t => { reported = t; changes++; };

            provider.Register(hero);
            Assert.That(reported, Is.EqualTo(hero));

            provider.Unregister(hero);
            Assert.That(reported, Is.Null);
            Assert.That(changes, Is.EqualTo(2));

            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(host);
        }

        // ----- Camera tracking state -----------------------------------------

        [Test]
        public void InitialStateIsFollow()
        {
            MobaCameraController controller = NewController(out GameObject host);
            Assert.That(controller.Mode, Is.EqualTo(CameraTrackingMode.FollowHero));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void ManualInputSwitchesToFree()
        {
            Assert.That(MobaCameraController.ResolveMode(CameraTrackingMode.FollowHero, true, false),
                Is.EqualTo(CameraTrackingMode.Free));

            MobaCameraController controller = NewController(out GameObject host);
            controller.ApplyManualPan(new Vector2(1f, 0f), Vector2.zero, 1f);
            Assert.That(controller.Mode, Is.EqualTo(CameraTrackingMode.Free));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void ZeroInputDoesNotChangeState()
        {
            Assert.That(MobaCameraController.HasManualPanInput(Vector2.zero, Vector2.zero, 1e-3f), Is.False);
            Assert.That(MobaCameraController.ResolveMode(CameraTrackingMode.FollowHero, false, false),
                Is.EqualTo(CameraTrackingMode.FollowHero));

            MobaCameraController controller = NewController(out GameObject host);
            controller.ApplyManualPan(Vector2.zero, Vector2.zero, 1f);
            Assert.That(controller.Mode, Is.EqualTo(CameraTrackingMode.FollowHero));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void RecenterSwitchesFromFreeToFollow()
        {
            Assert.That(MobaCameraController.ResolveMode(CameraTrackingMode.Free, false, true),
                Is.EqualTo(CameraTrackingMode.FollowHero));

            MobaCameraController controller = NewController(out GameObject host);
            controller.ApplyManualPan(new Vector2(1f, 0f), Vector2.zero, 1f);
            Assert.That(controller.Mode, Is.EqualTo(CameraTrackingMode.Free));
            controller.RecenterOnHero();
            Assert.That(controller.Mode, Is.EqualTo(CameraTrackingMode.FollowHero));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void RecenterWithoutHeroDoesNotThrow()
        {
            MobaCameraController controller = NewController(out GameObject host);
            Assert.DoesNotThrow(() => controller.RecenterOnHero());
            Assert.That(controller.Mode, Is.EqualTo(CameraTrackingMode.FollowHero));
            Object.DestroyImmediate(host);
        }

        [Test]
        public void FollowPreservesY()
        {
            Vector3 pivot = new Vector3(3f, 25f, -7f);
            Vector3 follow = MobaCameraController.ComputeFollowPosition(pivot, new Vector3(50f, 0f, 60f));
            Assert.That(follow.y, Is.EqualTo(25f).Within(Tolerance));
        }

        [Test]
        public void FollowUsesHeroXZ()
        {
            Vector3 pivot = new Vector3(3f, 25f, -7f);
            Vector3 follow = MobaCameraController.ComputeFollowPosition(pivot, new Vector3(50f, 12f, 60f));
            Assert.That(follow.x, Is.EqualTo(50f).Within(Tolerance));
            Assert.That(follow.z, Is.EqualTo(60f).Within(Tolerance));
        }

        [Test]
        public void FollowWithZeroOffsetMatchesPivotOnHero()
        {
            // M4.4 framing offset must default-preserve the M4.3 pivot-on-hero behavior.
            Vector3 pivot = new Vector3(3f, 25f, -7f);
            Vector3 hero = new Vector3(50f, 12f, 60f);
            Vector3 noOffset = MobaCameraController.ComputeFollowPosition(pivot, hero, Vector2.zero);
            Vector3 baseline = MobaCameraController.ComputeFollowPosition(pivot, hero);
            Assert.That(noOffset, Is.EqualTo(baseline));
        }

        [Test]
        public void FollowOffsetShiftsXZAndPreservesY()
        {
            Vector3 pivot = new Vector3(3f, 25f, -7f);
            Vector3 hero = new Vector3(50f, 12f, 60f);
            Vector3 follow = MobaCameraController.ComputeFollowPosition(pivot, hero, new Vector2(4f, -24f));
            Assert.That(follow.x, Is.EqualTo(54f).Within(Tolerance));
            Assert.That(follow.z, Is.EqualTo(36f).Within(Tolerance));
            Assert.That(follow.y, Is.EqualTo(25f).Within(Tolerance));
        }

        [Test]
        public void FollowPreservesRotation()
        {
            MobaCameraController controller = NewController(out GameObject host);
            Quaternion startRotation = Quaternion.Euler(55f, 0f, 0f);
            controller.transform.rotation = startRotation;

            LocalHeroProvider provider = NewProvider(out GameObject providerHost);
            Transform hero = NewTransform("Hero");
            hero.position = new Vector3(20f, 0f, 30f);
            controller.SetHeroProvider(provider);
            provider.Register(hero);

            controller.RecenterOnHero();

            Assert.That(Quaternion.Angle(controller.transform.rotation, startRotation), Is.LessThan(0.01f));
            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(providerHost);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void BoundsLimitRecenterNearACorner()
        {
            // Pure combination of follow + bounds: a hero far past the corner must leave
            // the whole visible region inside the bounds after clamping.
            Quaternion tilt = Quaternion.Euler(55f, 0f, 0f);
            CameraGroundOffsets offsets = MobaCameraController.ComputeVisibleGroundOffsets(tilt, 10f, 16f / 9f, 25f);
            Vector3 follow = MobaCameraController.ComputeFollowPosition(new Vector3(0f, 25f, 0f), new Vector3(500f, 0f, 500f));
            Vector3 clamped = MobaCameraController.ClampPivotToBounds(follow, offsets, -90f, 90f, -90f, 90f);

            Assert.That(clamped.x + offsets.MaxX, Is.LessThanOrEqualTo(90f + 1e-2f));
            Assert.That(clamped.z + offsets.MaxZ, Is.LessThanOrEqualTo(90f + 1e-2f));
            Assert.That(clamped.y, Is.EqualTo(25f).Within(Tolerance));
        }

        [Test]
        public void LosingTheHeroKeepsTheLastPosition()
        {
            MobaCameraController controller = NewController(out GameObject host);
            controller.transform.position = new Vector3(0f, 25f, 0f);

            LocalHeroProvider provider = NewProvider(out GameObject providerHost);
            Transform hero = NewTransform("Hero");
            hero.position = new Vector3(20f, 0f, 30f);
            controller.SetHeroProvider(provider);
            provider.Register(hero);
            controller.RecenterOnHero();
            Vector3 lastPosition = controller.transform.position;

            provider.Unregister(hero);
            controller.RecenterOnHero(); // no hero now: must keep the last position

            Assert.That(controller.transform.position, Is.EqualTo(lastPosition));
            Assert.That(controller.Mode, Is.EqualTo(CameraTrackingMode.FollowHero));
            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(providerHost);
            Object.DestroyImmediate(host);
        }

        [Test]
        public void RegisteringAHeroLaterAllowsFollowingIt()
        {
            MobaCameraController controller = NewController(out GameObject host);
            controller.transform.position = new Vector3(0f, 25f, 0f);

            LocalHeroProvider provider = NewProvider(out GameObject providerHost);
            controller.SetHeroProvider(provider);
            // No hero yet: recenter keeps position but stays in follow intent.
            controller.RecenterOnHero();
            Assert.That(controller.transform.position, Is.EqualTo(new Vector3(0f, 25f, 0f)));

            Transform hero = NewTransform("Hero");
            hero.position = new Vector3(15f, 0f, -25f);
            provider.Register(hero);
            controller.RecenterOnHero();

            Assert.That(controller.transform.position.x, Is.EqualTo(15f).Within(Tolerance));
            Assert.That(controller.transform.position.z, Is.EqualTo(-25f).Within(Tolerance));
            Object.DestroyImmediate(hero.gameObject);
            Object.DestroyImmediate(providerHost);
            Object.DestroyImmediate(host);
        }

        // ----- Fixtures ------------------------------------------------------

        private static LocalHeroProvider NewProvider(out GameObject host)
        {
            host = new GameObject("LocalHeroProvider");
            return host.AddComponent<LocalHeroProvider>();
        }

        private static MobaCameraController NewController(out GameObject host)
        {
            host = new GameObject("MOBA Camera");
            return host.AddComponent<MobaCameraController>();
        }

        private static Transform NewTransform(string name)
        {
            return new GameObject(name).transform;
        }
    }
}
