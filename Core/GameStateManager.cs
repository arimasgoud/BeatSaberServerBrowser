﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ServerBrowser.Game;
using ServerBrowser.Game.Models;
using ServerBrowser.UI.Components;
using ServerBrowser.Utils;
using static MultiplayerLobbyConnectionController;

namespace ServerBrowser.Core
{
    public static class GameStateManager
    {
        /// <summary>
        /// A summary of the current multiplayer game state.
        /// </summary>
        public static MultiplayerActivity Activity { get; private set; } = new();
        
        /// <summary>
        /// UI status text for the game state manager, as shown in the lobby panel.
        /// </summary>
        public static string StatusText { get; private set; } = "Hello world";
        
        /// <summary>
        /// Indicates whether the game state manager errored, meaning no successful announce was made.
        /// </summary>
        public static bool HasErrored { get; private set; } = true;

        private static bool _wasAnnounced = false;

        #region Lifecycle
        public static void SetUp()
        {
            MpEvents.OnlineMenuOpened += OnOnlineMenuOpened;
            MpEvents.OnlineMenuClosed += OnOnlineMenuClosed;
            MpEvents.MasterServerChanged += OnMasterServerChanged;
            MpEvents.ConnectionTypeChanged += OnConnectionTypeChanged;
            MpEvents.ServerCodeChanged += OnServerCodeChanged;
            MpEvents.LobbyStateChanged += OnLobbyStateChanged;
            MpEvents.BeforeConnectToServer += OnBeforeConnectToServer;
            MpEvents.SessionConnected += OnSessionConnected;
            MpEvents.SessionDisconnected += OnSessionDisconnected;
            MpEvents.PlayerConnected += OnPlayerConnected;
            MpEvents.PlayerDisconnected += OnPlayerDisconnected;
            MpEvents.StartingMultiplayerLevel += OnStartingMultiplayerLevel;
            MpEvents.PartyOwnerChanged += OnPartyOwnerChanged;

            _wasAnnounced = false;
        }

        public static void TearDown()
        {
            MpEvents.OnlineMenuOpened -= OnOnlineMenuOpened;
            MpEvents.OnlineMenuClosed -= OnOnlineMenuClosed;
            MpEvents.MasterServerChanged -= OnMasterServerChanged;
            MpEvents.ConnectionTypeChanged += OnConnectionTypeChanged;
            MpEvents.ServerCodeChanged -= OnServerCodeChanged;
            MpEvents.LobbyStateChanged -= OnLobbyStateChanged;
            MpEvents.BeforeConnectToServer -= OnBeforeConnectToServer;
            MpEvents.SessionConnected -= OnSessionConnected;
            MpEvents.SessionDisconnected -= OnSessionDisconnected;
            MpEvents.PlayerConnected -= OnPlayerConnected;
            MpEvents.PlayerDisconnected -= OnPlayerDisconnected;
            MpEvents.StartingMultiplayerLevel -= OnStartingMultiplayerLevel;
            MpEvents.PartyOwnerChanged -= OnPartyOwnerChanged;

            _wasAnnounced = false;
        }
        #endregion

        #region Events
        private static void OnOnlineMenuOpened(object sender, OnlineMenuOpenedEventArgs e)
        {
            Activity.InOnlineMenu = true;
            
            HandleUpdate();
        }
        private static void OnOnlineMenuClosed(object sender, EventArgs e)
        {
            Activity.InOnlineMenu = false;
            
            HandleUpdate();
        }
        
        private static void OnMasterServerChanged(object sender, MasterServerEndPoint endPoint)
        {
            Activity.MasterServer = endPoint;
            
            HandleUpdate();
        }
        
        private static void OnConnectionTypeChanged(object sender, LobbyConnectionType connectionType)
        {
            if (Activity.ConnectionType == connectionType)
                return;
            
            Activity.ConnectionType = connectionType;

            HandleUpdate();
        }

        private static void OnLobbyStateChanged(object sender, MultiplayerLobbyState lobbyState)
        {
            if (Activity.LobbyState == lobbyState)
                return;
            
            Activity.LobbyState = lobbyState;
            
            if (Activity.LobbyState != MultiplayerLobbyState.None)
                HandleUpdate();
        }

        private static void OnServerCodeChanged(object sender, string serverCode)
        {
            if (serverCode == Activity.ServerCode || String.IsNullOrEmpty(serverCode))
                return;

            Activity.ServerCode = serverCode;

            HandleUpdate();
        }

