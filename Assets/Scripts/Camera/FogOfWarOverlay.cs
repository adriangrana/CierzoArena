using System.Collections.Generic;
using CierzoArena.Core;
using CierzoArena.Units;
using UnityEngine;

namespace CierzoArena.CameraSystem
{
    /// <summary>One reusable mesh darkens terrain outside local team vision.</summary>
    public sealed class FogOfWarOverlay : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(1f)] private float halfMapSize = 86f;
        [SerializeField, Min(.5f)] private float cellSize = 4f;
        [SerializeField, Min(.02f)] private float refreshInterval = .12f;
        [SerializeField] private Color fogColor = new Color(.02f, .04f, .06f, .62f);
        private readonly List<Vector3> vertices = new();
        private readonly List<int> triangles = new();
        private Mesh mesh; private Material material; private float nextRefresh; private TeamId currentTeam = TeamId.Neutral;
        private void Awake()
        {
            if(targetCamera==null)targetCamera=GetComponent<Camera>();if(targetCamera==null)targetCamera=Camera.main;
            mesh=new Mesh { name="Fog Of War Overlay" };
            // This project uses the Built-in Render Pipeline. A URP-only material
            // rendered by Graphics.DrawMesh covers the world in error-magenta.
            Shader shader=Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
            if(shader==null||!shader.isSupported)
            {
                Debug.LogError("[FogOfWar] No supported Built-in transparent shader was found; the overlay was disabled.",this);
                enabled=false;
                return;
            }
            material=new Material(shader){name="Fog Of War Material",hideFlags=HideFlags.DontSave};
            if(material.HasProperty("_Color"))material.SetColor("_Color",fogColor);
            material.renderQueue=3000;
        }
        private void LateUpdate()
        {
            if(LocalHeroProvider.Active==null||LocalHeroProvider.Active.CurrentHero==null)return;TeamMember observer=LocalHeroProvider.Active.CurrentHero.GetComponent<TeamMember>();if(observer==null)return;if(observer.Team!=currentTeam||Time.unscaledTime>=nextRefresh){currentTeam=observer.Team;nextRefresh=Time.unscaledTime+refreshInterval;Rebuild(observer.Team);}if(mesh!=null&&material!=null&&targetCamera!=null)Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,targetCamera);
        }
        /// <summary>Fog never removes terrain from the known map; it only darkens it.</summary>
        public bool IsTerrainVisible(Vector3 _) => true;
        private void Rebuild(TeamId observerTeam)
        {
            vertices.Clear();triangles.Clear();float step=Mathf.Max(.5f,cellSize);for(float z=-halfMapSize;z<halfMapSize;z+=step)for(float x=-halfMapSize;x<halfMapSize;x+=step){Vector3 center=new Vector3(x+step*.5f,.08f,z+step*.5f);if(VisionSource.IsVisible(observerTeam,center))continue;int i=vertices.Count;vertices.Add(new Vector3(x,.08f,z));vertices.Add(new Vector3(x+step,.08f,z));vertices.Add(new Vector3(x+step,.08f,z+step));vertices.Add(new Vector3(x,.08f,z+step));triangles.Add(i);triangles.Add(i+2);triangles.Add(i+1);triangles.Add(i);triangles.Add(i+3);triangles.Add(i+2);}mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0,true);mesh.RecalculateBounds();
        }
        private void OnDestroy(){if(Application.isPlaying){Destroy(mesh);Destroy(material);}else{DestroyImmediate(mesh);DestroyImmediate(material);}}
    }
}
