using System.Collections.Generic;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    [RequireComponent(typeof(TeamMember))]
    public sealed class VisionSource : MonoBehaviour
    {
        private static readonly List<VisionSource> sources = new();
        [SerializeField, Min(0f)] private float radius = 12f;
        private TeamMember team; private Health health; private StructureEntity structure;
        public TeamId Team { get { Ensure(); return team != null ? team.Team : TeamId.Neutral; } }
        public float Radius => Mathf.Max(0f, radius);
        public StructureEntity Structure { get { Ensure(); return structure; } }
        public bool IsActiveVision { get { Ensure(); return gameObject.activeInHierarchy && team != null && (health == null || health.IsAlive) && (structure == null || !structure.IsDestroyed); } }
        private void Awake(){Ensure();Register();} private void OnEnable()=>Register(); private void OnDisable()=>sources.Remove(this);
        public void EnsureRegistered(){Ensure();Register();}
        private void Ensure(){if(team==null)team=GetComponent<TeamMember>(); if(health==null)TryGetComponent(out health); if(structure==null)TryGetComponent(out structure);}
        private void Register(){if(!sources.Contains(this))sources.Add(this);}
        public static bool IsVisible(TeamId observer, Vector3 position)
        {
            for(int i=0;i<sources.Count;i++){VisionSource source=sources[i];if(source!=null&&source.Team==observer&&source.IsActiveVision){Vector3 delta=source.transform.position-position;delta.y=0f;if(delta.sqrMagnitude<=source.Radius*source.Radius)return true;}}
            return false;
        }
        public static void CopySourcesTo(List<VisionSource> destination)
        {
            destination.Clear();
            for (int i = 0; i < sources.Count; i++) if (sources[i] != null) destination.Add(sources[i]);
        }
    }
}
