using System.Collections.Generic;
using CierzoArena.Core;
using CierzoArena.Units;
using Unity.Profiling;
using UnityEngine;

namespace CierzoArena.CameraSystem
{
    /// <summary>
    /// Central visual projection of the logical vision sources for one local
    /// observer. Sources never render fog themselves. This component snapshots
    /// them once per tick, updates only their dirty regions and uploads one
    /// persistent mask at most once per visual tick.
    /// </summary>
    public sealed class FogOfWarOverlay : MonoBehaviour
    {
        private const int MinimumMaskResolution = 64;
        private const int MaximumMaskResolution = 256;
        private static readonly ProfilerMarker CollectSourcesMarker = new("FogOfWar.CollectSources");
        private static readonly ProfilerMarker RasterizeMarker = new("FogOfWar.RasterizeDirtyRegions");
        private static readonly ProfilerMarker UploadMarker = new("FogOfWar.UploadMask");
        private static readonly ProfilerMarker BakeMarker = new("FogOfWar.BakeVisualTransition");

        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(1f)] private float halfMapSize = 86f;
        [SerializeField, Range(MinimumMaskResolution, MaximumMaskResolution)] private int maskResolution = 128;
        [SerializeField, Range(10f, 20f)] private float logicUpdateRate = 12f;
        [SerializeField, Range(10f, 20f)] private float visualUpdateRate = 12f;
        [SerializeField, Range(2f, 5f)] private float fogEdgeSoftness = 3f;
        [SerializeField, Range(.15f, .35f)] private float visualTransitionDuration = .24f;
        [SerializeField] private Color fogColor = new(.018f, .04f, .075f, 1f);
        [SerializeField, Range(.55f, .70f)] private float fogExploredAlpha = .64f;
        [SerializeField, Range(.71f, .92f)] private float fogUnexploredAlpha = .82f;

        private readonly List<VisionSource> activeSources = new(64);
        private readonly List<VisionCircle> activeCircles = new(64);
        private readonly List<RectInt> dirtyRegions = new(32);
        private TeamMaskState azureState;
        private TeamMaskState emberState;
        private TeamMaskState activeState;
        private TeamId currentTeam = TeamId.Neutral;
        private Mesh mesh;
        private Material fogMaterial;
        private Material maskBlendMaterial;
        private Texture2D targetMask;
        private RenderTexture displayMaskA;
        private RenderTexture displayMaskB;
        private bool displayAIsCurrent = true;
        private float nextLogicTick;
        private float nextVisualTick;
        private float transitionStartedAt;

        public FilterMode VisionMaskFilterMode => targetMask == null ? FilterMode.Bilinear : targetMask.filterMode;
        public int VisionMaskResolution => maskResolution;
        public float FogEdgeSoftness => fogEdgeSoftness;
        public bool BuffersArePersistent => targetMask != null && displayMaskA != null && displayMaskB != null;
        public int LastActiveSourceCount { get; private set; }
        public int LastDirtyCellCount { get; private set; }
        public int LastTextureUploads { get; private set; }
        public int LastVisualBlits { get; private set; }

        private sealed class TeamMaskState
        {
            public readonly Color32[] Pixels;
            public readonly Dictionary<int, SourceStamp> Sources = new();
            public readonly List<int> RemovedSourceIds = new(16);
            public bool Initialized;
            public int Generation;

            public TeamMaskState(int resolution)
            {
                Pixels = new Color32[resolution * resolution];
                for (int i = 0; i < Pixels.Length; i++) Pixels[i] = new Color32(0, 0, 0, 255);
            }
        }

        private struct SourceStamp
        {
            public Vector3 Position;
            public float Radius;
            public int Generation;
            public SourceStamp(Vector3 position, float radius, int generation)
            {
                Position = position;
                Radius = radius;
                Generation = generation;
            }
        }

        private readonly struct VisionCircle
        {
            public readonly Vector3 Position;
            public readonly float Radius;
            public VisionCircle(Vector3 position, float radius)
            {
                Position = position;
                Radius = radius;
            }
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) targetCamera = Camera.main;
            maskResolution = Mathf.Clamp(maskResolution, MinimumMaskResolution, MaximumMaskResolution);

