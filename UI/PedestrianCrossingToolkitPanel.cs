using System;
using ColossalFramework.UI;
using UnityEngine;
using UnifiedTransitLauncherToolbar = ScratchyBald.CitiesSkylines.UI.UnifiedTransitLauncherToolbar;

namespace PedestrianCrossingToolkit
{
    public class PedestrianCrossingToolkitPanel : UIPanel
    {
        public static PedestrianCrossingToolkitPanel Instance;

        private const float UiShieldPadding = 8f;
        private const float TopHudBlockHeight = 96f;
        private const float BottomHudBlockHeight = 128f;
        private const float ExpandedPanelHeight = 204f;
        private const float MonitoringPanelHeight = 32f;
        private const string NormalPanelTitle = "Pedestrian Crossing Toolkit";
        private const string MonitoringPanelTitle = "Monitoring your city";
        private static bool _uiPointerCaptured;
        private static int _uiBlockUntilFrame;
        private static int _ignoreRightClickCloseFrame = -1;

        private UIPanel _titleBar;
        private UILabel _titleLabel;
        private UILabel _statusLabel;
        private UIButton _autoScanButton;
        private UICheckBox _autoScanPreviewCheckbox;
        private UIPanel _autoScanPreviewCheckboxRow;
        private UIButton _rejectPreviewButton;
        private UIButton _applyPreviewButton;
        private UIButton _cancelPreviewButton;
        private UIButton _closeButton;
        private bool _monitoringLayout;
        private bool _panelDragging;
        private Vector2 _panelDragStartMouse;
        private Vector3 _panelDragStartPosition;

        public static bool IsOpen
        {
            get { return Instance != null && Instance.isVisible; }
        }

        public static bool IsWorkspaceOpen
        {
            get { return IsOpen || PedestrianCrossingRoadsTab.IsOpen; }
        }

        public static bool TryGetPanelScreenRect(out Rect rect)
        {
            rect = default(Rect);
            if (Instance == null || !Instance.isVisible)
                return false;

            UIView view = UIView.GetAView();
            if (view == null || view.fixedWidth <= 0f || view.fixedHeight <= 0f || Screen.width <= 0 || Screen.height <= 0)
                return false;

            Vector3 position = Instance.absolutePosition;
            float scaleX = Screen.width / view.fixedWidth;
            float scaleY = Screen.height / view.fixedHeight;
            rect = new Rect(position.x * scaleX, position.y * scaleY, Instance.width * scaleX, Instance.height * scaleY);
            return true;
        }

