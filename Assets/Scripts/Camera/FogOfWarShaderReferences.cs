using UnityEngine;

namespace CierzoArena.CameraSystem
{
    /// <summary>
    /// Serialized build dependency for the two shaders created at runtime by the
    /// fog overlay. A Shader.Find-only path is stripped from Windows players.
    /// </summary>
    public sealed class FogOfWarShaderReferences : ScriptableObject
    {
        [SerializeField] private Shader overlayShader;
        [SerializeField] private Shader maskBlendShader;

        public Shader OverlayShader => overlayShader;
        public Shader MaskBlendShader => maskBlendShader;
    }
}
