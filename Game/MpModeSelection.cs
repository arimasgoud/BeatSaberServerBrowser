﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HMUI;
using IPA.Utilities;
using ServerBrowser.Core;
using ServerBrowser.Harmony;
using ServerBrowser.UI;
using UnityEngine;
using static HMUI.ViewController;

namespace ServerBrowser.Game
{
    public static class MpModeSelection
    {
        #region Init
        private static MultiplayerModeSelectionFlowCoordinator _flowCoordinator;
        private static MultiplayerLobbyConnectionController _mpLobbyConnectionController;
        private static JoiningLobbyViewController _joiningLobbyViewController;
        private static SimpleDialogPromptViewController _simpleDialogPromptViewController;

        public static void SetUp()
        {
            _flowCoordinator = Resources.FindObjectsOfTypeAll<MultiplayerModeSelectionFlowCoordinator>().First();
            _mpLobbyConnectionController = ReflectionUtil.GetField<MultiplayerLobbyConnectionController, MultiplayerModeSelectionFlowCoordinator>(_flowCoordinator, "_multiplayerLobbyConnectionController");
            _joiningLobbyViewController = ReflectionUtil.GetField<JoiningLobbyViewController, MultiplayerModeSelectionFlowCoordinator>(_flowCoordinator, "_joiningLobbyViewController");
            _simpleDialogPromptViewController = ReflectionUtil.GetField<SimpleDialogPromptViewController, MultiplayerModeSelectionFlowCoordinator>(_flowCoordinator, "_simpleDialogPromptViewController");
        }

        public static void TearDown()
        {
            MpModeSelectionActivatedPatch.DisableButton();
        }
        #endregion

        #region Private method helpers
        public static void PresentViewController(ViewController viewController, Action finishedCallback = null, AnimationDirection animationDirection = AnimationDirection.Vertical, bool immediately = false)
        {
            _flowCoordinator.InvokeMethod<object, MultiplayerModeSelectionFlowCoordinator>("PresentViewController", new object[] {
                viewController, finishedCallback, animationDirection, immediately
            });
        }

        public static void ReplaceTopViewController(ViewController viewController, Action finishedCallback = null, ViewController.AnimationType animationType = ViewController.AnimationType.In, ViewController.AnimationDirection animationDirection = ViewController.AnimationDirection.Horizontal)
        {
            _flowCoordinator.InvokeMethod<object, MultiplayerModeSelectionFlowCoordinator>("ReplaceTopViewController", new object[] {
                viewController, finishedCallback, animationType, animationDirection
            });
        }

        public static void DismissViewController(ViewController viewController, ViewController.AnimationDirection animationDirection = ViewController.AnimationDirection.Horizontal, Action finishedCallback = null, bool immediately = false)
        {
            _flowCoordinator.InvokeMethod<object, MultiplayerModeSelectionFlowCoordinator>("DismissViewController", new object[] {
                viewController, animationDirection, finishedCallback, immediately
            });
        }

        public static void SetTitle(string title)
        {
            _flowCoordinator.InvokeMethod<object, MultiplayerModeSelectionFlowCoordinator>("SetTitle", new object[] {
                title, ViewController.AnimationType.In
            });
        }

        public static void TriggerMenuButton(MultiplayerModeSelectionViewController.MenuButton menuButton)
        {
            _flowCoordinator.InvokeMethod<object, MultiplayerModeSelectionFlowCoordinator>(
                "HandleMultiplayerLobbyControllerDidFinish", new object[]
                {
                    // nb: first param is unused by game, it's safe to pass null
                    null!, menuButton
                });
        }

        #endregion

        public static void OpenCreateServerMenu()
        {
            // Make sure any overrides are cleared when we're going to host
            MpConnect.ClearMasterServerOverride();

            // If we are initiating the server menu from our UI, assume the intent is to host a game
            Plugin.Config.LobbyAnnounceToggle = true;
            Plugin.Config.ShareQuickPlayGames = true;

            TriggerMenuButton(MultiplayerModeSelectionViewController.MenuButton.CreateServer);
        }