        public override void Start()
        {
            base.Start();

            const float panelWidth = 520f;
            const float margin = 16f;
            const float contentWidth = panelWidth - (margin * 2f);

            Instance = this;
            name = "PedestrianCrossingToolkitPanel";
            width = panelWidth;
            height = ExpandedPanelHeight;
            backgroundSprite = "MenuPanel2";
            color = new Color32(40, 48, 56, 245);
            canFocus = true;
            isInteractive = true;
            relativePosition = new Vector3(170f, 140f);
            RegisterInputShield(this);

            _titleBar = AddUIComponent<UIPanel>();
            _titleBar.width = width;
            _titleBar.height = 32;
            _titleBar.relativePosition = Vector3.zero;
            _titleBar.backgroundSprite = "MenuPanel";
            _titleBar.isInteractive = true;
            RegisterInputShield(_titleBar);

            _titleLabel = _titleBar.AddUIComponent<UILabel>();
            _titleLabel.text = NormalPanelTitle;
            _titleLabel.textScale = 0.9f;
            _titleLabel.autoSize = false;
            _titleLabel.autoHeight = false;
            _titleLabel.width = 466f;
            _titleLabel.height = 22f;
            _titleLabel.relativePosition = new Vector3(12f, 8f);
            _titleLabel.isInteractive = true;
            RegisterInputShield(_titleLabel);

            _titleBar.eventMouseDown += OnPanelDragMouseDown;
            _titleBar.eventMouseMove += OnPanelDragMouseMove;
            _titleBar.eventMouseUp += OnPanelDragMouseUp;
            _titleLabel.eventMouseDown += OnPanelDragMouseDown;
            _titleLabel.eventMouseMove += OnPanelDragMouseMove;
            _titleLabel.eventMouseUp += OnPanelDragMouseUp;

            _closeButton = AddButton(_titleBar, "x", 486f, 5f, 24f, 22f, OnCloseClicked);
            _closeButton.tooltip = "Close";

            _statusLabel = AddLabel(this, margin, 42f, contentWidth, 32f);

            _autoScanPreviewCheckboxRow = AddCheckBox(
                this,
                "Preview Auto Scan",
                "Review Auto Scan suggestions before applying them.",
                PedestrianCrossingToolkitState.AutoScanPreviewConfirmEnabled,
                value => PedestrianCrossingToolkitState.SetAutoScanPreviewConfirmEnabled(value),
                out _autoScanPreviewCheckbox);
            _autoScanPreviewCheckboxRow.relativePosition = new Vector3(margin, 76f);

            _autoScanButton = AddButton(this, "Auto\nScan", 202f, 108f, 116f, 42f, OnAutoScanClicked);
            _autoScanButton.tooltip =
                "Auto Scan monitors your city for one full minute.\n"
                + "PCT minimises as 'Monitoring your city', then reopens with results or preview instructions.\n"
                + "Results depend on time of day and city conditions; you might need to do multiple scans.";

            _rejectPreviewButton = AddButton(this, "Reject\nProposal", margin, 156f, 116f, 34f, OnRejectPreviewClicked);
            _rejectPreviewButton.tooltip = "Click a preview marker to reject that suggested crossing.";

            _applyPreviewButton = AddButton(this, "Apply\nPreview", 140f, 156f, 116f, 34f, OnApplyPreviewClicked);
            _applyPreviewButton.tooltip = "Create all remaining Auto Scan preview suggestions.";

            _cancelPreviewButton = AddButton(this, "Cancel\nPreview", 264f, 156f, 116f, 34f, OnCancelPreviewClicked);
            _cancelPreviewButton.tooltip = "Discard the staged Auto Scan suggestions.";

            Refresh();
            Hide();
        }

        public override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            base.OnDestroy();
        }

        public override void Update()
        {
            base.Update();

            if (_monitoringLayout)
                return;

            if (isVisible
                && Input.GetMouseButtonDown(1)
                && PedestrianCrossingToolkitState.ActiveMode == PedestrianToolMode.None
                && _ignoreRightClickCloseFrame != Time.frameCount)
            {
                PedestrianCrossingToolkitState.ClearAutoScanPreviewForToolkitClose();
                PedestrianCrossingAutoScanInstructionsPanel.HideInstance();
                Hide();
                _uiPointerCaptured = false;
                PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Toolkit closed: right-click with no active crossing tool.");
                return;
            }

            if (!isVisible || !Input.GetMouseButtonDown(0))
                return;

            if (!IsMouseOverExternalBlockingUi())
                return;

            CloseForExternalUiSelection();
            PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Toolkit closed: external UI selected while panel was open.");
        }

        public static void CreateIfNeeded(UIView view)
        {
            if (view == null || Instance != null)
                return;

            view.AddUIComponent(typeof(PedestrianCrossingToolkitPanel));
        }