            CreatePersistentResources();
            // The serialized Resources asset is the player-build dependency. Shader.Find
            // remains only as an Editor/recovery fallback, never as the only reference.
            FogOfWarShaderReferences references = Resources.Load<FogOfWarShaderReferences>("Rendering/FogOfWarShaders");
            Shader fogShader = references != null && references.OverlayShader != null ? references.OverlayShader : Shader.Find("CierzoArena/Fog Of War Soft Overlay");
            Shader blendShader = references != null && references.MaskBlendShader != null ? references.MaskBlendShader : Shader.Find("Hidden/CierzoArena/Fog Mask Blend");
            bool fogShaderSupported = fogShader != null && fogShader.isSupported;
            bool blendShaderSupported = blendShader != null && blendShader.isSupported;
            if (!fogShaderSupported || !blendShaderSupported)
            {
                Debug.LogError($"[FogOfWar] Soft fog overlay unavailable: references={DescribeReferences(references)}, overlay={DescribeShader(fogShader)}, blend={DescribeShader(blendShader)}. The overlay was disabled.", this);
                enabled = false;
                return;
            }

            fogMaterial = new Material(fogShader) { name = "Fog Of War Soft Material", hideFlags = HideFlags.DontSave };
            maskBlendMaterial = new Material(blendShader) { name = "Fog Mask Blend Material", hideFlags = HideFlags.DontSave };
            ConfigureFogMaterial();
            Debug.Log($"[FogOfWar] Soft overlay ready: overlay={fogShader.name}, blend={blendShader.name}.", this);
        }

        private static string DescribeShader(Shader shader)
        {
            return shader == null ? "missing" : $"{shader.name} (supported={shader.isSupported})";
        }

        private static string DescribeReferences(FogOfWarShaderReferences references)
        {
            if (references == null) return "missing";
            return $"loaded (overlay={DescribeShader(references.OverlayShader)}, blend={DescribeShader(references.MaskBlendShader)})";
        }

        private void LateUpdate()
        {
            LastTextureUploads = 0;
            LastVisualBlits = 0;
            if (TryGetObserverTeam(out TeamId observerTeam))
            {
                BindTeam(observerTeam);
                float now = Time.unscaledTime;
                if (now >= nextLogicTick)
                {
                    nextLogicTick = now + 1f / Mathf.Max(1f, logicUpdateRate);
                    CollectDirtyRegions(observerTeam);
                }

                if (dirtyRegions.Count > 0 && now >= nextVisualTick)
                {
                    nextVisualTick = now + 1f / Mathf.Max(1f, visualUpdateRate);
                    UpdateVisualMask();
                }
            }

            if (fogMaterial != null)
            {
                fogMaterial.SetFloat("_Transition", TransitionProgress());
                Draw();
            }
        }

        /// <summary>Fog never removes terrain from the known map; it only darkens it.</summary>
        public bool IsTerrainVisible(Vector3 _) => true;

        private bool TryGetObserverTeam(out TeamId team)
        {
            team = TeamId.Neutral;
            if (LocalHeroProvider.Active == null || LocalHeroProvider.Active.CurrentHero == null) return false;
            TeamMember observer = LocalHeroProvider.Active.CurrentHero.GetComponent<TeamMember>();
            if (observer == null) return false;
            team = observer.Team;
            return team == TeamId.Azure || team == TeamId.Ember;
        }

        private void CreatePersistentResources()
        {
            targetMask = new Texture2D(maskResolution, maskResolution, TextureFormat.RGBA32, false, true)
            {
                name = "Fog Of War Target Mask",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            displayMaskA = CreateDisplayMask("Fog Of War Display A");
            displayMaskB = CreateDisplayMask("Fog Of War Display B");

            mesh = new Mesh { name = "Fog Of War Soft Overlay" };
            float y = .17f;
            mesh.vertices = new[]
            {
                new Vector3(-halfMapSize, y, -halfMapSize), new Vector3(halfMapSize, y, -halfMapSize),
                new Vector3(halfMapSize, y, halfMapSize), new Vector3(-halfMapSize, y, halfMapSize)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
        }

        private RenderTexture CreateDisplayMask(string name)
        {
            RenderTexture result = new(maskResolution, maskResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            result.Create();
            return result;
        }

        private void ConfigureFogMaterial()
        {
            fogMaterial.SetTexture("_FromMask", CurrentDisplayMask());
            fogMaterial.SetTexture("_ToMask", targetMask);
            fogMaterial.SetColor("_FogColor", fogColor);
            fogMaterial.SetFloat("_FogEdgeSoftness", fogEdgeSoftness);
            fogMaterial.SetFloat("_WorldSize", halfMapSize * 2f);
            fogMaterial.SetFloat("_FogExploredAlpha", fogExploredAlpha);
            fogMaterial.SetFloat("_FogUnexploredAlpha", fogUnexploredAlpha);
            fogMaterial.renderQueue = 3010;
        }

        private void BindTeam(TeamId observerTeam)
        {
            if (observerTeam == currentTeam && activeState != null) return;
            currentTeam = observerTeam;
            activeState = observerTeam == TeamId.Azure
                ? azureState ??= new TeamMaskState(maskResolution)
                : emberState ??= new TeamMaskState(maskResolution);
            dirtyRegions.Clear();
            if (activeState.Initialized)
            {
                UploadStateToTargetMask();
                Graphics.Blit(targetMask, CurrentDisplayMask());
            }
            else
            {
                AddDirtyRegion(new RectInt(0, 0, maskResolution, maskResolution));
            }
            transitionStartedAt = Time.unscaledTime;
            ConfigureFogMaterial();
        }

        private void CollectDirtyRegions(TeamId observerTeam)
        {
            if (activeState == null) return;
            LastActiveSourceCount = 0;
            using (CollectSourcesMarker.Auto())
            {
                VisionSource.CopyActiveSourcesTo(observerTeam, activeSources);
                activeCircles.Clear();
                activeState.Generation++;
                int generation = activeState.Generation;
                LastActiveSourceCount = activeSources.Count;

                for (int i = 0; i < activeSources.Count; i++)
                {
                    VisionSource source = activeSources[i];
                    if (source == null) continue;
                    int id = source.StableId;
                    Vector3 position = source.transform.position;
                    float radius = source.Radius;
                    activeCircles.Add(new VisionCircle(position, radius));
                    SourceStamp next = new(position, radius, generation);
                    if (!activeState.Sources.TryGetValue(id, out SourceStamp previous))
                    {
                        AddDirtyCircle(next.Position, next.Radius);
                    }
                    else
                    {
                        bool moved = (previous.Position - next.Position).sqrMagnitude > .0025f;
                        bool radiusChanged = !Mathf.Approximately(previous.Radius, next.Radius);
                        if (moved || radiusChanged)
                        {
                            AddDirtyCircle(previous.Position, previous.Radius);
                            AddDirtyCircle(next.Position, next.Radius);
                        }
                    }
                    activeState.Sources[id] = next;
                }

                activeState.RemovedSourceIds.Clear();
                foreach (KeyValuePair<int, SourceStamp> entry in activeState.Sources)
                {
                    if (entry.Value.Generation != generation) activeState.RemovedSourceIds.Add(entry.Key);
                }
                for (int i = 0; i < activeState.RemovedSourceIds.Count; i++)
                {
                    int id = activeState.RemovedSourceIds[i];
                    AddDirtyCircle(activeState.Sources[id].Position, activeState.Sources[id].Radius);
                    activeState.Sources.Remove(id);
                }
            }

            if (!activeState.Initialized)
            {
                dirtyRegions.Clear();
                AddDirtyRegion(new RectInt(0, 0, maskResolution, maskResolution));
            }
        }

        private void UpdateVisualMask()
        {
            if (activeState == null || dirtyRegions.Count == 0) return;
            // Blend the previous visual transition into a persistent RenderTexture
            // before mutating target pixels. This is one GPU blit per visual tick,
            // never one texture upload per unit or per frame.
            if (activeState.Initialized) BakeCurrentTransition();

            bool changed = false;
            LastDirtyCellCount = 0;
            using (RasterizeMarker.Auto())
            {
                for (int regionIndex = 0; regionIndex < dirtyRegions.Count; regionIndex++)
                {
                    RectInt region = dirtyRegions[regionIndex];
                    for (int y = region.yMin; y < region.yMax; y++)
                    {
                        for (int x = region.xMin; x < region.xMax; x++)
                        {
                            LastDirtyCellCount++;
                            int index = y * maskResolution + x;
                            bool visible = IsVisibleToSnapshot(PixelToWorld(x, y));
                            Color32 current = activeState.Pixels[index];
                            byte visibleValue = visible ? (byte)255 : (byte)0;
                            byte exploredValue = visible || current.g > 0 ? (byte)255 : (byte)0;
                            if (current.r == visibleValue && current.g == exploredValue) continue;
                            activeState.Pixels[index] = new Color32(visibleValue, exploredValue, 0, 255);
                            changed = true;
                        }
                    }
                }
            }

            dirtyRegions.Clear();
            activeState.Initialized = true;
            if (!changed)
            {
                return;
            }

            UploadStateToTargetMask();
            transitionStartedAt = Time.unscaledTime;
        }

        private bool IsVisibleToSnapshot(Vector3 position)
        {
            for (int i = 0; i < activeCircles.Count; i++)
            {
                VisionCircle source = activeCircles[i];
                Vector3 delta = source.Position - position;
                delta.y = 0f;
                float radius = source.Radius;
                if (delta.sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }

        private void UploadStateToTargetMask()
        {
            using (UploadMarker.Auto())
            {
                targetMask.SetPixels32(activeState.Pixels);
                targetMask.Apply(false, false);
            }
            LastTextureUploads++;
        }

        private void BakeCurrentTransition()
        {
            using (BakeMarker.Auto())
            {
                maskBlendMaterial.SetTexture("_FromMask", CurrentDisplayMask());
                maskBlendMaterial.SetTexture("_ToMask", targetMask);
                maskBlendMaterial.SetFloat("_Blend", TransitionProgress());
                Graphics.Blit(Texture2D.blackTexture, NextDisplayMask(), maskBlendMaterial);
                displayAIsCurrent = !displayAIsCurrent;
            }
            LastVisualBlits++;
            ConfigureFogMaterial();
        }

        private float TransitionProgress()
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - transitionStartedAt) / Mathf.Max(.001f, visualTransitionDuration)));
        }

        private void AddDirtyCircle(Vector3 position, float radius)
        {
            float worldSize = halfMapSize * 2f;
            float pixelSize = worldSize / maskResolution;
            int minX = Mathf.FloorToInt((position.x - radius + halfMapSize) / pixelSize) - 1;
            int maxX = Mathf.CeilToInt((position.x + radius + halfMapSize) / pixelSize) + 1;
            int minY = Mathf.FloorToInt((position.z - radius + halfMapSize) / pixelSize) - 1;
            int maxY = Mathf.CeilToInt((position.z + radius + halfMapSize) / pixelSize) + 1;
            int x = Mathf.Clamp(minX, 0, maskResolution);
            int y = Mathf.Clamp(minY, 0, maskResolution);
            int width = Mathf.Clamp(maxX, 0, maskResolution) - x;
            int height = Mathf.Clamp(maxY, 0, maskResolution) - y;
            if (width > 0 && height > 0) AddDirtyRegion(new RectInt(x, y, width, height));
        }

        private void AddDirtyRegion(RectInt region)
        {
            for (int i = 0; i < dirtyRegions.Count; i++)
            {
                RectInt existing = dirtyRegions[i];
                if (!TouchesOrOverlaps(existing, region)) continue;
                region = Union(existing, region);
                dirtyRegions.RemoveAt(i);
                i = -1;
            }
            dirtyRegions.Add(region);
        }

        private static bool TouchesOrOverlaps(RectInt first, RectInt second)
        {
            return first.xMin <= second.xMax && first.xMax >= second.xMin
                && first.yMin <= second.yMax && first.yMax >= second.yMin;
        }

        private static RectInt Union(RectInt first, RectInt second)
        {
            int xMin = Mathf.Min(first.xMin, second.xMin);
            int yMin = Mathf.Min(first.yMin, second.yMin);
            int xMax = Mathf.Max(first.xMax, second.xMax);
            int yMax = Mathf.Max(first.yMax, second.yMax);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private Vector3 PixelToWorld(int x, int y)
        {
            float pixelSize = halfMapSize * 2f / maskResolution;
            return new Vector3(-halfMapSize + (x + .5f) * pixelSize, .18f, -halfMapSize + (y + .5f) * pixelSize);
        }

        private RenderTexture CurrentDisplayMask() => displayAIsCurrent ? displayMaskA : displayMaskB;
        private RenderTexture NextDisplayMask() => displayAIsCurrent ? displayMaskB : displayMaskA;

        private void Draw()
        {
            if (mesh != null && fogMaterial != null && targetCamera != null)
            {
                Graphics.DrawMesh(mesh, Matrix4x4.identity, fogMaterial, 0, targetCamera);
            }
        }

        private void OnDestroy()
        {
            DisposeUnityObject(mesh);
            DisposeUnityObject(fogMaterial);
            DisposeUnityObject(maskBlendMaterial);
            DisposeUnityObject(targetMask);
            DisposeRenderTexture(displayMaskA);
            DisposeRenderTexture(displayMaskB);
        }

        private static void DisposeUnityObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private static void DisposeRenderTexture(RenderTexture value)
        {
            if (value == null) return;
            value.Release();
            DisposeUnityObject(value);
        }
    }
}
