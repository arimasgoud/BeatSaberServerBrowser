﻿using System;
using HarmonyLib;
using HMUI;
using IPA.Utilities;
using ServerBrowser.Game;
using ServerBrowser.Game.Models;
using UnityEngine;
using UnityEngine.UI;

namespace ServerBrowser.Harmony
{
    [HarmonyPatch(typeof(MultiplayerModeSelectionViewController), "DidActivate", MethodType.Normal)]
    public static class MpModeSelectionActivatedPatch
    {
        private static Button? _btnGameBrowser;
        
        public static void Postfix(MultiplayerModeSelectionViewController __instance, bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            // Raise internal event
            MpEvents.RaiseOnlineMenuOpened(__instance, new OnlineMenuOpenedEventArgs()
            {
                FirstActivation = firstActivation
            });

            // Touch master server endpoint to trigger MasterServerEndPointPatch (fixes #49)
            try
            {
                __instance.GetField<INetworkConfig, MultiplayerModeSelectionViewController>("_networkConfig")
                    .masterServerEndPoint.ToString();
            }
            catch (MissingFieldException) { }

            // Enable the "game browser" button (it was left in the game but unused currently)
            _btnGameBrowser = ReflectionUtil.GetField<Button, MultiplayerModeSelectionViewController>(__instance, "_gameBrowserButton");
            _btnGameBrowser.enabled = true;
            _btnGameBrowser.gameObject.SetActive(true);

            foreach (var comp in _btnGameBrowser.GetComponents<Component>())
                comp.gameObject.SetActive(true);

            if (firstActivation)
            {
                // Move up and enlarge the button a bit
                var transform = _btnGameBrowser.gameObject.transform;
                transform.position = new Vector3(
                    transform.position.x,
                    transform.position.y + 0.4f, // carefully positioned so it is visually seperated from maintenance notice
                    transform.position.z
                );
                transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);
                _btnGameBrowser.GetComponentInChildren<CurvedTextMeshPro>()
                    .SetText("Server Browser");
            }
        }

        public static void DisableButton()
        {
            if (_btnGameBrowser == null)
                return;
            
            _btnGameBrowser.gameObject.SetActive(false);
        }
    }
}
