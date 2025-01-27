using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using ServerBrowser.Core;
using ServerBrowser.Utils;
using static MultiplayerLobbyConnectionController;

namespace ServerBrowser.Game.Models
{
    /// <summary>
    /// The executive summary of the current local multiplayer state.
    /// </summary>
    public sealed class MultiplayerActivity
    {
        #region Fields
        public bool InOnlineMenu;
        public string Name;
        public MasterServerEndPoint MasterServer;
        public LobbyConnectionType ConnectionType;
        public MultiplayerLobbyState LobbyState;
        public string? ServerCode;
        public string? HostUserId;
        public string? HostSecret;
        public IPEndPoint? Endpoint;
        public bool IsDedicatedServer;
        public BeatmapLevelSelectionMask? SelectionMask;
        public GameplayServerConfiguration? ServerConfiguration;
        public int MaxPlayerCount;
        public List<IConnectedPlayer>? Players;
        public IPreviewBeatmapLevel? CurrentLevel;
        public BeatmapDifficulty? CurrentDifficulty;
        public BeatmapCharacteristicSO? CurrentCharacteristic;
        public GameplayModifiers? CurrentModifiers;
        public DateTime? SessionStartedAt;
        public string? ManagerId;
        public HostedGameData? BssbGame;
        #endregion

        #region Getters
        public int CurrentPlayerCount => Players?.Count(p => p.sortIndex >= 0 && !p.isKicked) ?? 1;
        
        public bool IsInMultiplayer => ConnectionType != LobbyConnectionType.None &&
                                       LobbyState != MultiplayerLobbyState.None &&
                                       LobbyState != MultiplayerLobbyState.Error;

        public bool IsInGameplay => LobbyState == MultiplayerLobbyState.GameRunning;

        public bool IsHost => ConnectionType == LobbyConnectionType.PartyHost;

        public bool IsQuickPlay => ConnectionType == LobbyConnectionType.QuickPlay;

        public string CurrentDifficultyName => CurrentDifficulty?.ToNiceName() ?? "Unknown";

        public string DifficultyMaskName => SelectionMask.HasValue
            ? SelectionMask.Value.difficulties.FromMask().ToNiceName()
            : "All";
        
        public bool IsBeatTogether => IsDedicatedServer && MasterServer.hostName.Contains("beattogether.systems");
        public bool IsBeatDedi => HostPlayer?.userName.StartsWith("BeatDedi/") ?? false;

        public bool IsModded
        {
            get
            {
                if (HostPlayer is not null)
                    if (HostPlayer.HasState("modded") || HostPlayer.HasState("customsongs"))
                        return true;
                if (PartyLeaderPlayer is not null)
                    if (PartyLeaderPlayer.HasState("modded") || PartyLeaderPlayer.HasState("customsongs"))
                        return true;
                return false;
            }
        }

        public bool IsAnnounced
        {
            get
            {
                if (HostPlayer is not null)
                    if (HostPlayer.HasState("lobbyannounce"))
                        return true;
                if (PartyLeaderPlayer is not null)
                    if (PartyLeaderPlayer.HasState("lobbyannounce"))
                        return true;
                return false;
            }
        }

        public IConnectedPlayer? HostPlayer => Players?.FirstOrDefault(p => p.isConnectionOwner);

        public IConnectedPlayer? PartyLeaderPlayer => ManagerId is not null && Players is not null
            ? Players.FirstOrDefault(p => p.userId == ManagerId) : null;
        
        public IConnectedPlayer? LocalPlayer => Players?.FirstOrDefault(p => p.isMe);

        public bool WeArePartyLeader => IsHost || (LocalPlayer is not null && LocalPlayer.userId == ManagerId);
        #endregion

        #region Announce helpers
        public string DetermineServerType()
        {
            // Special: BeatTogether
            if (IsBeatTogether)
                if (IsQuickPlay)
                    return HostedGameData.ServerTypeBeatTogetherQuickplay;
                else
                    return HostedGameData.ServerTypeBeatTogetherDedicated;
            
            // Special: BeatDedi
            if (IsBeatDedi)
                if (IsQuickPlay)
                    return HostedGameData.ServerTypeBeatDediQuickplay;
                else
                    return HostedGameData.ServerTypeBeatDediCustom;
            
            // Official: Dedicated Server
            if (IsDedicatedServer)
                if (IsQuickPlay)
                    return HostedGameData.ServerTypeVanillaQuickplay;
                else
                    return HostedGameData.ServerTypeVanillaDedicated;

            // Fallback: old P2P host? This should never happen
            return HostedGameData.ServerTypePlayerHost;
        }

        public IEnumerable<HostedGamePlayer> GetPlayersForAnnounce()
        {
            if (Players != null)
            {
                foreach (var player in Players)
                {
                    yield return new HostedGamePlayer()
                    {
                        SortIndex = player.sortIndex,
                        UserId = player.userId,
                        UserName = player.userName,
                        IsHost = player.isConnectionOwner,
                        IsAnnouncer = player.isMe,
                        Latency = player.currentLatency
                    };
                }
            }
        }

        public BeatmapDifficulty? DetermineLobbyDifficulty()
        {
            var difficulty = CurrentDifficulty;
            if (difficulty == null && SelectionMask.HasValue)
                difficulty = SelectionMask.Value.difficulties.FromMask();
            return difficulty;
        }
        #endregion
    }
}