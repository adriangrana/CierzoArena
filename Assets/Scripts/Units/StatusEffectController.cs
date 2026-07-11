using System;
using System.Collections.Generic;
using CierzoArena.Combat;
using CierzoArena.Core;
using CierzoArena.Structures;
using UnityEngine;

namespace CierzoArena.Units
{
    public enum StatusEffectType { Stun, Root, Silence, Slow, Shield, DamageBuff, MoveSpeedBuff }
    public enum StatusStackRule { RefreshDuration, ReplaceIfStronger, StackMagnitude }
    [Serializable] public struct StatusEffectSpec { public string Id; public StatusEffectType Type; public float Duration; public float Magnitude; public StatusStackRule StackRule; public bool ClearOnDeath; }
    public readonly struct StatusEffectState { public readonly StatusEffectType Type; public readonly float Remaining; public readonly float Magnitude; public StatusEffectState(StatusEffectType type,float remaining,float magnitude){Type=type;Remaining=remaining;Magnitude=magnitude;} }
    [RequireComponent(typeof(Health))]
    public sealed class StatusEffectController : MonoBehaviour
    {
        private sealed class Active { public StatusEffectSpec spec; public float remaining; }
        private readonly List<Active> active = new();
        private Health health; private ClickMover mover; private BasicAttack attack; private HeroAbilities abilities; private bool authorityEnabled = true;
        public bool IsStunned => Has(StatusEffectType.Stun); public bool IsRooted => Has(StatusEffectType.Root); public bool IsSilenced => Has(StatusEffectType.Silence);
        public float Shield { get; private set; }
        public event Action<StatusEffectController> Changed;
        private void Awake() { health = GetComponent<Health>(); TryGetComponent(out mover); TryGetComponent(out attack); TryGetComponent(out abilities); health.Died += OnDied; }
        private void OnDestroy() { if (health != null) health.Died -= OnDied; }
        private void Update() => Simulate(Time.deltaTime);
        public void SetAuthorityEnabled(bool enabled) => authorityEnabled = enabled;
        public bool CanMove => !IsStunned && !IsRooted; public bool CanAttack => !IsStunned; public bool CanCast => !IsStunned && !IsSilenced;
        public bool Apply(StatusEffectSpec spec)
        {
            if (!authorityEnabled || (MatchStateController.Active != null && !MatchStateController.Active.IsPlaying) || string.IsNullOrWhiteSpace(spec.Id)) return false;
            spec.Duration = Mathf.Max(0f, spec.Duration); spec.Magnitude = Mathf.Max(0f, spec.Magnitude);
            Active existing = active.Find(x => x.spec.Id == spec.Id);
            if (existing != null) { if (spec.StackRule == StatusStackRule.ReplaceIfStronger) existing.spec.Magnitude = Mathf.Max(existing.spec.Magnitude, spec.Magnitude); else if (spec.StackRule == StatusStackRule.StackMagnitude) existing.spec.Magnitude += spec.Magnitude; existing.remaining = spec.Duration; Recalculate(); return true; }
            active.Add(new Active { spec = spec, remaining = spec.Duration }); Recalculate(); return true;
        }
        public void Simulate(float delta)
        {
            if (!authorityEnabled) return; bool changed = false;
            for (int i=active.Count-1;i>=0;i--) { active[i].remaining -= Mathf.Max(0f,delta); if(active[i].remaining<=0f){active.RemoveAt(i);changed=true;} }
            if(changed) Recalculate();
        }
        public float AbsorbDamage(float amount) { float absorbed=Mathf.Min(Mathf.Max(0f,amount),Shield); if(absorbed>0f){Shield-=absorbed; if(Shield<=0f) active.RemoveAll(x=>x.spec.Type==StatusEffectType.Shield); Changed?.Invoke(this);} return amount-absorbed; }
        public void ClearOnDeath() { if(!authorityEnabled)return; active.RemoveAll(x=>x.spec.ClearOnDeath); Recalculate(); }
        public void ClearAll() { if(!authorityEnabled)return; active.Clear(); Recalculate(); }
        public void CopyStatesTo(List<StatusEffectState> destination) { destination.Clear(); foreach (Active effect in active) destination.Add(new StatusEffectState(effect.spec.Type, Mathf.Max(0f,effect.remaining), effect.spec.Magnitude)); }
        public void ApplyAuthoritativeStates(IReadOnlyList<StatusEffectState> states)
        {
            authorityEnabled=false; active.Clear();
            if(states!=null) for(int i=0;i<states.Count;i++) active.Add(new Active { spec=new StatusEffectSpec { Id=$"replicated.{i}.{states[i].Type}", Type=states[i].Type, Magnitude=states[i].Magnitude, ClearOnDeath=true }, remaining=states[i].Remaining });
            Recalculate();
        }
        private bool Has(StatusEffectType type) => active.Exists(x=>x.spec.Type==type && x.remaining>0f);
        private void OnDied(Health _) => ClearOnDeath();
        private void Recalculate() { Shield=0f; float slow=0f,dmg=0f,move=0f; foreach(var x in active){if(x.spec.Type==StatusEffectType.Shield)Shield+=x.spec.Magnitude; if(x.spec.Type==StatusEffectType.Slow)slow=Mathf.Max(slow,x.spec.Magnitude); if(x.spec.Type==StatusEffectType.DamageBuff)dmg+=x.spec.Magnitude; if(x.spec.Type==StatusEffectType.MoveSpeedBuff)move+=x.spec.Magnitude;} mover?.SetStatusMoveSpeedMultiplier(1f-Mathf.Clamp01(slow)); mover?.SetStatusMoveSpeedBonus(move); attack?.SetStatusDamageBonus(dmg); if(IsStunned){mover?.Stop();attack?.ClearTarget();abilities?.CancelBeforeRelease();} Changed?.Invoke(this); }
    }
}
