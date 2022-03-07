namespace MultiplayerExtensions
{
    public static class MPStateBase
    {
        private static MasterServerInfo _masterServerEndPoint = new MasterServerInfo("localhost", 2328, "");

        private static string? _lastRoomCode;

        private static MultiplayerGameState? _currentGameState = MultiplayerGameState.None;

        private static MultiplayerLobbyState? _currentLobbyState = MultiplayerLobbyState.None;

        private static bool _customSongsEnabled;

        private static bool _easterEggsEnabled = true;

        private static bool _localPlayerIsHost = false;
        /// <summary>
        /// The current known Master Server.
        /// </summary>
        public static MasterServerInfo CurrentMasterServer
        {
            get => _masterServerEndPoint;
            internal set
            {
                if (_masterServerEndPoint == value)
                    return;
                _masterServerEndPoint = value;
                Plugin.Log?.Debug($"Updated MasterServer to '{value}'");
            }
        }
        /// <summary>
        /// The last room code that was set.
        /// </summary>
        public static string? LastRoomCode
        {
            get => _lastRoomCode;
            internal set
            {
                if (_lastRoomCode == value)
                    return;
                _lastRoomCode = value;
                Plugin.Log?.Debug($"Updated room code to '{value}'");
            }
        }
        /// <summary>
        /// The current multiplayer game state.
        /// </summary>
        public static MultiplayerGameState? CurrentGameState
        {
            get => _currentGameState;
            internal set
            {
                if (_currentGameState == value)
                    return;
                _currentGameState = value;
                Plugin.Log?.Debug($"Updated game state to '{value}'");
            }
        }
        /// <summary>
        /// The current multiplayer game state.
        /// </summary>
        public static MultiplayerLobbyState? CurrentLobbyState
        {
            get => _currentLobbyState;
            internal set
            {
                if (_currentLobbyState == value)
                    return;
                _currentLobbyState = value;
                Plugin.Log?.Debug($"Updated game state to '{value}'");
            }
        }
        /// <summary>
        /// Whether custom songs are enabled in the current lobby.
        /// </summary>
        public static bool CustomSongsEnabled
        {
            get => _customSongsEnabled;
            internal set
            {
                if (_customSongsEnabled == value)
                    return;
                _customSongsEnabled = value;
                Plugin.Log?.Debug($"Updated custom songs to '{value}'");
            }
        }
        /// <summary>
        /// Whether easter eggs in multiplayer are enabled.
        /// </summary>
        public static bool EasterEggsEnabled
        {
            get => _easterEggsEnabled;
            internal set
            {
                if (_easterEggsEnabled == value)
                    return;
                _easterEggsEnabled = value;
                Plugin.Log?.Debug($"Easter Eggs {(value ? "enabled" : "disabled")}.");
            }
        }
        /// <summary>
        /// Whether the local player is the lobby host.
        /// </summary>
        public static bool LocalPlayerIsHost
        {
            get => _localPlayerIsHost;
            internal set
            {
                if (_localPlayerIsHost == value)
                    return;
                _localPlayerIsHost = value;
                Plugin.Log?.Debug($"Local player is{(_localPlayerIsHost ? " " : " not ")}host.");
            }
        }
    }
}