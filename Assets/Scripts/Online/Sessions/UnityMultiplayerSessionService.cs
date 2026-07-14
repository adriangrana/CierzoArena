using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CierzoArena.Core;
using CierzoArena.Online.Identity;
using CierzoArena.Online.Room;
using CierzoArena.Units;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CierzoArena.Online.Sessions
{
    /// <summary>
    /// Sole adapter between CierzoArena and Unity Multiplayer Services. Sessions
    /// own membership, join codes and room data; Relay allocations are captured by
    /// a deferred handler and later applied to NGO by the arena bootstrap.
    /// </summary>
    public sealed class UnityMultiplayerSessionService : IOnlineSessionService
    {
        private const string BuildKey = "build";
        private const string ProtocolKey = "protocol";
        private const string MapKey = "map";
        private const string PhaseKey = "phase";
        private const string DisplayNameKey = "name";
        private const string TeamKey = "team";
        private const string SlotKey = "slot";
        private const string ReadyKey = "ready";
        private const string HeroKey = "hero";
        private const string HeroIntentKey = "hero_intent";
        private const string HeroPickStateKey = "hero_pick_state";
        private const string HeroPickOrderKey = "hero_pick_order";
        private const string HeroTurnKey = "hero_turn";
        private const string HeroDeadlineKey = "hero_deadline";
        private const string HeroTurnSecondsKey = "hero_turn_seconds";
        private const string HeroPicksKey = "hero_picks";
        private const string ChatHistoryKey = "room_chat";
        // Sessions accept at most ten player properties. Keep the build and
        // protocol together so the room can also persist the chosen slot and
        // its chat history without exceeding that service limit.
        private const string PlayerVersionKey = "version";

        private readonly UnityServicesBootstrap services;
        private readonly OnlineServicesSettings settings;
        private readonly MatchRoster roster = new();
        private readonly DeferredRelayNetworkHandler relayHandler = new();
        private ISession session;
        private IHostSession hostSession;

        public bool IsOnlineAvailable => services != null && services.IsCloudProjectLinked && services.Identity != null && services.Identity.IsOnline;
        public bool IsInSession => session != null;
        // The session SDK is the authority for the local role. The roster is a
        // replicated display projection and can briefly be stale during a phase
        // transition, so UI permissions must not be inferred from it.
        public bool IsLocalHost => session != null && session.IsHost;
        // Session membership IDs are the authority for roster ownership. They can
        // differ from the Authentication ID in local multi-instance development.
        public string LocalPlayerId => session?.CurrentPlayer?.Id ?? services.Identity?.PlayerId ?? string.Empty;
        public string JoinCode => session?.Code ?? string.Empty;
        public MatchRoster Roster => roster;
        public HeroSelectionSnapshot HeroSelection { get; private set; } = new();
        public event Action Changed;

        public UnityMultiplayerSessionService(UnityServicesBootstrap bootstrap, OnlineServicesSettings onlineSettings)
        {
            services = bootstrap;
            settings = onlineSettings;
            roster.Changed += RaiseChanged;
        }

        public async Task<OnlineErrorCode> CreatePrivateSessionAsync(MatchPlayerSlot host, CancellationToken cancellationToken)
        {
            if (!IsOnlineAvailable) return OnlineErrorCode.ConfigurationRequired;
            if (host == null || string.IsNullOrWhiteSpace(host.PlayerId)) return OnlineErrorCode.AuthenticationFailed;
            try
            {
                ThrowIfCancelled(cancellationToken);
                SessionOptions options = new SessionOptions
                {
                    Name = "CierzoArena Private Room",
                    MaxPlayers = settings.MaxPlayers,
                    IsPrivate = true,
                    IsLocked = false,
                    PlayerProperties = PlayerProperties(host),
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { BuildKey, new SessionProperty(settings.BuildVersion) },
                        { ProtocolKey, new SessionProperty(settings.ProtocolVersion.ToString()) },
                        { MapKey, new SessionProperty("MobaGreyboxArena") },
                        { PhaseKey, new SessionProperty("room") }
                    }
                };
                options.WithRelayNetwork().WithNetworkHandler(relayHandler);
                hostSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                session = hostSession;
                SubscribeSession();
                RefreshRoster();
                return OnlineErrorCode.None;
            }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception)
            {
                Debug.LogWarning($"[Online] No se pudo crear la sala: {exception.Error} — {exception.Message}");
                return MapSessionError(exception);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return OnlineErrorCode.RelayUnavailable;
            }
        }

        public async Task<OnlineErrorCode> JoinByCodeAsync(string joinCode, MatchPlayerSlot player, CancellationToken cancellationToken)
        {
            if (!IsOnlineAvailable) return OnlineErrorCode.ConfigurationRequired;
            if (string.IsNullOrWhiteSpace(joinCode) || player == null) return OnlineErrorCode.CodeInvalid;
            try
            {
                ThrowIfCancelled(cancellationToken);
                JoinSessionOptions options = new JoinSessionOptions { PlayerProperties = PlayerProperties(player) };
                options.WithNetworkHandler(relayHandler);
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode, options);
                hostSession = session.IsHost ? session.AsHost() : null;
                if (!IsCompatible(session)) { await session.LeaveAsync(); ClearSession(); return OnlineErrorCode.VersionMismatch; }
                SubscribeSession();
                RefreshRoster();
                return OnlineErrorCode.None;
            }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception) { return MapSessionError(exception); }
            catch { return OnlineErrorCode.CodeInvalid; }
        }

        public async Task<OnlineErrorCode> LeaveAsync(string playerId, CancellationToken cancellationToken)
        {
            if (session == null) return OnlineErrorCode.None;
            try { ThrowIfCancelled(cancellationToken); await session.LeaveAsync(); ClearSession(); return OnlineErrorCode.None; }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception) { return MapSessionError(exception); }
            catch { ClearSession(); return OnlineErrorCode.Unknown; }
        }

        public async Task<OnlineErrorCode> CloseAsync(CancellationToken cancellationToken)
        {
            if (session == null) return OnlineErrorCode.None;
            try
            {
                ThrowIfCancelled(cancellationToken);
                if (hostSession != null) await hostSession.DeleteAsync(); else await session.LeaveAsync();
                ClearSession();
                return OnlineErrorCode.None;
            }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception) { return MapSessionError(exception); }
        }

        public IReadOnlyList<MatchPlayerSlot> GetPlayers() => roster.Players;
        public void ObserveSessionChanges() => RefreshRoster();

        public async Task<OnlineErrorCode> SetPlayerDataAsync(MatchPlayerSlot player, CancellationToken cancellationToken)
        {
            if (session == null || player == null || !string.Equals(player.PlayerId, LocalPlayerId, StringComparison.Ordinal)) return OnlineErrorCode.PermissionDenied;
            try
            {
                ThrowIfCancelled(cancellationToken);
                session.CurrentPlayer.SetProperties(PlayerProperties(player));
                await session.SaveCurrentPlayerDataAsync();
                RefreshRoster();
                return OnlineErrorCode.None;
            }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception) { return MapSessionError(exception); }
        }

        public async Task<OnlineErrorCode> SetSessionDataAsync(string key, string value, CancellationToken cancellationToken)
        {
            if (hostSession == null) return OnlineErrorCode.PermissionDenied;
            try
            {
                ThrowIfCancelled(cancellationToken);
                hostSession.SetProperty(key, new SessionProperty(value));
                await hostSession.SavePropertiesAsync();
                RefreshRoster();
                return OnlineErrorCode.None;
            }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception) { return MapSessionError(exception); }
        }

        public async Task<OnlineErrorCode> SetJoinableAsync(bool joinable, CancellationToken cancellationToken)
        {
            if (hostSession == null) return OnlineErrorCode.PermissionDenied;
            try
            {
                ThrowIfCancelled(cancellationToken);
                hostSession.IsLocked = !joinable;
                hostSession.SetProperty(PhaseKey, new SessionProperty(joinable ? "room" : "hero-select"));
                await hostSession.SavePropertiesAsync();
                RefreshRoster();
                return OnlineErrorCode.None;
            }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception) { return MapSessionError(exception); }
        }

        public async Task<OnlineErrorCode> StartGameAsync(CancellationToken cancellationToken)
        {
            if (hostSession == null) return OnlineErrorCode.PermissionDenied;
            // The first roster currently contains six unique heroes. Do not start a
            // draft the authoritative rules could not complete without duplicates.
            if (roster.Players.Count > HeroCatalog.Shared.Heroes.Count) return OnlineErrorCode.Unknown;
            try
            {
                ThrowIfCancelled(cancellationToken);
                // A newly created/joined player already has empty draft data, and
                // EndGameAsync clears it before a rematch. Avoid one remote write
                // per player here: some UGS versions keep that batch pending while
                // the room lock is being propagated, which stranded the host on
                // "Preparando selección de héroe...".
                hostSession.IsLocked = true;
                hostSession.SetProperty(PhaseKey, new SessionProperty("hero-select"));
                hostSession.SetProperty(HeroTurnKey, new SessionProperty("0"));
                hostSession.SetProperty(HeroTurnSecondsKey, new SessionProperty(settings.HeroSelectionTurnSeconds.ToString()));
                hostSession.SetProperty(HeroDeadlineKey, new SessionProperty((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + settings.HeroSelectionTurnSeconds * 1000L).ToString()));
                hostSession.SetProperty(HeroPicksKey, new SessionProperty(string.Empty));
                await hostSession.SavePropertiesAsync();
                RefreshRoster();
                return OnlineErrorCode.None;
            }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception) { return MapSessionError(exception); }
            catch { return OnlineErrorCode.Unknown; }
        }

        public async Task<OnlineErrorCode> SubmitHeroSelectionIntentAsync(string heroId, CancellationToken cancellationToken)
        {
            if (session == null || !HeroSelection.IsActive || !HeroCatalog.Shared.TryGet(heroId, out _)) return OnlineErrorCode.MatchStarted;
            MatchPlayerSlot local = roster.Find(LocalPlayerId);
            if (local == null || local.HeroPickState == HeroPickState.Locked || local.HeroPickState == HeroPickState.AutoPicked) return OnlineErrorCode.MatchStarted;
            List<MatchPlayerSlot> order = PickOrder();
            if (HeroSelection.TurnIndex < 0 || HeroSelection.TurnIndex >= order.Count || order[HeroSelection.TurnIndex].PlayerId != local.PlayerId) return OnlineErrorCode.PermissionDenied;
            if (!IsAvailableHero(heroId)) return OnlineErrorCode.MatchStarted;
            try
            {
                ThrowIfCancelled(cancellationToken);
                session.CurrentPlayer.SetProperty(HeroIntentKey, new PlayerProperty(heroId));
                session.CurrentPlayer.SetProperty(HeroPickStateKey, new PlayerProperty(((int)HeroPickState.Intent).ToString()));
                await session.SaveCurrentPlayerDataAsync();
                RefreshRoster();
                return OnlineErrorCode.None;
            }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception) { return MapSessionError(exception); }
        }

        /// <summary>Runs only on the session host. Clients can submit an intent but
        /// never lock a hero or advance the draft themselves.</summary>
        public async Task<OnlineErrorCode> UpdateHeroSelectionAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (hostSession == null || !HeroSelection.IsActive) return OnlineErrorCode.None;
            try
            {
                ThrowIfCancelled(cancellationToken);
                List<MatchPlayerSlot> order = PickOrder();
                int turn = HeroSelection.TurnIndex;
                if (turn < 0 || turn >= order.Count) return await MoveToLoadingMatchAsync(cancellationToken);
                MatchPlayerSlot picker = order[turn];
                string selected = IsAvailableHero(picker.HeroIntentId) ? picker.HeroIntentId : string.Empty;
                bool automatic = false;
                if (string.IsNullOrWhiteSpace(selected) && HeroSelection.IsDeadlineReached(now))
                {
                    // HeroId before a lock is merely the persisted favourite/last
                    // hero. It becomes final only when this host code commits it.
                    selected = IsAvailableHero(picker.HeroId) ? picker.HeroId : FirstAvailableHero();
                    automatic = true;
                }
                if (string.IsNullOrWhiteSpace(selected)) return OnlineErrorCode.None;
                // Lock results in one session property, not in one player write.
                // Some session SDK versions do not complete host writes to remote
                // player data during a locked room, leaving the draft visually and
                // logically incomplete even after its timer elapsed.
                Dictionary<string, CommittedHeroPick> picks = ReadCommittedPicks();
                picks[picker.PlayerId] = new CommittedHeroPick(selected, automatic ? HeroPickState.AutoPicked : HeroPickState.Locked, turn);
                if (turn + 1 >= order.Count)
                {
                    hostSession.SetProperty(PhaseKey, new SessionProperty("loading-match"));
                    hostSession.SetProperty(HeroTurnKey, new SessionProperty("-1"));
                }
                else
                {
                    hostSession.SetProperty(HeroTurnKey, new SessionProperty((turn + 1).ToString()));
                    hostSession.SetProperty(HeroDeadlineKey, new SessionProperty((now.ToUnixTimeMilliseconds() + settings.HeroSelectionTurnSeconds * 1000L).ToString()));
                }
                hostSession.SetProperty(HeroPicksKey, new SessionProperty(SerializeCommittedPicks(picks)));
                await hostSession.SavePropertiesAsync();
                RefreshRoster();
                return OnlineErrorCode.None;
            }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception) { return MapSessionError(exception); }
            catch (Exception exception)
            {
                Debug.LogWarning("[HeroSelect] Host could not advance draft: " + exception.GetType().Name);
                return OnlineErrorCode.Unknown;
            }
        }

        public async Task<OnlineErrorCode> EndGameAsync(CancellationToken cancellationToken)
        {
            if (hostSession == null) return OnlineErrorCode.PermissionDenied;
            try
            {
                ThrowIfCancelled(cancellationToken);
                foreach (IPlayer player in hostSession.Players)
                {
                    player.SetProperty(ReadyKey, new PlayerProperty("0"));
                    player.SetProperty(HeroIntentKey, new PlayerProperty(string.Empty));
                    player.SetProperty(HeroPickStateKey, new PlayerProperty(((int)HeroPickState.None).ToString()));
                    player.SetProperty(HeroPickOrderKey, new PlayerProperty("-1"));
                    await hostSession.SavePlayerDataAsync(player.Id);
                }
                return await SetJoinableAsync(true, cancellationToken);
            }
            catch (OperationCanceledException) { return OnlineErrorCode.Timeout; }
            catch (SessionException exception) { return MapSessionError(exception); }
        }

        public Task<OnlineErrorCode> JoinMatchmadeSessionAsync(CancellationToken cancellationToken) => Task.FromResult(OnlineErrorCode.Unknown);
        public bool TryConsumeRelayConfiguration(out NetworkConfiguration configuration) => relayHandler.TryConsume(out configuration);

        private void SubscribeSession()
        {
            if (session == null) return;
            session.Changed += RefreshRoster;
            session.PlayerJoined += OnPlayerJoined;
            session.PlayerHasLeft += OnPlayerHasLeft;
            session.RemovedFromSession += OnRemoved;
            session.Deleted += OnRemoved;
        }

        private void UnsubscribeSession()
        {
            if (session == null) return;
            session.Changed -= RefreshRoster;
            session.PlayerJoined -= OnPlayerJoined;
            session.PlayerHasLeft -= OnPlayerHasLeft;
            session.RemovedFromSession -= OnRemoved;
            session.Deleted -= OnRemoved;
        }

        private async void OnPlayerJoined(string playerId)
        {
            RefreshRoster();
            if (hostSession == null) return;
            MatchPlayerSlot joined = roster.Find(playerId);
            if (joined == null) return;
            int teamCount = roster.Players.Count(player => player.Team == joined.Team);
            if (teamCount <= settings.MaxPlayersPerTeam) return;
            try { await hostSession.RemovePlayerAsync(playerId); } catch { /* Session event will reconcile on next refresh. */ }
        }

        private void OnPlayerHasLeft(string _) => RefreshRoster();

        private void OnRemoved() { ClearSession(); }

        private void RefreshRoster()
        {
            if (session == null) return;
            List<MatchPlayerSlot> slots = new List<MatchPlayerSlot>();
            Dictionary<string, CommittedHeroPick> committedPicks = ReadCommittedPicks();
            HashSet<int> azureSlots = new HashSet<int>();
            HashSet<int> emberSlots = new HashSet<int>();
            int index = 0;
            foreach (IReadOnlyPlayer member in session.Players.OrderBy(member => member.Joined))
            {
                TeamId team = ReadTeam(member, TeamId.Azure);
                HashSet<int> occupiedSlots = team == TeamId.Ember ? emberSlots : azureSlots;
                int persistedSlot = int.TryParse(Read(member, SlotKey, "-1"), out int parsedSlot) ? parsedSlot : -1;
                int stableSlot = persistedSlot >= 0 && persistedSlot < settings.MaxPlayersPerTeam && !occupiedSlots.Contains(persistedSlot)
                    ? persistedSlot
                    : FirstFreeSlot(occupiedSlots, settings.MaxPlayersPerTeam);
                occupiedSlots.Add(stableSlot);
                string heroId = Read(member, HeroKey, "storm_warden");
                HeroPickState pickState = ReadPickState(member);
                int pickOrder = int.TryParse(Read(member, HeroPickOrderKey, "-1"), out int parsedPickOrder) ? parsedPickOrder : -1;
                if (committedPicks.TryGetValue(member.Id, out CommittedHeroPick committed))
                {
                    heroId = committed.HeroId;
                    pickState = committed.State;
                    pickOrder = committed.Order;
                }
                (string playerBuildVersion, int playerProtocolVersion) = ReadPlayerVersion(member);
                slots.Add(new MatchPlayerSlot
                {
                    PlayerId = member.Id,
                    DisplayName = Read(member, DisplayNameKey, "Jugador"),
                    Team = team,
                    StableSlot = stableSlot,
                    IsConnected = true,
                    IsReady = string.Equals(Read(member, ReadyKey, "0"), "1", StringComparison.Ordinal),
                    IsHost = string.Equals(member.Id, session.Host, StringComparison.Ordinal),
                    HeroId = heroId,
                    HeroIntentId = Read(member, HeroIntentKey, string.Empty),
                    HeroPickState = pickState,
                    HeroPickOrder = pickOrder,
                    JoinOrder = index++,
                    BuildVersion = playerBuildVersion,
                    ProtocolVersion = playerProtocolVersion,
                    ChatHistory = Read(member, ChatHistoryKey, string.Empty)
                });
            }
            bool joinable = !session.IsLocked && string.Equals(Read(session.Properties, PhaseKey, "room"), "room", StringComparison.Ordinal);
            HeroSelection = new HeroSelectionSnapshot
            {
                Phase = ReadPhase(Read(session.Properties, PhaseKey, "room")),
                TurnIndex = int.TryParse(Read(session.Properties, HeroTurnKey, "-1"), out int turn) ? turn : -1,
                TurnDeadlineUnixMilliseconds = long.TryParse(Read(session.Properties, HeroDeadlineKey, "0"), out long deadline) ? deadline : 0,
                TurnDurationSeconds = int.TryParse(Read(session.Properties, HeroTurnSecondsKey, settings.HeroSelectionTurnSeconds.ToString()), out int seconds) ? seconds : settings.HeroSelectionTurnSeconds
            };
            roster.Replace(slots, session.Host, joinable);
        }

        private bool IsCompatible(ISession candidate) => string.Equals(Read(candidate.Properties, BuildKey, settings.BuildVersion), settings.BuildVersion, StringComparison.Ordinal) && int.TryParse(Read(candidate.Properties, ProtocolKey, "-1"), out int protocol) && protocol == settings.ProtocolVersion;
        private void ClearSession()
        {
            UnsubscribeSession();
            session = null;
            hostSession = null;
            roster.Replace(Array.Empty<MatchPlayerSlot>(), string.Empty, true);
            HeroSelection = new HeroSelectionSnapshot();
            RaiseChanged();
        }
        private void RaiseChanged() => Changed?.Invoke();
        private static Dictionary<string, PlayerProperty> PlayerProperties(MatchPlayerSlot player) => new Dictionary<string, PlayerProperty>
        {
            { DisplayNameKey, new PlayerProperty(NormalizeDisplayName(player.DisplayName)) },
            { TeamKey, new PlayerProperty(((int)player.Team).ToString()) },
            { SlotKey, new PlayerProperty(player.StableSlot.ToString()) },
            { ReadyKey, new PlayerProperty(player.IsReady ? "1" : "0") },
            { HeroKey, new PlayerProperty(player.HeroId ?? "storm_warden") },
            { HeroIntentKey, new PlayerProperty(player.HeroIntentId ?? string.Empty) },
            { HeroPickStateKey, new PlayerProperty(((int)player.HeroPickState).ToString()) },
            { HeroPickOrderKey, new PlayerProperty(player.HeroPickOrder.ToString()) },
            { ChatHistoryKey, new PlayerProperty(player.ChatHistory ?? string.Empty) },
            { PlayerVersionKey, new PlayerProperty(SerializePlayerVersion(player.BuildVersion, player.ProtocolVersion)) }
        };
        private (string BuildVersion, int ProtocolVersion) ReadPlayerVersion(IReadOnlyPlayer player)
        {
            // Read the old two-property shape as a fallback so any room that
            // was created before this change remains understandable.
            string buildVersion = Read(player, BuildKey, settings.BuildVersion);
            int protocolVersion = int.TryParse(Read(player, ProtocolKey, settings.ProtocolVersion.ToString()), out int legacyProtocol)
                ? legacyProtocol
                : -1;
            string packedVersion = Read(player, PlayerVersionKey, string.Empty);
            int separator = packedVersion.LastIndexOf('|');
            if (separator <= 0 || separator >= packedVersion.Length - 1) return (buildVersion, protocolVersion);
            if (!int.TryParse(packedVersion.Substring(separator + 1), out int parsedProtocol)) return (buildVersion, protocolVersion);
            return (packedVersion.Substring(0, separator), parsedProtocol);
        }
        private static string SerializePlayerVersion(string buildVersion, int protocolVersion) => $"{buildVersion ?? string.Empty}|{protocolVersion}";
        private static string Read(IReadOnlyDictionary<string, PlayerProperty> values, string key, string fallback) => values != null && values.TryGetValue(key, out PlayerProperty value) && !string.IsNullOrWhiteSpace(value?.Value) ? value.Value : fallback;
        private static int FirstFreeSlot(HashSet<int> occupied, int capacity)
        {
            for (int slot = 0; slot < capacity; slot++) if (!occupied.Contains(slot)) return slot;
            return 0;
        }
        private static string Read(IReadOnlyDictionary<string, SessionProperty> values, string key, string fallback) => values != null && values.TryGetValue(key, out SessionProperty value) && !string.IsNullOrWhiteSpace(value?.Value) ? value.Value : fallback;
        private static string Read(IReadOnlyPlayer player, string key, string fallback) => Read(player?.Properties, key, fallback);
        private static TeamId ReadTeam(IReadOnlyPlayer player, TeamId fallback) => int.TryParse(Read(player, TeamKey, ((int)fallback).ToString()), out int value) && value == (int)TeamId.Ember ? TeamId.Ember : TeamId.Azure;
        private static HeroPickState ReadPickState(IReadOnlyPlayer player) => int.TryParse(Read(player, HeroPickStateKey, "0"), out int value) && Enum.IsDefined(typeof(HeroPickState), value) ? (HeroPickState)value : HeroPickState.None;
        private static SessionPhase ReadPhase(string value) => value switch { "hero-select" => SessionPhase.HeroSelection, "loading-match" => SessionPhase.LoadingMatch, "in-match" => SessionPhase.InMatch, "match-ended" => SessionPhase.MatchEnded, _ => SessionPhase.Lobby };
        private static string NormalizeDisplayName(string value) => PlayerDisplayName.TryNormalize(value, out string normalized, out _) ? normalized : "Jugador";
        private static void ThrowIfCancelled(CancellationToken token) { if (token.IsCancellationRequested) throw new OperationCanceledException(token); }
        private static OnlineErrorCode MapSessionError(SessionException exception)
        {
            string code = exception.Error.ToString();
            if (code.IndexOf("Full", StringComparison.OrdinalIgnoreCase) >= 0) return OnlineErrorCode.SessionFull;
            if (code.IndexOf("Code", StringComparison.OrdinalIgnoreCase) >= 0 || code.IndexOf("NotFound", StringComparison.OrdinalIgnoreCase) >= 0) return OnlineErrorCode.CodeInvalid;
            if (code.IndexOf("Locked", StringComparison.OrdinalIgnoreCase) >= 0) return OnlineErrorCode.SessionClosed;
            if (code.IndexOf("Network", StringComparison.OrdinalIgnoreCase) >= 0 || code.IndexOf("Relay", StringComparison.OrdinalIgnoreCase) >= 0) return OnlineErrorCode.RelayUnavailable;
            if (code.IndexOf("Authorization", StringComparison.OrdinalIgnoreCase) >= 0 || code.IndexOf("Auth", StringComparison.OrdinalIgnoreCase) >= 0) return OnlineErrorCode.AuthenticationFailed;
            return OnlineErrorCode.Unknown;
        }

        private async Task<OnlineErrorCode> MoveToLoadingMatchAsync(CancellationToken cancellationToken)
        {
            hostSession.SetProperty(PhaseKey, new SessionProperty("loading-match"));
            hostSession.SetProperty(HeroTurnKey, new SessionProperty("-1"));
            await hostSession.SavePropertiesAsync();
            RefreshRoster();
            return OnlineErrorCode.None;
        }

        private List<MatchPlayerSlot> PickOrder()
        {
            List<MatchPlayerSlot> azure = roster.Players.Where(player => player.Team == TeamId.Azure).OrderBy(player => player.StableSlot).ToList();
            List<MatchPlayerSlot> ember = roster.Players.Where(player => player.Team == TeamId.Ember).OrderBy(player => player.StableSlot).ToList();
            List<MatchPlayerSlot> result = new List<MatchPlayerSlot>(azure.Count + ember.Count);
            for (int index = 0; index < Math.Max(azure.Count, ember.Count); index++)
            {
                if (index < azure.Count) result.Add(azure[index]);
                if (index < ember.Count) result.Add(ember[index]);
            }
            return result;
        }

        private bool IsAvailableHero(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId) || !HeroCatalog.Shared.TryGet(heroId, out _)) return false;
            return !roster.Players.Any(player => (player.HeroPickState == HeroPickState.Locked || player.HeroPickState == HeroPickState.AutoPicked) && string.Equals(player.HeroId, heroId, StringComparison.Ordinal));
        }

        private string FirstAvailableHero()
        {
            foreach (HeroDefinition hero in HeroCatalog.Shared.Heroes)
                if (hero != null && IsAvailableHero(hero.HeroId)) return hero.HeroId;
            return string.Empty;
        }

        private Dictionary<string, CommittedHeroPick> ReadCommittedPicks()
        {
            Dictionary<string, CommittedHeroPick> picks = new Dictionary<string, CommittedHeroPick>(StringComparer.Ordinal);
            string raw = session == null ? string.Empty : Read(session.Properties, HeroPicksKey, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return picks;
            foreach (string entry in raw.Split(';'))
            {
                string[] values = entry.Split(',');
                if (values.Length != 4 || string.IsNullOrWhiteSpace(values[0]) || !int.TryParse(values[2], out int rawState) || !int.TryParse(values[3], out int order)) continue;
                HeroPickState state = Enum.IsDefined(typeof(HeroPickState), rawState) ? (HeroPickState)rawState : HeroPickState.None;
                if ((state != HeroPickState.Locked && state != HeroPickState.AutoPicked) || !HeroCatalog.Shared.TryGet(values[1], out _)) continue;
                picks[values[0]] = new CommittedHeroPick(values[1], state, order);
            }
            return picks;
        }

        private static string SerializeCommittedPicks(Dictionary<string, CommittedHeroPick> picks)
        {
            return string.Join(";", picks.OrderBy(pair => pair.Value.Order).Select(pair => pair.Key + "," + pair.Value.HeroId + "," + (int)pair.Value.State + "," + pair.Value.Order));
        }

        private readonly struct CommittedHeroPick
        {
            public readonly string HeroId;
            public readonly HeroPickState State;
            public readonly int Order;
            public CommittedHeroPick(string heroId, HeroPickState state, int order) { HeroId = heroId; State = state; Order = order; }
        }
}
}
