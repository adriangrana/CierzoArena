using UnityEngine;

namespace CierzoArena.Frontend
{
    /// <summary>Small compatible water cue for the prototype river: a slow luminance
    /// pulse, no collider and no NavMesh participation.</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class RiverSurfaceVisual : MonoBehaviour
    {
        private Material material; private Color baseColor;
        private void Awake(){material=GetComponent<Renderer>().material;baseColor=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.color;}
        private void Update(){if(material==null)return;Color color=Color.Lerp(baseColor,Color.cyan,.08f*(.5f+.5f*Mathf.Sin(Time.time*.55f)));if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);else material.color=color;}
    }
}