        private static void OnSessionConnected(object sender, SessionConnectedEventArgs e)
        {
            Activity.SessionStartedAt = DateTime.Now;

            if (e.MaxPlayers > 0)
                Activity.MaxPlayerCount = e.MaxPlayers;
            
            Activity.HostUserId = e.ConnectionOwner.userId;
            
            if (Activity.Players == null) 
                Activity.Players = new List<IConnectedPlayer>(e.MaxPlayers);
            else
                Activity.Players.Clear();

            if (GlobalModState.WeInitiatedConnection && GlobalModState.LastConnectToHostedGame is not null)
            {
                // We have joined the game from the server browser as a client, override certain details
                Activity.BssbGame = GlobalModState.LastConnectToHostedGame;
                Activity.Name = Activity.BssbGame.GameName;
                Activity.MaxPlayerCount = Activity.BssbGame.PlayerLimit;
            }
            else
            {
                // Joined outside of server browser
                Activity.BssbGame = null;
                Activity.Name = MpSession.GetHostGameName();
            }

            OnPlayerConnected(sender, e.LocalPlayer);
            OnPlayerConnected(sender, e.ConnectionOwner);
        }

        private static void OnSessionDisconnected(object sender, DisconnectedReason e)
        {
            Activity.ConnectionType = LobbyConnectionType.None;
            Activity.LobbyState = MultiplayerLobbyState.None;
            Activity.ServerCode = null;
            Activity.HostUserId = null;
            Activity.HostSecret = null;
            Activity.IsDedicatedServer = false;
            Activity.ServerConfiguration = null;
            Activity.MaxPlayerCount = 5;
            Activity.Players?.Clear();
            Activity.CurrentLevel = null;
            Activity.CurrentDifficulty = null;
            Activity.CurrentCharacteristic = null;
            Activity.CurrentModifiers = null;
            Activity.SessionStartedAt = null;
            
            _wasAnnounced = false;
                
            HandleUpdate();
        }

        private static void OnPlayerConnected(object sender, IConnectedPlayer player)
        {
            if (Activity.Players == null || Activity.Players.Any(p => p.userId == player.userId))
                return;
            
            Activity.Players.Add(player);
            
            _wasAnnounced = Activity.IsAnnounced;
                
            HandleUpdate();
        }

        private static void OnPlayerDisconnected(object sender, IConnectedPlayer player)
        {
            if (Activity.Players == null)
                return;
            
            Activity.Players.Remove(player);
                
            HandleUpdate();
        }
        
        private static void OnBeforeConnectToServer(object sender, ConnectToServerEventArgs e)
        {
            Activity.ServerCode = e.Code;
            Activity.HostUserId = e.UserId;
            Activity.HostSecret = e.Secret;
            Activity.Endpoint = e.RemoteEndPoint;
            Activity.IsDedicatedServer = e.IsDedicatedServer;
            Activity.SelectionMask = e.SelectionMask;
            Activity.ServerConfiguration = e.Configuration;
            
            if (e.Configuration.maxPlayerCount > 0)
                Activity.MaxPlayerCount = e.Configuration.maxPlayerCount;
            
            Activity.ManagerId = e.ManagerId;
            
            Activity.Players = new(Activity.MaxPlayerCount);
            
            HandleUpdate();
        }

        private static void OnStartingMultiplayerLevel(object sender, StartingMultiplayerLevelEventArgs e)
        {
            Activity.CurrentLevel = e.BeatmapLevel;
            Activity.CurrentDifficulty = e.Difficulty;
            Activity.CurrentCharacteristic = e.Characteristic;
            Activity.CurrentModifiers = e.Modifiers;

            HandleUpdate();
        }
        
        private static async void OnPartyOwnerChanged(object sender, string userId)
        {
            if (Activity.ManagerId == userId)
                return;
            
            Activity.ManagerId = userId;

            var didTakeOver = false;

            if (Activity.LocalPlayer?.userId == userId && Activity.ConnectionType == LobbyConnectionType.PartyClient)
            {
                // We are transitioning from client to host
                Plugin.Log?.Info($"Local player has been promoted to party leader (_wasAnnounced={_wasAnnounced})");
                Activity.ConnectionType = LobbyConnectionType.PartyHost;

                if (_wasAnnounced)
                {
                    // The game was already announced by previous manager, do auto takeover
                    Plugin.Config.LobbyAnnounceToggle = true;
                    didTakeOver = true;
                }
            }
            
            _wasAnnounced = Activity.IsAnnounced;

            HandleUpdate();

            if (didTakeOver)
            {
                // If we did a take over, re-send announcement after ~1 sec to work around a possible race condition
                //  with the previous host sending an un-announce msg at the same time.
                await Task.Delay(1500);
                HandleUpdate();
            }
        }
        #endregion

        #region Update Action
        public static void HandleUpdate(bool raiseEvent = true)
        {
            if (raiseEvent)
                MpEvents.RaiseActivityUpdated(null, Activity);
            
            if (!CheckCanAnnounce())
            {
                // We are not able or allowed to announce right now
                SetErrorState("Can't announce: you have to be the host, or be in a Quick Play game");
                return;
            }

            if (!CheckToggleSwitch())
            {
                // The toggle switch is off, announce disabled
                SetErrorState("Flip the switch to announce this game");
                return;
            }
            
            // No objections, try announce now
            try
            {
                var lobbyAnnounce = GenerateAnnounce();

                StatusText = "Announcing your game to the world...\r\n" + lobbyAnnounce.Describe();
                HasErrored = false;

                LobbyConfigPanel.UpdatePanelInstance();

                // Send actual
                _ = DoAnnounce(lobbyAnnounce);
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"[GameStateManager] Error in announce: {e}");
                SetErrorState("Error: could not send announce");
            }
        }
        
