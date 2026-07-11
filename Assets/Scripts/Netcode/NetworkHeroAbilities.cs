using CierzoArena.Units;
using CierzoArena.Combat;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    [RequireComponent(typeof(HeroAbilities))]
    [RequireComponent(typeof(HeroMana))]
    public sealed class NetworkHeroAbilities : NetworkBehaviour, IHeroAbilityRequestGateway
    {
        private readonly NetworkVariable<float> manaCurrent = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> manaMaximum = new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> skillPoints = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkList<int> levels = new(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkList<float> cooldowns = new(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private HeroMana mana; private HeroAbilities abilities;
        public bool IsReady => IsSpawned && IsOwner;
        public void RequestUpgrade(int slot) => RequestUpgradeRpc(slot);
        public void RequestCast(int slot, Health target, Vector3 point)
        {
            RequestCastRpc(slot, target != null && target.TryGetComponent(out NetworkObject targetObject) ? new NetworkObjectReference(targetObject) : default, point);
        }

        private void Awake()
        {
            mana = GetComponent<HeroMana>(); abilities = GetComponent<HeroAbilities>();
            mana.SetAuthorityEnabled(false); abilities.SetAuthorityEnabled(false);
        }
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                mana.SetAuthorityEnabled(true); abilities.SetAuthorityEnabled(true);
                while (levels.Count < 4) levels.Add(0);
                while (cooldowns.Count < 4) cooldowns.Add(0f);
                mana.Changed += OnChanged; abilities.Changed += OnAbilitiesChanged; abilities.ProjectileReleased += OnProjectileReleased;
                Publish();
            }
            else
            {
                manaCurrent.OnValueChanged += OnManaChanged; manaMaximum.OnValueChanged += OnManaMaximumChanged; skillPoints.OnValueChanged += OnPointsChanged;
                levels.OnListChanged += OnListChanged; cooldowns.OnListChanged += OnCooldownsChanged; Apply();
            }
        }
        public override void OnNetworkDespawn()
        {
            if (IsServer) { mana.Changed -= OnChanged; abilities.Changed -= OnAbilitiesChanged; abilities.ProjectileReleased -= OnProjectileReleased; }
            else { manaCurrent.OnValueChanged -= OnManaChanged; manaMaximum.OnValueChanged -= OnManaMaximumChanged; skillPoints.OnValueChanged -= OnPointsChanged; levels.OnListChanged -= OnListChanged; cooldowns.OnListChanged -= OnCooldownsChanged; }
        }
        [Rpc(SendTo.Server)] public void RequestUpgradeRpc(int slot, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) abilities.TryUpgrade(slot);
        }
        [Rpc(SendTo.Server)] public void RequestCastRpc(int slot, NetworkObjectReference targetReference, Vector3 point, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            Health target = null;
            if (targetReference.TryGet(out NetworkObject targetObject)) target = targetObject.GetComponent<Health>();
            abilities.TryStartCast(slot, target, point);
        }
        private void Update() { if (IsServer && IsSpawned) Publish(); }
        private void OnChanged(HeroMana _) => Publish();
        private void OnAbilitiesChanged(HeroAbilities _) => Publish();
        private void Publish()
        {
            manaCurrent.Value = mana.CurrentMana; manaMaximum.Value = mana.MaximumMana; skillPoints.Value = abilities.SkillPoints;
            for (int i = 0; i < 4; i++) { levels[i] = abilities.GetLevel(i); cooldowns[i] = abilities.GetCooldown(i); }
        }
        private void Apply()
        {
            mana.ApplyAuthoritativeState(manaCurrent.Value, manaMaximum.Value);
            int[] replicatedLevels = new int[levels.Count]; float[] replicatedCooldowns = new float[cooldowns.Count];
            for (int i = 0; i < levels.Count; i++) replicatedLevels[i] = levels[i];
            for (int i = 0; i < cooldowns.Count; i++) replicatedCooldowns[i] = cooldowns[i];
            abilities.ApplyAuthoritativeState(skillPoints.Value, replicatedLevels, replicatedCooldowns);
        }
        private void OnManaChanged(float _, float __) => Apply();
        private void OnManaMaximumChanged(float _, float __) => Apply();
        private void OnPointsChanged(int _, int __) => Apply();
        private void OnListChanged(NetworkListEvent<int> _) => Apply();
        private void OnCooldownsChanged(NetworkListEvent<float> _) => Apply();
        private void OnProjectileReleased(HeroAbilities _, Vector3 origin, Health target, float speed, float lifetime) => NetworkProjectileSpawner.Active?.SpawnVisual(origin, target, speed, lifetime);
    }
}
