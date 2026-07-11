using System.Collections.Generic;
using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    [RequireComponent(typeof(StatusEffectController))]
    public sealed class NetworkStatusEffects : NetworkBehaviour
    {
        private readonly NetworkList<int> types=new(null,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly NetworkList<float> remaining=new(null,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly NetworkList<float> magnitudes=new(null,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
        private readonly List<StatusEffectState> buffer=new(); private StatusEffectController effects;
        private void Awake(){effects=GetComponent<StatusEffectController>();effects.SetAuthorityEnabled(false);}
        public override void OnNetworkSpawn(){if(IsServer){effects.SetAuthorityEnabled(true);effects.Changed+=OnChanged;Publish();}else{types.OnListChanged+=OnTypes;remaining.OnListChanged+=OnRemaining;magnitudes.OnListChanged+=OnMagnitudes;Apply();}}
        public override void OnNetworkDespawn(){if(IsServer)effects.Changed-=OnChanged;else{types.OnListChanged-=OnTypes;remaining.OnListChanged-=OnRemaining;magnitudes.OnListChanged-=OnMagnitudes;}}
        private void Update(){if(IsServer&&IsSpawned)Publish();}
        private void OnChanged(StatusEffectController _)=>Publish();
        private void Publish(){effects.CopyStatesTo(buffer);while(types.Count>buffer.Count){types.RemoveAt(types.Count-1);remaining.RemoveAt(remaining.Count-1);magnitudes.RemoveAt(magnitudes.Count-1);}while(types.Count<buffer.Count){types.Add(0);remaining.Add(0);magnitudes.Add(0);}for(int i=0;i<buffer.Count;i++){types[i]=(int)buffer[i].Type;remaining[i]=buffer[i].Remaining;magnitudes[i]=buffer[i].Magnitude;}}
        private void Apply(){buffer.Clear();for(int i=0;i<types.Count&&i<remaining.Count&&i<magnitudes.Count;i++)buffer.Add(new StatusEffectState((StatusEffectType)types[i],remaining[i],magnitudes[i]));effects.ApplyAuthoritativeStates(buffer);}
        private void OnTypes(NetworkListEvent<int> _)=>Apply(); private void OnRemaining(NetworkListEvent<float> _)=>Apply(); private void OnMagnitudes(NetworkListEvent<float> _)=>Apply();
    }
}