        /// <summary>
        /// Sets error state, removes any previous announces, and updates the UI.
        /// </summary>
        private static void SetErrorState(string errorText, bool unAnnounce = true)
        {
            StatusText = errorText;
            HasErrored = true;
            
            if (unAnnounce)
                _ = UnAnnounce();
            
            LobbyConfigPanel.UpdatePanelInstance();
        }

        /// <summary>
        /// Checks whether the local player can currently announce to the server browser.
        /// </summary>
        private static bool CheckCanAnnounce()
        {
            if (Activity.ConnectionType is LobbyConnectionType.None or LobbyConnectionType.PartyClient)
                // Not connected / not in a Quick Play game / not a host
                return false;
            
            if (Activity.ServerCode == null || Activity.HostPlayer == null)
                // Need a server code and connection owner at minimum
                return false;

            // No objections
            return true;
        }

        /// <summary>
        /// Checks whether announces are enabled by the local player for the current lobby type.
        /// </summary>
        private static bool CheckToggleSwitch()
        {
            if (Activity.IsQuickPlay)
                return Plugin.Config.ShareQuickPlayGames;
            else
                return Plugin.Config.LobbyAnnounceToggle;
        }
        
        /// <summary>
        /// Generates the announce payload for the master server API.
        /// </summary>
        private static HostedGameData GenerateAnnounce() => new()
        {
            ServerCode = Activity.ServerCode!,
            GameName = MpSession.GetHostGameName(),
            OwnerId = Activity.HostPlayer!.userId,
            OwnerName = Activity.HostPlayer!.userName,
            PlayerCount = Activity.CurrentPlayerCount,
            PlayerLimit = Activity.MaxPlayerCount,
            IsModded = Activity.IsModded,
            LobbyState = Activity.LobbyState,
            LevelId = Activity.CurrentLevel?.levelID,
            SongName = Activity.CurrentLevel?.songName,
            SongAuthor = Activity.CurrentLevel?.songAuthorName,
            Difficulty = Activity.DetermineLobbyDifficulty(),
            Platform = MpLocalPlayer.PlatformId,
            MasterServerHost = Activity.MasterServer.hostName,
            MasterServerPort = Activity.MasterServer.port,
            MpExVersion = ModCheck.MultiplayerExtensions.InstalledVersion,
            ServerType = Activity.DetermineServerType(),
            HostSecret = Activity.HostSecret,
            Endpoint = Activity.Endpoint,
            ManagerId = Activity.ManagerId,
            
            Players = Activity.GetPlayersForAnnounce().ToList()
        };
        #endregion

        #region Announce actions
        private static Dictionary<string, AnnounceState> _announceStates = new();
        
        /// <summary>
        /// Sends a host announcement.
        /// </summary>
        private static async Task<bool> DoAnnounce(HostedGameData announce)
        {
            if (String.IsNullOrEmpty(announce.ServerCode))
                return false;  
            
            // Get or initialize state
            AnnounceState announceState;

            if (!_announceStates.ContainsKey(announce.ServerCode))
            {
                _announceStates.Add(announce.ServerCode, new()
                {
                    ServerCode = announce.ServerCode,
                    OwnerId = announce.OwnerId,
                    HostSecret = announce.HostSecret
                });
            }

            announceState = _announceStates[announce.ServerCode];

            // Try send announce
            var announceResult = await BSSBMasterAPI.Announce(announce);
            
            if (announceResult.Success)
            {
                announceState.DidAnnounce = true;
                announceState.LastSuccess = DateTime.Now;

                StatusText = $"Players can now join from the browser!\r\n{announce.Describe()}";
                HasErrored = false;

                announce.Key = announceResult.Key;
                Activity.BssbGame = announce;
                
                MpEvents.RaiseActivityUpdated(Activity, Activity);
            }
            else
            {
                announceState.DidFail = true;
                announceState.LastFailure = DateTime.Now;

                StatusText = $"Failed to announce to Server Browser!";
                HasErrored = true;
            }

            LobbyConfigPanel.UpdatePanelInstance();
            
            return announceResult.Success;
        }

        /// <summary>
        /// Ensures that any host announcements made by us previously are removed.
        /// </summary>
        public static async Task UnAnnounce()
        {
            foreach (var state in _announceStates.Values.ToArray())
            {
                if (await BSSBMasterAPI.UnAnnounce(state))
                {
                    _announceStates.Remove(state.ServerCode);
                }
            }
        }
        #endregion
    }
}
