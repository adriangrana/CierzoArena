using System.Collections;
using CierzoArena.Frontend;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CierzoArena.Tests.PlayMode
{
    public sealed class RiverSurfaceVisualPlayModeTests
    {
        [UnityTest]
        public IEnumerator VisualWaterAnimatesWithoutColliderOrMaterialClone()
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(water.GetComponent<Collider>());
            Material shared = new Material(Shader.Find("Standard"));
            water.GetComponent<Renderer>().sharedMaterial = shared;
            water.AddComponent<RiverSurfaceVisual>();

            yield return null;
            yield return null;

            Assert.That(water.GetComponent<Collider>(), Is.Null);
            Assert.That(water.GetComponent<Renderer>().sharedMaterial, Is.SameAs(shared));
            Object.Destroy(water); Object.Destroy(shared);
        }
    }
}
