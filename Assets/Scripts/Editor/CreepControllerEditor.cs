#if UNITY_EDITOR
using CierzoArena.Units;
using UnityEditor;
using UnityEngine;

namespace CierzoArena.EditorTools
{
    /// <summary>Play-mode-only inspector for diagnosing lane-end navigation without
    /// emitting per-frame console logs.</summary>
    [CustomEditor(typeof(CreepController))]
    public sealed class CreepControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("La telemetría de ruta aparece al ejecutar la escena.", MessageType.None);
                return;
            }

            CreepNavigationDebugInfo info = ((CreepController)target).GetNavigationDebugInfo();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Diagnóstico de ruta", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Carril", info.Lane);
            EditorGUILayout.LabelField("Equipo", info.Team.ToString());
            EditorGUILayout.LabelField("Estado", info.CurrentState);
            EditorGUILayout.LabelField("Waypoint", $"{info.CurrentWaypointIndex} / {info.TotalWaypoints}");
            EditorGUILayout.Vector3Field("Destino NavMesh", info.CurrentDestination);
            EditorGUILayout.ObjectField("Objetivo", info.CurrentTarget, typeof(Object), true);
            EditorGUILayout.LabelField("Tipo de objetivo", info.TargetType);
            EditorGUILayout.LabelField("Distancia al objetivo", info.DistanceToTarget.ToString("0.00"));
            EditorGUILayout.LabelField("NavMesh", $"hasPath={info.HasPath}, pending={info.PathPending}, {info.PathStatus}");
            EditorGUILayout.LabelField("Distancia restante", float.IsInfinity(info.RemainingDistance) ? "—" : info.RemainingDistance.ToString("0.00"));
            EditorGUILayout.Toggle("Último waypoint", info.IsAtFinalWaypoint);
            EditorGUILayout.Toggle("Núcleo vulnerable", info.IsCoreVulnerable);
            EditorGUILayout.Toggle("Núcleo seleccionable", info.IsCoreTargetable);
            EditorGUILayout.Toggle("Núcleo alcanzable", info.IsCoreReachable);
        }
    }
}
#endif
