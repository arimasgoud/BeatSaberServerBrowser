﻿using System;
using System.Threading;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HMUI;
using ServerBrowser.Core;
using ServerBrowser.Game;
using ServerBrowser.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ServerBrowser.UI.ViewControllers
{
    #pragma warning disable CS0649
    public class ServerBrowserViewController : BeatSaberMarkupLanguage.ViewControllers.BSMLResourceViewController
    {
        public override string ResourceName => "ServerBrowser.UI.BSML.ServerBrowserViewController.bsml";

        private HostedGameFilters _filters = new HostedGameFilters();
        private CancellationTokenSource _imageLoadCancellation = null;
        private HostedGameData _selectedGame = null;
        private LoadingControl _loadingControl = null;

        #region Activation / Deactivation

        public override void __Activate(bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.__Activate(addedToHierarchy, screenSystemEnabling);

            // Attach loading control
            if (_loadingControl == null)
            {
                _loadingControl = ListLoadingControl.Create(GameList.gameObject.transform);
                if (_loadingControl != null)
                    _loadingControl.didPressRefreshButtonEvent += RefreshButtonClick;
            }

            // Begin listening for API browse responses
            HostedGameBrowser.OnUpdate += LobbyBrowser_OnUpdate;

            // Reset the UI
            SetInitialUiState();

            // Perform initial refresh
            _ = HostedGameBrowser.FullRefresh(_filters);
        }

        public override void __Deactivate(bool removedFromHierarchy, bool deactivateGameObject, bool screenSystemDisabling)
        {
            base.__Deactivate(removedFromHierarchy, deactivateGameObject, screenSystemDisabling);

            HostedGameBrowser.OnUpdate -= LobbyBrowser_OnUpdate;

            parserParams.EmitEvent("closeSearchKeyboard");

            CancelImageLoading(false);
        }
        #endregion

        #region Core UI Code

        private void SetInitialUiState()
        {
            MpModeSelection.SetTitle("Server Browser");

            ClearSelection();
            CancelImageLoading();

            StatusText.text = "Loading...";
            StatusText.color = Color.gray;

            if (_loadingControl != null)
                _loadingControl.ShowLoading("Loading servers...");
            
            // make sure the table is fully cleared, if we don't do the cell gameobjects continue to add on every load
            GameList.data.Clear();
            GameList.tableView.DeleteCells(0, GameList.tableView.numberOfCells);

            // sometimes the non-primary buttons become disabled if the server browser
            //  isn't opened until after level selection, so let's ensure they're active
            RefreshButton.gameObject.SetActive(true);
            SearchButton.gameObject.SetActive(true);
            CreateButton.gameObject.SetActive(true);

            RefreshButton.interactable = false;
            SearchButton.interactable = false;
            ConnectButton.interactable = false;

            PageUpButton.interactable = false;
            PageDownButton.interactable = false;
        }

        private void CancelImageLoading(bool reset = true)
        {
            try
            {
                if (_imageLoadCancellation != null)
                {
                    _imageLoadCancellation.Cancel();
                    _imageLoadCancellation.Dispose();
                }
            }
            catch (Exception) { }

            _imageLoadCancellation = null;

            if (reset)
            {
                _imageLoadCancellation = new CancellationTokenSource();
            }
        }

        private void ClearSelection()
        {
            GameList?.tableView?.ClearSelection();
            ConnectButton.interactable = false;
            _selectedGame = null;
        }

        private void LobbyBrowser_OnUpdate()
        {
            GameList.data.Clear();
            CancelImageLoading();

            if (!String.IsNullOrEmpty(HostedGameBrowser.ServerMessage))
            {
                ServerMessageText.text = HostedGameBrowser.ServerMessage;
                ServerMessageText.enabled = true;
            }
            else
            {
                ServerMessageText.enabled = false;
            }

            if (!HostedGameBrowser.ConnectionOk)
            {
                StatusText.text = "Failed to get server list";
                StatusText.color = Color.red;
                
                if (_loadingControl != null)
                    _loadingControl.ShowText("Failed to load servers", true);
            }
            else if (!HostedGameBrowser.AnyResultsOnPage)
            {
                if (HostedGameBrowser.TotalResultCount == 0)
                {
                    if (IsSearching)
                    {
                        StatusText.text = "No servers found matching your search";
                    }
                    else
                    {
                        StatusText.text = "Sorry, no servers found";
                    }
                    
                    if (_loadingControl != null)
                        _loadingControl.ShowText("No servers found", true);
                }
                else
                {
                    StatusText.text = "This page is empty";
                    RefreshButtonClick(); // this is awkward so force a refresh
                }

                StatusText.color = Color.red;
            }
            else
            {
                StatusText.text = $"Found {HostedGameBrowser.TotalResultCount} "
                    + (HostedGameBrowser.TotalResultCount == 1 ? "server" : "servers")
                    + $" (Page {HostedGameBrowser.PageIndex + 1} of {HostedGameBrowser.TotalPageCount})";

                StatusText.color = Color.green;

                if (IsSearching)
                {
                    StatusText.text += " (Filtered)";
                }

                foreach (var lobby in HostedGameBrowser.LobbiesOnPage)
                {
                    GameList.data.Add(new HostedGameCellData(_imageLoadCancellation, CellUpdateCallback, lobby));
                }
                
                if (_loadingControl != null)
                    _loadingControl.Hide();
            }

            if (!MpSession.GetLocalPlayerHasMultiplayerExtensions())
            {
                FilterModdedButton.interactable = false;

                if (HostedGameBrowser.ConnectionOk)
                {
                    StatusText.text += "\r\nMultiplayerExtensions not detected, hiding modded games";
                    StatusText.color = Color.yellow;
                }
            }

            AfterCellsCreated();

            RefreshButton.interactable = true;

            SearchButton.interactable = (IsSearching || HostedGameBrowser.AnyResultsOnPage);
            SearchButton.SetButtonText(IsSearching ? "<color=#32CD32>Search</color>" : "Search");

            PageUpButton.interactable = HostedGameBrowser.PageIndex > 0;
            PageDownButton.interactable = HostedGameBrowser.PageIndex < HostedGameBrowser.TotalPageCount - 1;
        }

        public bool IsSearching => _filters.AnyActive;
        #endregion

        #region BSML UI Components
        [UIParams]
        public BeatSaberMarkupLanguage.Parser.BSMLParserParams parserParams;

        [UIComponent("mainContentRoot")]
        public VerticalLayoutGroup MainContentRoot;

        [UIComponent("searchKeyboard")]
        public ModalKeyboard SearchKeyboard;

        [UIComponent("serverMessageText")]
        public TextMeshProUGUI ServerMessageText;

        [UIComponent("statusText")]
        public TextMeshProUGUI StatusText;

        [UIComponent("lobbyList")]
        public CustomListTableData GameList;

        [UIComponent("refreshButton")]
        public Button RefreshButton;

        [UIComponent("searchButton")]
        public Button SearchButton;

        [UIComponent("createButton")]
        public Button CreateButton;

        [UIComponent("connectButton")]
        public Button ConnectButton;

        [UIComponent("pageUpButton")]
        private Button PageUpButton;

        [UIComponent("pageDownButton")]
        private Button PageDownButton;

        [UIComponent("loadingModal")]
        public ModalView LoadingModal;

        [UIComponent("filterModded")]
        public Button FilterModdedButton;
        #endregion

        #region BSML UI Bindings
        [UIValue("searchValue")]
        public string SearchValue
        {
            get => _filters.TextSearch;
            set
            {
                _filters.TextSearch = value;
                NotifyPropertyChanged();
            }
        }

        public bool FilterFull
        {
            get => _filters.HideFullGames;
            set
            {
                _filters.HideFullGames = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(FilterFullColor));
            }
        }

        [UIValue("filterFullColor")]
        public string FilterFullColor
        {
            get
            {
                switch (FilterFull)
                {
                    case true:
                        return "#32CD32";
                    case false:
                        return "#FF0000";
                    default:
                        return "#FF0000";
                }
            }
        }

        public bool FilterInProgress
        {
            get => _filters.HideInProgressGames;
            set
            {
                _filters.HideInProgressGames = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(FilterInProgressColor));
            }
        }

        [UIValue("filterInProgressColor")]
        public string FilterInProgressColor
        {
            get
            {
                switch (FilterInProgress)
                {
                    case true:
                        return "#32CD32";
                    case false:
                        return "#FF0000";
                    default:
                        return "#FF0000";
                }
            }
        }

        public bool FilterModded
        {
            get => _filters.HideModdedGames;
            set
            {
                _filters.HideModdedGames = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(FilterModdedColor));
            }
        }

        [UIValue("filterModdedColor")]
        public string FilterModdedColor
        {
            get
            {
                switch (FilterModded)
                {
                    case true:
                        return "#32CD32";
                    case false:
                        return "#FF0000";
                    default:
                        return "#FF0000";
                }
            }
        }
        #endregion

        #region BSML UI Actions
        [UIAction("searchKeyboardSubmit")]
        private void SearchKeyboardSubmit(string text)
        {
            SearchValue = text;

            // Make main content visible again
            MainContentRoot.gameObject.SetActive(true);

            // Hit refresh
            RefreshButtonClick();
        }

        [UIAction("refreshButtonClick")]
        private void RefreshButtonClick()
        {
            SetInitialUiState();
            _ = HostedGameBrowser.FullRefresh(_filters);
        }

        [UIAction("searchButtonClick")]
        private void SearchButtonClick()
        {
            ClearSelection();
            parserParams.EmitEvent("openSearchKeyboard");
        }

        [UIAction("filterfullClick")]
        private void FilterFullClick()
        {
            FilterFull = !FilterFull;
        }

        [UIAction("filterInProgressClick")]
        private void FilterInProgressClick()
        {
            FilterInProgress = !FilterInProgress;
        }

        [UIAction("filterModdedClick")]
        private void FilterModdedClick()
        {
            FilterModded = !FilterModded;
        }

        [UIAction("createButtonClick")]
        private void CreateButtonClick()
        {
            MpModeSelection.OpenCreateServerMenu();
        }

        [UIAction("connectButtonClick")]
        private void ConnectButtonClick()
        {
            if (_selectedGame?.CanJoin ?? false)
            {
                MpConnect.Join(_selectedGame);
            }
            else
            {
                ClearSelection();
            }   
        }

        [UIAction("listSelect")]
        private void ListSelect(TableView tableView, int row)
        {
            var selectedRow = GameList.data[row];

            if (selectedRow == null)
            {
                ClearSelection();
                return;
            }

            var cellData = (HostedGameCellData)selectedRow;
            _selectedGame = cellData.Game;

            ConnectButton.interactable = _selectedGame.CanJoin;
        }

        [UIAction("pageUpButtonClick")]
        private void PageUpButtonClick()
        {
            if (HostedGameBrowser.PageIndex > 0)
            {
                SetInitialUiState();
                _ = HostedGameBrowser.LoadPage((HostedGameBrowser.PageIndex - 1) * HostedGameBrowser.PageSize, _filters);
            }
        }

        [UIAction("pageDownButtonClick")]
        private void PageDownButtonClick()
        {
            if (HostedGameBrowser.PageIndex < HostedGameBrowser.TotalPageCount - 1)
            {
                SetInitialUiState();
                _ = HostedGameBrowser.LoadPage((HostedGameBrowser.PageIndex + 1) * HostedGameBrowser.PageSize, _filters);
            }
        }
        #endregion

        #region Custom Cell Behaviors
        private void CellUpdateCallback(HostedGameCellData cellInfo)
        {
            GameList.tableView.RefreshCellsContent();

            foreach (var cell in GameList.tableView.visibleCells)
            {
                var extensions = cell.gameObject.GetComponent<HostedGameCellExtensions>();

                if (extensions != null)
                {
                    extensions.RefreshContent((HostedGameCellData)GameList.data[cell.idx]);
                }
            }
        }

        private void AfterCellsCreated()
        {
            GameList.tableView.selectionType = TableViewSelectionType.Single;

            GameList.tableView.ReloadData(); // should cause visibleCells to be updated

            foreach (var cell in GameList.tableView.visibleCells)
            {
                var extensions = cell.gameObject.GetComponent<HostedGameCellExtensions>();
                var data = (HostedGameCellData)GameList.data[cell.idx];

                if (extensions == null)
                {
                    cell.gameObject.AddComponent<HostedGameCellExtensions>().Configure(cell, data);
                }
                else
                {
                    extensions.RefreshContent(data);
                }
            }

            ClearSelection();
        }
        #endregion
    }
    #pragma warning restore CS0649
}