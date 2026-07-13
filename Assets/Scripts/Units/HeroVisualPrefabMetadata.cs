using UnityEngine;

namespace CierzoArena.Units
{
    /// <summary>Read-only authored measurements of a static hero visual. This is
    /// presentation metadata only; it carries no gameplay or networking behaviour.</summary>
    public sealed class HeroVisualPrefabMetadata : MonoBehaviour
    {
        [SerializeField] private Bounds rendererBounds;
        [SerializeField] private Vector3 modelRootLocalPosition;
        [SerializeField] private Vector3 modelRootLocalEulerAngles;
        [SerializeField] private Vector3 modelRootLocalScale = Vector3.one;

        public Bounds RendererBounds => rendererBounds;
        public Vector3 ModelRootLocalPosition => modelRootLocalPosition;
        public Vector3 ModelRootLocalEulerAngles => modelRootLocalEulerAngles;
        public Vector3 ModelRootLocalScale => modelRootLocalScale;

        public void Configure(Bounds bounds, Transform modelRoot)
        {
            rendererBounds = bounds;
            modelRootLocalPosition = modelRoot.localPosition;
            modelRootLocalEulerAngles = modelRoot.localEulerAngles;
            modelRootLocalScale = modelRoot.localScale;
        }
    }
}