        public static void DestroyInstance()
        {
            PedestrianCrossingAutoScanInstructionsPanel.DestroyInstance();
            if (Instance == null)
                return;

            UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        public static void Toggle()
        {
            if (Instance == null)
                return;

            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
            {
                Instance.BeginAutoScanMonitoring();
                return;
            }

            if (PedestrianCrossingToolkitState.HasAutoScanPreviewPlan)
            {
                ShowOptionsMessage(
                    "Clear All Crossings",
                    "Apply or cancel the pending PCT Auto Scan preview before clearing crossings.");
                return;
            }

            if (Instance.isVisible)
            {
                Instance.CancelActiveTool();
                PedestrianCrossingToolkitState.ClearAutoScanPreviewForToolkitClose();
                PedestrianCrossingAutoScanInstructionsPanel.HideInstance();
                Instance.Hide();
            }
            else
            {
                Instance.Show();
            }
        }

        public static void ShowManager()
        {
            if (Instance == null)
                return;

            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
            {
                Instance.BeginAutoScanMonitoring();
                return;
            }

            Instance.Show();
            Instance.BringToFront();
            Instance.Refresh();
        }

        public static void RefreshInstance()
        {
            if (Instance != null)
                Instance.Refresh();
        }

        public static void ShowScheduledValidationWarning(int crossingCount)
        {
            if (crossingCount <= 0)
                return;

            string plural = crossingCount == 1 ? string.Empty : "s";
            string message = "PCT's scheduled read-only scan found "
                             + crossingCount
                             + " crossing"
                             + plural
                             + " that needs player attention.\n\n"
                             + "Open Roads > Crossing to find the warning billboards. They remain until you close the Crossing tab or remove and rebuild the affected crossing.";
            try
            {
                ExceptionPanel panel = UIView.library != null
                    ? UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")
                    : null;
                if (panel != null)
                    panel.SetMessage("PCT Crossing Problem Detected", message, false);
                else
                    ConfirmPanel.ShowModal("PCT Crossing Problem Detected", message, null);
            }
            catch (Exception exception)
            {
                PedestrianCrossingLog.Warning(
                    "Could not display scheduled crossing validation warning: "
                    + exception.Message);
            }
        }

        public static void ShowAutoScanCompletionSummary(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                ExceptionPanel panel = UIView.library != null
                    ? UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")
                    : null;
                if (panel != null)
                    panel.SetMessage("PCT Auto Scan Complete", message, false);
                else
                    ConfirmPanel.ShowModal("PCT Auto Scan Complete", message, null);
            }
            catch (Exception exception)
            {
                PedestrianCrossingLog.Warning(
                    "Could not display Auto Scan completion summary: "
                    + exception.Message);
            }
        }

        public static void RequestClearAllCrossingsFromOptions()
        {
            if (!PedestrianCrossingToolkitState.Enabled)
            {
                ShowOptionsMessage(
                    "Clear All Crossings",
                    "Load a city before clearing PCT crossings.");
                return;
            }

            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
            {
                ShowOptionsMessage(
                    "Clear All Crossings",
                    "Wait for the active PCT Auto Scan to finish before clearing crossings.");
                return;
            }

            if (CrossingPlacementRegistry.Count <= 0)
            {
                ShowOptionsMessage(
                    "Clear All Crossings",
                    "This city has no PCT crossings to clear.");
                return;
            }

            ConfirmPanel.ShowModal(
                "Clear All Crossings",
                "This removes all PCT crossings from the loaded city. Are you sure?",
                OnClearAllConfirmed);
        }

        private static void ShowOptionsMessage(string title, string message)
        {
            try
            {
                ExceptionPanel panel = UIView.library != null
                    ? UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel")
                    : null;
                if (panel != null)
                    panel.SetMessage(title, message, false);
                else
                    ConfirmPanel.ShowModal(title, message, null);
            }
            catch (Exception exception)
            {
                PedestrianCrossingLog.Warning(
                    "Could not display PCT Options message: "
                    + exception.Message);
            }
        }

        public static void BeginAutoScanMonitoringInstance()
        {
            if (Instance != null)
                Instance.BeginAutoScanMonitoring();
        }

        public static void EndAutoScanMonitoringInstance()
        {
            if (Instance != null)
                Instance.EndAutoScanMonitoring(true);
        }

        public static void ResetAutoScanMonitoringInstance()
        {
            if (Instance != null)
                Instance.EndAutoScanMonitoring(false);
        }

        public static void ShowAutoScanPreviewInstructionsIfNeeded()
        {
            PedestrianCrossingAutoScanInstructionsPanel.ShowIfNeeded();
        }

        public static bool IsMouseOverToolkitUi()
        {
            return IsMouseOverToolkitUi(false);
        }

        public static bool IsMouseOverAnyBlockingUi()
        {
            return IsMouseOverToolkitUi(true);
        }

        public static bool IsMouseOverExternalBlockingUi()
        {
            UIComponent hoveredComponent = UIInput.hoveredComponent;
            if (hoveredComponent != null)
                return !IsToolkitComponentOrChild(hoveredComponent);

            if (IsMouseOverToolkitComponent())
                return false;

            return IsMouseOverKnownHudBand();
        }

