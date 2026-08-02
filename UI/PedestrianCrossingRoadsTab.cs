using System;
using ColossalFramework.UI;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    internal class PedestrianCrossingRoadsTab : MonoBehaviour
    {
        private const string ComponentName = "PedestrianCrossingToolkitRoadsTab";
        private const string TabName = "PedestrianCrossingToolkitCrossingsTab";
        private const string PageName = "PedestrianCrossingToolkitCrossingsPage";
        private const string TabIconName = "PedestrianCrossingToolkitCrossingsTabIcon";
        private const float RetrySeconds = 0.75f;
        private const int LogAttemptLimit = 8;

        public static PedestrianCrossingRoadsTab Instance;

        private float _nextAttemptTime;
        private int _attempts;
        private bool _installed;
        private UITabstrip _tabstrip;
        private UIButton _crossingsTab;
        private UIComponent _crossingsPage;
        private PedestrianCrossingRoadsTabPanel _crossingsPanel;
        private bool _wasOpen;

        public static bool IsOpen
        {
            get
            {
                return Instance != null
                       && Instance._installed
                       && IsEffectivelyVisible(Instance._crossingsPage);
            }
        }

        public static void CreateIfNeeded(UIView view)
        {
            if (view == null || Instance != null)
                return;

            Instance = view.gameObject.AddComponent<PedestrianCrossingRoadsTab>();
            Instance.name = ComponentName;
        }

        public static void DestroyInstance()
        {
            if (Instance == null)
                return;

            PedestrianCrossingRoadsTab instance = Instance;
            Instance = null;
            UnityEngine.Object.Destroy(instance);
        }

        public static void RefreshInstance()
        {
            if (Instance != null && Instance._crossingsPanel != null)
                Instance._crossingsPanel.Refresh();
        }

        public static bool IsMouseOverUi()
        {
            return Instance != null
                   && (IsMouseOverComponent(Instance._crossingsTab)
                       || IsMouseOverComponent(Instance._crossingsPage));
        }

        public static bool IsToolkitComponentOrChild(UIComponent component)
        {
            if (Instance == null)
                return false;

            while (component != null)
            {
                if (component == Instance._crossingsTab
                    || component == Instance._crossingsPage
                    || component == Instance._crossingsPanel)
                {
                    return true;
                }

                component = component.parent;
            }

            return false;
        }

        private void LateUpdate()
        {
            if (_installed)
            {
                if (_tabstrip == null
                    || _crossingsTab == null
                    || _crossingsPage == null
                    || _crossingsPanel == null)
                {
                    ClearInstalledReferences();
                }
                else
                {
                    _crossingsTab.isEnabled =
                        !PedestrianCrossingToolkitState.IsAutoScanObservationActive;
                    bool open = IsOpen;
                    if (_wasOpen && !open)
                        PedestrianCrossingToolkitState.ClearValidationProblemMarkersForCrossingTabClose();
                    _wasOpen = open;
                }

                return;
            }

            if (Time.realtimeSinceStartup < _nextAttemptTime)
                return;

            _nextAttemptTime = Time.realtimeSinceStartup + RetrySeconds;
            _attempts++;
            TryInstall();
        }

        private void OnDestroy()
        {
            PedestrianCrossingRoadsHoverPreview.DestroyInstance();
            RemoveInstalledUi();
            if (Instance == this)
                Instance = null;
        }

        private void TryInstall()
        {
            RoadsGroupPanel roadsGroupPanel = FindRoadsGroupPanel();
            UITabstrip tabstrip = FindGroupTabstrip(roadsGroupPanel);
            if (tabstrip == null)
            {
                if (_attempts <= LogAttemptLimit)
                {
                    PedestrianCrossingLog.Info(
                        "Road-building category strip not ready; waiting to add the Crossing tab. attempt="
                        + _attempts);
                }

                return;
            }

            UIButton existingTab = FindChild(tabstrip, TabName) as UIButton;
            UIComponent existingPage = FindChild(tabstrip.tabPages, PageName);
            if (existingTab != null && existingPage != null)
            {
                PedestrianCrossingRoadsTabPanel existingPanel =
                    existingPage.GetComponentInChildren<PedestrianCrossingRoadsTabPanel>();
                if (existingPanel == null)
                    existingPanel = existingPage.AddUIComponent<PedestrianCrossingRoadsTabPanel>();

                AdoptInstalledUi(tabstrip, existingTab, existingPage, existingPanel);
                return;
            }

            UITabContainer tabPages = tabstrip.tabPages;
            if (tabPages == null)
            {
                Debug.LogWarning(
                    "[PedestrianCrossingToolkit] Road-building category strip has no page container; the Crossing tab remains unavailable.");
                return;
            }

            int oldPageCount = tabPages.components.Count;
            GameObject buttonTemplate = UITemplateManager.GetAsGameObject("SubbarButtonTemplate");
            GameObject pageTemplate = UITemplateManager.GetAsGameObject("SubbarPanelTemplate");
            UIButton tab = null;
            if (buttonTemplate != null && pageTemplate != null)
            {
                tab = tabstrip.AddTab(
                    TabName,
                    buttonTemplate,
                    pageTemplate,
                    new Type[0]) as UIButton;
            }

            if (tab == null)
                tab = tabstrip.AddTab(string.Empty, true);

            if (tab == null)
            {
                Debug.LogWarning(
                    "[PedestrianCrossingToolkit] Road-building category strip refused the Pedestrian Crossing Toolkit Crossing tab.");
                return;
            }

            UIComponent page = null;
            if (tabPages.components.Count > oldPageCount)
                page = tabPages.components[oldPageCount];
            if (page == null)
                page = tabPages.AddTabPage("Crossings");

            if (page == null)
            {
                UnityEngine.Object.Destroy(tab.gameObject);
                Debug.LogWarning(
                    "[PedestrianCrossingToolkit] Road-building Crossing tab page creation failed; the incomplete tab was removed.");
                return;
            }

            tab.name = TabName;
            page.name = PageName;
            page.isVisible = false;
            PedestrianCrossingRoadsTabPanel panel = page.AddUIComponent<PedestrianCrossingRoadsTabPanel>();
            AdoptInstalledUi(tabstrip, tab, page, panel);

            PedestrianCrossingLog.Info(
                "Added the PCT Crossing tab to the native road-building category strip: strip="
                + GetPath(tabstrip)
                + " page="
                + GetPath(page)
                + ".");
        }

        private void AdoptInstalledUi(
            UITabstrip tabstrip,
            UIButton tab,
            UIComponent page,
            PedestrianCrossingRoadsTabPanel panel)
        {
            _tabstrip = tabstrip;
            _crossingsTab = tab;
            _crossingsPage = page;
            _crossingsPanel = panel;
            ConfigureTab(tabstrip, tab);
            tab.eventClick -= OnCrossingsTabClicked;
            tab.eventClick += OnCrossingsTabClicked;
            page.eventVisibilityChanged -= OnCrossingsPageVisibilityChanged;
            page.eventVisibilityChanged += OnCrossingsPageVisibilityChanged;
            _installed = true;
            _wasOpen = IsOpen;
            panel.Refresh();
        }

        private void OnCrossingsTabClicked(UIComponent component, UIMouseEventParameter eventParam)
        {
            PedestrianCrossingToolkitPanel.NotifyToolkitUiInput(false);
            PedestrianCrossingToolkitState.SetActiveMode(PedestrianToolMode.None);
            if (ToolsModifierControl.toolController != null)
                ToolsModifierControl.SetTool<DefaultTool>();

            if (_crossingsPage != null)
            {
                _crossingsPage.isVisible = true;
                _crossingsPage.BringToFront();
            }

            PedestrianCrossingToolkitPanel.RefreshInstance();
            RefreshInstance();
        }

        private void OnCrossingsPageVisibilityChanged(UIComponent component, bool visible)
        {
            if (visible)
                RefreshInstance();
            else
            {
                PedestrianCrossingToolkitState.ClearValidationProblemMarkersForCrossingTabClose();
                PedestrianCrossingAutoScanInstructionsPanel.HideInstance();
                PedestrianCrossingRoadsHoverPreview.HideInstance();
                PedestrianCrossingToolkitState.SetActiveMode(PedestrianToolMode.None);
                if (ToolsModifierControl.toolController != null
                    && ToolsModifierControl.toolController.CurrentTool is PedestrianCrossingInteractionTool)
                {
                    ToolsModifierControl.SetTool<DefaultTool>();
                }
            }
        }

        private void RemoveInstalledUi()
        {
            if (_crossingsTab != null)
            {
                _crossingsTab.eventClick -= OnCrossingsTabClicked;
                UnityEngine.Object.Destroy(_crossingsTab.gameObject);
            }

            if (_crossingsPage != null)
            {
                _crossingsPage.eventVisibilityChanged -= OnCrossingsPageVisibilityChanged;
                UnityEngine.Object.Destroy(_crossingsPage.gameObject);
            }

            ClearInstalledReferences();
        }

        private void ClearInstalledReferences()
        {
            _installed = false;
            _tabstrip = null;
            _crossingsTab = null;
            _crossingsPage = null;
            _crossingsPanel = null;
            _wasOpen = false;
        }

        private static RoadsGroupPanel FindRoadsGroupPanel()
        {
            UIView view = UIView.GetAView();
            if (view == null)
                return null;

            RoadsGroupPanel[] candidates = view.GetComponentsInChildren<RoadsGroupPanel>(true);
            return candidates == null || candidates.Length == 0 ? null : candidates[0];
        }

        private static UITabstrip FindGroupTabstrip(RoadsGroupPanel roadsGroupPanel)
        {
            if (roadsGroupPanel == null)
                return null;

            UITabstrip[] tabstrips = roadsGroupPanel.GetComponentsInChildren<UITabstrip>(true);
            UITabstrip best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < tabstrips.Length; i++)
            {
                UITabstrip candidate = tabstrips[i];
                if (candidate == null || candidate.tabPages == null)
                    continue;

                string path = GetPath(candidate).ToLowerInvariant();
                int score = 20;
                if (candidate.name == "GroupToolstrip")
                    score += 100;
                if (path.IndexOf("roadsgrouppanel") >= 0)
                    score += 60;
                if (path.IndexOf("grouptoolstrip") >= 0)
                    score += 40;
                if (path.IndexOf("elevation") >= 0)
                    score -= 100;
                score += Mathf.Min(20, candidate.tabCount * 2);
                if (score <= bestScore)
                    continue;

                best = candidate;
                bestScore = score;
            }

            return bestScore >= 80 ? best : null;
        }

        private static void ConfigureTab(UITabstrip tabstrip, UIButton tab)
        {
            if (tabstrip == null || tab == null)
                return;

            UIButton template = FindTemplateButton(tabstrip, tab);
            if (template != null)
            {
                tab.atlas = template.atlas;
                tab.normalBgSprite = template.normalBgSprite;
                tab.hoveredBgSprite = template.hoveredBgSprite;
                tab.pressedBgSprite = template.pressedBgSprite;
                tab.focusedBgSprite = template.focusedBgSprite;
                tab.disabledBgSprite = template.disabledBgSprite;
                tab.width = template.width;
                tab.height = template.height;
            }

            tab.text = string.Empty;
            tab.normalFgSprite = string.Empty;
            tab.hoveredFgSprite = string.Empty;
            tab.pressedFgSprite = string.Empty;
            tab.focusedFgSprite = string.Empty;
            tab.disabledFgSprite = string.Empty;
            tab.tooltip = "Crossings — Pedestrian Crossing Toolkit";
            tab.isVisible = true;
            tab.isEnabled = true;

            UISprite icon = tab.Find<UISprite>(TabIconName);
            if (icon == null)
            {
                icon = tab.AddUIComponent<UISprite>();
                icon.name = TabIconName;
            }

            UITextureAtlas iconAtlas = PedestrianCrossingToolkitLauncherButton.GetOrCreateIconAtlas();
            icon.atlas = iconAtlas;
            icon.spriteName = PedestrianCrossingToolkitLauncherButton.IconSpriteName;
            icon.width = Mathf.Min(30f, Mathf.Max(20f, tab.width - 12f));
            icon.height = Mathf.Min(30f, Mathf.Max(20f, tab.height - 10f));
            icon.relativePosition = new Vector3(
                Mathf.Max(0f, (tab.width - icon.width) * 0.5f),
                Mathf.Max(0f, (tab.height - icon.height) * 0.5f));
            icon.isInteractive = false;
            icon.isVisible = iconAtlas != null;
            if (iconAtlas != null)
            {
                icon.BringToFront();
            }
            else
            {
                tab.text = "X";
                tab.textScale = 0.9f;
            }
        }

        private static UIButton FindTemplateButton(UITabstrip tabstrip, UIButton crossingTab)
        {
            if (tabstrip == null)
                return null;

            for (int i = 0; i < tabstrip.components.Count; i++)
            {
                UIButton button = tabstrip.components[i] as UIButton;
                if (button != null && button != crossingTab && button.name != TabName)
                    return button;
            }

            return null;
        }

        private static UIComponent FindChild(UIComponent parent, string childName)
        {
            if (parent == null)
                return null;

            UIComponent[] children = parent.GetComponentsInChildren<UIComponent>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == childName)
                    return children[i];
            }

            return null;
        }

        private static bool IsEffectivelyVisible(UIComponent component)
        {
            while (component != null)
            {
                if (!component.isVisible)
                    return false;
                component = component.parent;
            }

            return true;
        }

        private static bool IsMouseOverComponent(UIComponent component)
        {
            return component != null && IsEffectivelyVisible(component) && component.containsMouse;
        }

        private static string GetPath(UIComponent component)
        {
            if (component == null)
                return string.Empty;

            string path = component.name;
            Transform current = component.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }

    public class PedestrianCrossingRoadsTabPanel : UIPanel
    {
        private const float HorizontalMargin = 10f;
        private const float ButtonGap = 8f;
        private const float ButtonHeight = 64f;

        private UIButton _standardButton;
        private UIButton _signalButton;
        private UIButton _autoSubwayButton;
        private UIButton _manualSubwayButton;
        private UIButton _bridgeButton;
        private UIButton _autoScanButton;

        public override void Start()
        {
            base.Start();

            name = "PedestrianCrossingToolkitCrossingsControls";
            isInteractive = true;
            relativePosition = Vector3.zero;

            _standardButton = AddButton(
                "Standard Crossing — place a standard surface crossing.",
                "Standard Crossing",
                "Adds a visible zebra crossing at the selected road position. Pedestrians cross without traffic signals.",
                "Select the tool, then choose a valid road position.",
                PedestrianToolMode.MidBlockCrossing,
                CrossingRoadsIconKind.Standard);
            _signalButton = AddButton(
                "Signalled Crossing — place a demand-led signalled crossing.",
                "Signalled Crossing",
                "Adds a demand-led crossing at a valid road join. Vehicle and pedestrian lights change when pedestrians are waiting.",
                "Select the tool, then choose a valid road join.",
                PedestrianToolMode.SignalCrossing,
                CrossingRoadsIconKind.Signal);
            _autoSubwayButton = AddButton(
                "Automatic Subway — place a pedestrian subway with automatic entrances.",
                "Automatic Subway",
                "Builds a pedestrian underpass across the selected road and positions both entrances automatically.",
                "Select the tool, then choose a valid road position.",
                PedestrianToolMode.SubwayLink,
                CrossingRoadsIconKind.AutoSubway);
            _manualSubwayButton = AddButton(
                "Manual Subway — select both entrances for a manual subway crossing.",
                "Manual Subway",
                "Builds a pedestrian underpass between two pavement-side entrance points that you choose.",
                "Select the tool, then choose both entrance points.",
                PedestrianToolMode.SubwayPointToPoint,
                CrossingRoadsIconKind.ManualSubway);
            _bridgeButton = AddButton(
                "Pedestrian Bridge — place a generated pedestrian bridge.",
                "Pedestrian Bridge",
                "Builds a covered elevated pedestrian route over a supported road or rail line.",
                "Select the tool, then choose a valid crossing position.",
                PedestrianToolMode.PedestrianBridge,
                CrossingRoadsIconKind.Bridge);
            _autoScanButton = AddButton(
                "Auto Scan — monitor pedestrian movement and propose or apply useful crossings.",
                "Auto Scan",
                "Observes pedestrian movement for one minute, then plans useful crossing proposals across the city.",
                "Select Auto Scan, then choose preview or direct application.",
                PedestrianToolMode.None,
                CrossingRoadsIconKind.AutoScan);
            _autoScanButton.eventClick += OnAutoScanClicked;

            LayoutButtons();
            Refresh();
        }

        public override void Update()
        {
            base.Update();
            LayoutButtons();
            Refresh();
        }

        public void Refresh()
        {
            if (_standardButton == null)
                return;

            PedestrianToolMode mode = PedestrianCrossingToolkitState.ActiveMode;
            bool actionEnabled = PedestrianCrossingToolkitState.Enabled
                                 && !PedestrianCrossingToolkitState.IsAutoScanObservationActive
                                 && !PedestrianCrossingToolkitState.HasAutoScanPreviewPlan;

            SetButtonState(_standardButton, mode == PedestrianToolMode.MidBlockCrossing, actionEnabled);
            SetButtonState(_signalButton, mode == PedestrianToolMode.SignalCrossing, actionEnabled);
            SetButtonState(_autoSubwayButton, mode == PedestrianToolMode.SubwayLink, actionEnabled);
            SetButtonState(_manualSubwayButton, mode == PedestrianToolMode.SubwayPointToPoint, actionEnabled);
            SetButtonState(_bridgeButton, mode == PedestrianToolMode.PedestrianBridge, actionEnabled);
            _autoScanButton.isEnabled = PedestrianCrossingToolkitState.Enabled
                                        && !PedestrianCrossingToolkitState.IsAutoScanObservationActive;
        }

        private UIButton AddButton(
            string tooltip,
            string previewTitle,
            string previewDescription,
            string previewHint,
            PedestrianToolMode mode,
            CrossingRoadsIconKind iconKind)
        {
            UIButton button = AddUIComponent<UIButton>();
            button.text = string.Empty;
            button.height = ButtonHeight;
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.focusedBgSprite = "ButtonMenuPressed";
            button.disabledBgSprite = "ButtonMenuDisabled";

            UISprite icon = button.AddUIComponent<UISprite>();
            icon.atlas = CrossingRoadsIconFactory.GetAtlas(iconKind);
            icon.spriteName = CrossingRoadsIconFactory.GetSpriteName(iconKind);
            icon.width = 52f;
            icon.height = 52f;
            icon.relativePosition = new Vector3(6f, 6f);
            icon.isInteractive = false;

            if (!CrossingRoadsInfoTooltip.Bind(
                    button,
                    iconKind,
                    previewTitle,
                    previewDescription,
                    previewHint))
            {
                button.tooltip = tooltip;
                button.eventMouseEnter += delegate
                {
                    PedestrianCrossingRoadsHoverPreview.ShowFor(
                        button,
                        iconKind,
                        previewTitle,
                        previewDescription,
                        previewHint);
                };
                button.eventMouseLeave += delegate
                {
                    PedestrianCrossingRoadsHoverPreview.HideInstance();
                };
            }

            if (mode != PedestrianToolMode.None)
            {
                button.eventClick += delegate
                {
                    PedestrianCrossingToolkitPanel.NotifyToolkitUiInput(false);
                    PedestrianCrossingToolkitPanel.ActivateModeFromUi(mode);
                };
            }

            return button;
        }

        private void OnAutoScanClicked(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
                return;

            if (PedestrianCrossingToolkitState.HasAutoScanPreviewPlan)
            {
                PedestrianCrossingAutoScanInstructionsPanel.ShowIfNeeded();
                return;
            }

            PedestrianCrossingToolkitPanel.NotifyToolkitUiInput(false);
            PedestrianCrossingToolkitState.SetActiveMode(PedestrianToolMode.None);
            if (ToolsModifierControl.toolController != null
                && ToolsModifierControl.toolController.CurrentTool is PedestrianCrossingInteractionTool)
            {
                ToolsModifierControl.SetTool<DefaultTool>();
            }

            ConfirmPanel.ShowModal(
                "Auto Scan",
                "Would you like to preview the suggested crossings before they are applied?\n\nYes: preview suggestions. No: apply directly. Close or Escape: cancel.",
                OnAutoScanPreviewChoice);
        }

        private static void OnAutoScanPreviewChoice(UIComponent component, int result)
        {
            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive
                || PedestrianCrossingToolkitState.HasAutoScanPreviewPlan)
                return;

            if (result < 0)
            {
                PedestrianCrossingLog.Advanced(
                    "[PedestrianCrossingToolkit] Auto Scan choice dialog cancelled before observation started.");
                return;
            }

            PedestrianCrossingToolkitState.SetAutoScanPreviewConfirmEnabled(result == 1);
            PedestrianCrossingToolkitState.BeginAutoScanObservation();
        }

        private void LayoutButtons()
        {
            if (parent == null || _standardButton == null)
                return;

            width = parent.width;
            height = parent.height;
            float availableWidth = Mathf.Max(700f, width - (HorizontalMargin * 2f));
            float buttonWidth = Mathf.Clamp(
                (availableWidth - (ButtonGap * 5f)) / 6f,
                96f,
                128f);
            UIButton[] buttons =
            {
                _standardButton,
                _signalButton,
                _autoSubwayButton,
                _manualSubwayButton,
                _bridgeButton,
                _autoScanButton
            };

            float x = HorizontalMargin;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].width = buttonWidth;
                buttons[i].relativePosition = new Vector3(x, 8f);
                UISprite icon = buttons[i].GetComponentInChildren<UISprite>();
                if (icon != null)
                    icon.relativePosition = new Vector3((buttonWidth - icon.width) * 0.5f, 6f);
                x += buttonWidth + ButtonGap;
            }
        }

        private static void SetButtonState(UIButton button, bool active, bool enabled)
        {
            button.isEnabled = enabled;
            button.normalBgSprite = active ? "ButtonMenuPressed" : "ButtonMenu";
            button.hoveredBgSprite = active ? "ButtonMenuPressed" : "ButtonMenuHovered";
        }
    }
}
