using CierzoArena.Units;
using Unity.Netcode;
using UnityEngine;

namespace CierzoArena.Netcode
{
    [RequireComponent(typeof(HeroInventory))]
    public sealed class NetworkHeroInventory : NetworkBehaviour, IHeroInventoryRequestGateway
    {
        [SerializeField] private ItemCatalog catalog;
        private readonly NetworkList<int> itemIds = new(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private HeroInventory inventory;
        public bool IsReady => IsSpawned && IsOwner;
        public void RequestBuy(int itemIdHash) => RequestBuyRpc(itemIdHash);
        public void RequestSell(int slot) => RequestSellRpc(slot);
        public void RequestSwap(int sourceSlot, int destinationSlot) => RequestSwapRpc(sourceSlot, destinationSlot);
        private void Awake()
        {
            inventory = GetComponent<HeroInventory>();
            inventory.SetAuthorityEnabled(false);
        }
        public override void OnNetworkSpawn()
        {
            catalog ??= ItemCatalog.Active;
            inventory.SetAuthorityEnabled(IsServer);
            if (IsServer) { while(itemIds.Count < inventory.Capacity) itemIds.Add(0); inventory.Changed += Publish; Publish(inventory); }
            else { itemIds.OnListChanged += OnItemsChanged; Apply(); }
        }
        public override void OnNetworkDespawn() { if(IsServer) inventory.Changed -= Publish; else itemIds.OnListChanged -= OnItemsChanged; }
        [Rpc(SendTo.Server)] public void RequestBuyRpc(int itemIdHash, RpcParams rpcParams=default) { if(rpcParams.Receive.SenderClientId==OwnerClientId) inventory.TryBuyByHash(itemIdHash, FindZone()); }
        [Rpc(SendTo.Server)] public void RequestSellRpc(int slot, RpcParams rpcParams=default) { if(rpcParams.Receive.SenderClientId==OwnerClientId) inventory.TrySell(slot, FindZone()); }
        [Rpc(SendTo.Server)] public void RequestSwapRpc(int sourceSlot, int destinationSlot, RpcParams rpcParams=default) { if(rpcParams.Receive.SenderClientId==OwnerClientId) inventory.TrySwap(sourceSlot, destinationSlot); }
        private ShopZone FindZone()
        {
            return TryGetComponent(out CierzoArena.Core.TeamMember team)
                ? ShopZone.FindFriendlyContaining(team.Team, transform.position)
                : null;
        }
        private void Publish(HeroInventory _) { for(int i=0;i<itemIds.Count;i++) itemIds[i]=inventory.GetItemHash(i); }
        private void Apply()
        {
            if (catalog == null)
            {
                return;
            }

            var ids = new int[itemIds.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = itemIds[i];
            if (inventory.ApplyAuthoritativeHashes(catalog, ids) && TryGetComponent(out NetworkUnitController unit))
            {
                unit.ReapplyReplicatedHealth();
            }
        }
        private void OnItemsChanged(NetworkListEvent<int> _) => Apply();
    }
}