        public static void CloseForExternalUiSelection()
        {
            if (Instance == null)
                return;

            Instance.CancelActiveTool();
            PedestrianCrossingToolkitState.ClearAutoScanPreviewForToolkitClose();
            PedestrianCrossingAutoScanInstructionsPanel.HideInstance();
            Instance.Hide();
            _uiPointerCaptured = false;
            PedestrianCrossingToolkitLauncherButton.SetExternalPressed(false);
        }

        private static bool IsMouseOverToolkitUi(bool includeExternalUi)
        {
            if (_uiPointerCaptured)
            {
                if (Input.GetMouseButton(0))
                    return true;

                _uiPointerCaptured = false;
            }

            if (_uiBlockUntilFrame >= Time.frameCount)
                return true;

            bool overToolkit = IsMouseOverToolkitComponent();
            if (overToolkit)
            {
                ShieldToolInput(Input.GetMouseButton(0));
                return true;
            }

            if (!includeExternalUi)
                return false;

            bool overHud = IsMouseOverKnownHudBand();
            if (overHud)
                ShieldToolInput(Input.GetMouseButton(0) || Input.GetMouseButtonDown(0));

            return overHud;
        }

        private static bool IsMouseOverKnownHudBand()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
                return false;

            float topY = Screen.height - Input.mousePosition.y;
            float bottomY = Input.mousePosition.y;
            return topY <= TopHudBlockHeight || bottomY <= BottomHudBlockHeight;
        }

        public static void NotifyToolkitUiInput(bool capture)
        {
            ShieldToolInput(capture);
            if (!capture && !Input.GetMouseButton(0))
                _uiPointerCaptured = false;
        }

        public static void NotifyToolClearedByRightClick()
        {
            _ignoreRightClickCloseFrame = Time.frameCount;
        }

        internal static void ActivateModeFromUi(PedestrianToolMode mode)
        {
            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
            {
                RefreshInstance();
                PedestrianCrossingRoadsTab.RefreshInstance();
                return;
            }

            if (PedestrianCrossingToolkitState.HasAutoScanPreviewPlan
                && mode != PedestrianToolMode.AutoScanReject)
            {
                RefreshInstance();
                PedestrianCrossingRoadsTab.RefreshInstance();
                return;
            }

            PedestrianCrossingToolkitState.SetActiveMode(mode);
            PedestrianCrossingInteractionTool tool = PedestrianCrossingInteractionTool.EnsureOnToolController();
            if (tool != null && ToolsModifierControl.toolController != null)
            {
                ToolsModifierControl.toolController.CurrentTool = tool;
                PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Interaction tool activated for mode: " + mode);
            }

            RefreshInstance();
            PedestrianCrossingRoadsTab.RefreshInstance();
        }

        private void ActivateMode(PedestrianToolMode mode)
        {
            ActivateModeFromUi(mode);
        }

        private void OnCloseClicked(UIComponent component, UIMouseEventParameter p)
        {
            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
                return;

            CancelActiveTool();
            PedestrianCrossingToolkitState.ClearAutoScanPreviewForToolkitClose();
            PedestrianCrossingAutoScanInstructionsPanel.HideInstance();
            Hide();
            PedestrianCrossingToolkitLauncherButton.SetExternalPressed(false);
        }

        private void OnAutoScanClicked(UIComponent component, UIMouseEventParameter p)
        {
            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
                return;

            CancelActiveTool();
            if (PedestrianCrossingToolkitState.BeginAutoScanObservation())
                PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Auto scan button started observation.");
        }

        private void BeginAutoScanMonitoring()
        {
            _monitoringLayout = true;
            height = MonitoringPanelHeight;
            _titleLabel.text = MonitoringPanelTitle;
            _titleLabel.width = width - 24f;
            SetExpandedContentVisible(false);
            _closeButton.isVisible = false;
            Show();
            BringToFront();
        }

        private void EndAutoScanMonitoring(bool showPanel)
        {
            _monitoringLayout = false;
            height = ExpandedPanelHeight;
            _titleLabel.text = NormalPanelTitle;
            _titleLabel.width = 466f;
            SetExpandedContentVisible(true);
            _closeButton.isVisible = true;
            Refresh();
            if (showPanel)
            {
                Show();
                BringToFront();
            }
            else
            {
                Hide();
            }
        }

        private void SetExpandedContentVisible(bool visible)
        {
            _statusLabel.isVisible = visible;
            _autoScanButton.isVisible = visible;
            _autoScanPreviewCheckboxRow.isVisible = visible;
            _rejectPreviewButton.isVisible = visible && PedestrianCrossingToolkitState.HasAutoScanPreviewPlan;
            _applyPreviewButton.isVisible = visible && PedestrianCrossingToolkitState.HasAutoScanPreviewPlan;
            _cancelPreviewButton.isVisible = visible && PedestrianCrossingToolkitState.HasAutoScanPreviewPlan;
        }

        private void OnRejectPreviewClicked(UIComponent component, UIMouseEventParameter p)
        {
            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
                return;

            if (!PedestrianCrossingToolkitState.HasAutoScanPreviewPlan)
            {
                Refresh();
                return;
            }

            ActivateMode(PedestrianToolMode.AutoScanReject);
        }

        private void OnApplyPreviewClicked(UIComponent component, UIMouseEventParameter p)
        {
            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
                return;

            CancelActiveTool();
            PedestrianCrossingToolkitState.ApplyAutoScanPreview();
        }

        private void OnCancelPreviewClicked(UIComponent component, UIMouseEventParameter p)
        {
            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
                return;

            CancelActiveTool();
            PedestrianCrossingToolkitState.CancelAutoScanPreview();
        }

        private static void OnClearAllConfirmed(UIComponent component, int result)
        {
            if (result != 1
                || PedestrianCrossingToolkitState.IsAutoScanObservationActive
                || PedestrianCrossingToolkitState.HasAutoScanPreviewPlan)
                return;

            PedestrianCrossingToolkitState.ClearPlacements();
        }

        private void OnPanelDragMouseDown(UIComponent component, UIMouseEventParameter p)
        {
            ShieldToolInput(true);
            _panelDragging = true;
            _panelDragStartMouse = p.position;
            _panelDragStartPosition = relativePosition;
            BringToFront();
        }

        private void OnPanelDragMouseMove(UIComponent component, UIMouseEventParameter p)
        {
            ShieldToolInput(_panelDragging || Input.GetMouseButton(0));
            if (!_panelDragging)
                return;

            Vector2 delta = p.position - _panelDragStartMouse;
            relativePosition = new Vector3(_panelDragStartPosition.x + delta.x, _panelDragStartPosition.y - delta.y);
            ClampToView();
        }

        private void OnPanelDragMouseUp(UIComponent component, UIMouseEventParameter p)
        {
            ShieldToolInput(false);
            if (!_panelDragging)
                return;

            _panelDragging = false;
            ClampToView();
        }

        private void CancelActiveTool()
        {
            _panelDragging = false;
            PedestrianCrossingToolkitState.SetActiveMode(PedestrianToolMode.None);
            if (ToolsModifierControl.toolController != null
                && ToolsModifierControl.toolController.CurrentTool is PedestrianCrossingInteractionTool)
            {
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }

        private void ClampToView()
        {
            UIView view = UIView.GetAView();
            if (view == null)
                return;

            float maxX = Mathf.Max(0f, view.fixedWidth - width);
            float maxY = Mathf.Max(0f, view.fixedHeight - height);
            relativePosition = new Vector3(
                Mathf.Clamp(relativePosition.x, 0f, maxX),
                Mathf.Clamp(relativePosition.y, 0f, maxY),
                relativePosition.z);
        }

        private void Refresh()
        {
            PedestrianToolMode mode = PedestrianCrossingToolkitState.ActiveMode;
            SetButtonState(_rejectPreviewButton, mode == PedestrianToolMode.AutoScanReject);

            _statusLabel.text = GetSelectedModeStatusText(mode);
            bool scanning = PedestrianCrossingToolkitState.IsAutoScanObservationActive;
            bool previewPending = PedestrianCrossingToolkitState.HasAutoScanPreviewPlan;
            bool actionEnabled = !scanning && !previewPending;
            _autoScanButton.isEnabled = PedestrianCrossingToolkitState.Enabled && actionEnabled;
            _autoScanButton.text = scanning ? "Scanning..." : "Auto\nScan";
            if (_autoScanPreviewCheckbox != null)
            {
                _autoScanPreviewCheckbox.isEnabled = !scanning && !previewPending;
                if (_autoScanPreviewCheckbox.isChecked != PedestrianCrossingToolkitState.AutoScanPreviewConfirmEnabled)
                    _autoScanPreviewCheckbox.isChecked = PedestrianCrossingToolkitState.AutoScanPreviewConfirmEnabled;
            }

            bool showPreviewControls = previewPending;
            _rejectPreviewButton.isVisible = showPreviewControls;
            _applyPreviewButton.isVisible = showPreviewControls;
            _cancelPreviewButton.isVisible = showPreviewControls;
            _rejectPreviewButton.isEnabled = !scanning
                                             && showPreviewControls
                                             && PedestrianCrossingToolkitState.AutoScanPreviewAcceptedCount > 0;
            _applyPreviewButton.isEnabled = !scanning
                                            && showPreviewControls
                                            && PedestrianCrossingToolkitState.AutoScanPreviewAcceptedCount > 0;
            _cancelPreviewButton.isEnabled = !scanning && showPreviewControls;
            _closeButton.isEnabled = !scanning;
        }

        private static string GetSelectedModeStatusText(PedestrianToolMode mode)
        {
            switch (mode)
            {
                case PedestrianToolMode.AutoScanReject:
                    if (!string.IsNullOrEmpty(PedestrianCrossingToolkitState.StatusMessage))
                        return PedestrianCrossingToolkitState.StatusMessage;

                    return "Reject Proposal: click a yellow Auto Scan marker to remove that suggested crossing.";
                default:
                    if (!string.IsNullOrEmpty(PedestrianCrossingToolkitState.StatusMessage)
                        && PedestrianCrossingToolkitState.StatusMessage != "No pedestrian crossing tool selected.")
                    {
                        return PedestrianCrossingToolkitState.StatusMessage;
                    }

                    return "Manage existing PCT crossings here, or open Roads > Crossing to place one.";
            }
        }

        private void SetButtonState(UIButton button, bool active)
        {
            button.normalBgSprite = active ? "ButtonMenuPressed" : "ButtonMenu";
            button.hoveredBgSprite = active ? "ButtonMenuPressed" : "ButtonMenuHovered";
        }

        private UILabel AddLabel(UIComponent parent, float x, float y, float labelWidth, float labelHeight)
        {
            UILabel label = parent.AddUIComponent<UILabel>();
            label.relativePosition = new Vector3(x, y);
            label.width = labelWidth;
            label.height = labelHeight;
            label.textScale = 0.68f;
            label.autoSize = false;
            label.wordWrap = true;
            label.autoHeight = false;
            return label;
        }

        private UIPanel AddCheckBox(UIComponent parent, string text, string tooltip, bool initial, Action<bool> onChanged, out UICheckBox checkbox)
        {
            UIPanel row = parent.AddUIComponent<UIPanel>();
            row.width = 260f;
            row.height = 24f;
            row.tooltip = tooltip;
            RegisterInputShield(row);

            checkbox = row.AddUIComponent<UICheckBox>();
            checkbox.width = 22f;
            checkbox.height = 22f;
            checkbox.relativePosition = new Vector3(0f, 1f);
            checkbox.tooltip = tooltip;
            RegisterInputShield(checkbox);

            UISprite uncheckedSprite = checkbox.AddUIComponent<UISprite>();
            uncheckedSprite.spriteName = "check-unchecked";
            uncheckedSprite.size = new Vector2(16f, 16f);
            uncheckedSprite.relativePosition = new Vector3(2f, 3f);
            RegisterInputShield(uncheckedSprite);

            UISprite checkedSprite = uncheckedSprite.AddUIComponent<UISprite>();
            checkedSprite.spriteName = "check-checked";
            checkedSprite.size = uncheckedSprite.size;
            checkedSprite.relativePosition = Vector3.zero;
            checkbox.checkedBoxObject = checkedSprite;
            checkbox.isChecked = initial;
            RegisterInputShield(checkedSprite);

            UILabel label = row.AddUIComponent<UILabel>();
            label.text = text;
            label.textScale = 0.66f;
            label.width = 220f;
            label.height = 22f;
            label.relativePosition = new Vector3(28f, 4f);
            label.tooltip = tooltip;
            label.wordWrap = false;
            RegisterInputShield(label);

            checkbox.eventCheckChanged += (c, value) => onChanged(value);
            return row;
        }

        private UIButton AddButton(UIComponent parent, string text, float x, float y, float buttonWidth, float buttonHeight, MouseEventHandler onClick)
        {
            UIButton button = parent.AddUIComponent<UIButton>();
            button.text = text;
            button.textScale = 0.64f;
            button.wordWrap = true;
            button.textHorizontalAlignment = UIHorizontalAlignment.Center;
            button.textVerticalAlignment = UIVerticalAlignment.Middle;
            button.textPadding = new RectOffset(3, 3, 2, 2);
            button.width = buttonWidth;
            button.height = buttonHeight;
            button.relativePosition = new Vector3(x, y);
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.disabledBgSprite = "ButtonMenuDisabled";
            RegisterInputShield(button);
            button.eventClick += onClick;
            return button;
        }

        private static void RegisterInputShield(UIComponent component)
        {
            if (component == null)
                return;

            component.eventMouseEnter += OnShieldMouseEvent;
            component.eventMouseMove += OnShieldMouseEvent;
            component.eventMouseDown += OnShieldMouseEvent;
            component.eventMouseUp += OnShieldMouseUp;
            component.eventMouseLeave += OnShieldMouseLeave;
        }

        private static void OnShieldMouseEvent(UIComponent component, UIMouseEventParameter p)
        {
            ShieldToolInput(Input.GetMouseButton(0) || Input.GetMouseButtonDown(0));
        }

        private static void OnShieldMouseUp(UIComponent component, UIMouseEventParameter p)
        {
            ShieldToolInput(false);
            _uiPointerCaptured = false;
        }

        private static void OnShieldMouseLeave(UIComponent component, UIMouseEventParameter p)
        {
            ShieldToolInput(Input.GetMouseButton(0));
        }

        private static void ShieldToolInput(bool capture)
        {
            _uiBlockUntilFrame = Mathf.Max(_uiBlockUntilFrame, Time.frameCount + 2);
            if (capture)
                _uiPointerCaptured = true;
        }

        private static bool IsMouseOverComponent(UIComponent component)
        {
            if (component == null || !component.isVisible)
                return false;

            if (component.containsMouse)
                return true;

            UIView view = UIView.GetAView();
            if (view == null || Screen.width <= 0 || Screen.height <= 0)
                return false;

            Vector3 position = component.absolutePosition;
            Vector2 mouse = Input.mousePosition;
            float rawX = mouse.x;
            float rawY = Screen.height - mouse.y;
            float uiX = mouse.x * (view.fixedWidth / Screen.width);
            float uiY = (Screen.height - mouse.y) * (view.fixedHeight / Screen.height);

            if (ContainsPoint(rawX, rawY, position.x, position.y, component.width, component.height))
                return true;

            return ContainsPoint(uiX, uiY, position.x, position.y, component.width, component.height);
        }

        private static bool ContainsPoint(float pointX, float pointY, float left, float top, float width, float height)
        {
            return pointX >= left - UiShieldPadding
                   && pointX <= left + width + UiShieldPadding
                   && pointY >= top - UiShieldPadding
                   && pointY <= top + height + UiShieldPadding;
        }

        private static bool IsMouseOverToolkitComponent()
        {
            return IsMouseOverComponent(Instance)
                   || IsMouseOverComponent(PedestrianCrossingAutoScanInstructionsPanel.Instance)
                   || IsMouseOverComponent(PedestrianCrossingToolkitLauncherButton.Instance)
                   || IsMouseOverComponent(UnifiedTransitLauncherToolbar.Current)
                   || PedestrianCrossingRoadsTab.IsMouseOverUi();
        }

        private static bool IsToolkitComponentOrChild(UIComponent component)
        {
            while (component != null)
            {
                if (component == Instance
                    || component == PedestrianCrossingAutoScanInstructionsPanel.Instance
                    || component == PedestrianCrossingToolkitLauncherButton.Instance
                    || component == UnifiedTransitLauncherToolbar.Current
                    || PedestrianCrossingRoadsTab.IsToolkitComponentOrChild(component))
                    return true;

                component = component.parent;
            }

            return false;
        }
    }
}