        public static async Task ConnectToHostedGame(HostedGameData? game, bool fixViewHierarchy = true)
        {
            if (game == null)
                return;
            
            // Cancel any previous connection attempts, ensure cancellation token is initialized
            var gameJoinCancellationTokenSource = _flowCoordinator.GetField<CancellationTokenSource,
                MultiplayerModeSelectionFlowCoordinator>("_joiningLobbyCancellationTokenSource");

            gameJoinCancellationTokenSource?.Cancel();
            gameJoinCancellationTokenSource?.Dispose();

            gameJoinCancellationTokenSource = new CancellationTokenSource();
            _flowCoordinator.SetField("_joiningLobbyCancellationTokenSource", gameJoinCancellationTokenSource);
            
            // Set global mod state for Harmony patches
            Plugin.Log.Info("--> Connecting to lobby destination now" +
                            $" (ServerCode={game.ServerCode}, HostSecret={game.HostSecret}," +
                            $" ServerType={game.ServerType}, ServerBrowserKey={game.Key}," +
                            $" Endpoint={game.Endpoint})");
            
            GlobalModState.Reset();
            GlobalModState.WeInitiatedConnection = true;
            GlobalModState.LastConnectToHostedGame = game;

            if (game.SupportsDirectConnect && game.Endpoint != null)
            {
                Plugin.Log.Info($"Attempting direct connection to endpoint: {game.Endpoint}");
                GlobalModState.DirectConnectTarget = game.Endpoint;
            }

            if (fixViewHierarchy && !PluginUi.ServerBrowserViewController.isInViewControllerHierarchy)
            {
                // Ensure Server Browser view is up - this avoids UI crashes for async joins from rich presence...
                PluginUi.LaunchServerBrowser();

                try
                {
                    await Task.Delay(1000, gameJoinCancellationTokenSource.Token);

                    if (!PluginUi.ServerBrowserViewController.isActivated)
                    {
                        // Fix for #41 (Backing out of rich presence join can cause UI softlock)
                        Plugin.Log.Warn("Aborted rich presence join - user closed server browser?");
                        return;
                    }
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }

            // Trigger game connection and UI
            var lobbyDestination = new SelectMultiplayerLobbyDestination(game.HostSecret, game.ServerCode);
            
            _mpLobbyConnectionController.CreateOrConnectToDestinationParty(lobbyDestination);

            _joiningLobbyViewController.Init($"{game.GameName} ({game.ServerCode})");

            ReplaceTopViewController(_joiningLobbyViewController, animationType: AnimationType.In,
                animationDirection: AnimationDirection.Vertical);
        }

        public static void PresentConnectionFailedError(string errorTitle = "Connection failed",
            string errorMessage = null, bool canRetry = true)
        {
            CancelLobbyJoin();

            if (GlobalModState.LastConnectToHostedGame == null)
                canRetry = false; // we don't have game info to retry with

            _simpleDialogPromptViewController.Init(errorTitle, errorMessage, "Back to browser",
                canRetry ? "Retry connection" : null, delegate(int btnId)
                {
                    switch (btnId)
                    {
                        default:
                        case 0: // Back to browser
                            MakeServerBrowserTopView();
                            break;
                        case 1: // Retry connection
                            _ = ConnectToHostedGame(GlobalModState.LastConnectToHostedGame, false);
                            break;
                    }
                });

            ReplaceTopViewController(_simpleDialogPromptViewController, null,
                ViewController.AnimationType.In, ViewController.AnimationDirection.Vertical);
        }

        public static void CancelLobbyJoin(bool hideLoading = true)
        {
            _mpLobbyConnectionController.LeaveLobby();
            
            if (hideLoading)
                _joiningLobbyViewController.HideLoading();
        }

        public static void MakeServerBrowserTopView()
        {
            ReplaceTopViewController(PluginUi.ServerBrowserViewController, null, ViewController.AnimationType.In, ViewController.AnimationDirection.Vertical);
        }
    }
}
