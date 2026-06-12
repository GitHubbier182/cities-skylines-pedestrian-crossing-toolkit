using System;
using ColossalFramework.UI;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public class PedestrianCrossingToolkitPanel : UIPanel
    {
        public static PedestrianCrossingToolkitPanel Instance;

        private const float UiShieldPadding = 8f;
        private const float TopHudBlockHeight = 96f;
        private const float BottomHudBlockHeight = 128f;
        private static bool _uiPointerCaptured;
        private static int _uiBlockUntilFrame;
        private static int _ignoreRightClickCloseFrame = -1;

        private UILabel _statusLabel;
        private UIButton _midBlockButton;
        private UIButton _signalButton;
        private UIButton _subwayButton;
        private UIButton _subwayPointButton;
        private UIButton _bridgeButton;
        private UIButton _removeButton;
        private UIButton _clearButton;
        private UIButton _validateButton;
        private UIButton _autoScanButton;
        private UIButton _infoButton;
        private bool _panelDragging;
        private Vector2 _panelDragStartMouse;
        private Vector3 _panelDragStartPosition;

        public static bool IsOpen
        {
            get { return Instance != null && Instance.isVisible; }
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
            const float panelHeight = 218f;
            const float margin = 16f;
            const float contentWidth = panelWidth - (margin * 2f);

            Instance = this;
            name = "PedestrianCrossingToolkitPanel";
            width = panelWidth;
            height = panelHeight;
            backgroundSprite = "MenuPanel2";
            color = new Color32(40, 48, 56, 245);
            canFocus = true;
            isInteractive = true;
            relativePosition = new Vector3(170f, 140f);
            RegisterInputShield(this);

            UIPanel titleBar = AddUIComponent<UIPanel>();
            titleBar.width = width;
            titleBar.height = 32;
            titleBar.relativePosition = Vector3.zero;
            titleBar.backgroundSprite = "MenuPanel";
            titleBar.isInteractive = true;
            RegisterInputShield(titleBar);

            UILabel title = titleBar.AddUIComponent<UILabel>();
            title.text = "Pedestrian Crossing Toolkit";
            title.textScale = 0.9f;
            title.autoSize = false;
            title.autoHeight = false;
            title.width = 408f;
            title.height = 22f;
            title.relativePosition = new Vector3(12f, 8f);
            title.isInteractive = true;
            RegisterInputShield(title);

            titleBar.eventMouseDown += OnPanelDragMouseDown;
            titleBar.eventMouseMove += OnPanelDragMouseMove;
            titleBar.eventMouseUp += OnPanelDragMouseUp;
            title.eventMouseDown += OnPanelDragMouseDown;
            title.eventMouseMove += OnPanelDragMouseMove;
            title.eventMouseUp += OnPanelDragMouseUp;

            _infoButton = AddButton(titleBar, "Info", 432f, 5f, 48f, 22f, OnInfoClicked);
            _infoButton.textScale = 0.58f;
            _infoButton.tooltip = "Copy debug info for support.";

            UIButton close = AddButton(titleBar, "x", 486f, 5f, 24f, 22f, OnCloseClicked);
            close.tooltip = "Close";

            _midBlockButton = AddButton(this, "Standard\nCrossing", margin, 48f, 92f, 42f, (c, p) => ActivateMode(PedestrianToolMode.MidBlockCrossing));
            _midBlockButton.tooltip = "Place a simple crossing.";

            _signalButton = AddButton(this, "Signalled\nCrossing", 116f, 48f, 104f, 42f, (c, p) => ActivateMode(PedestrianToolMode.SignalCrossing));
            _signalButton.tooltip = "Place a signal-controlled crossing.";

            _subwayButton = AddButton(this, "Auto\nSubway", 228f, 48f, 84f, 42f, (c, p) => ActivateMode(PedestrianToolMode.SubwayLink));
            _subwayButton.tooltip = "Place a compact subway link.";

            _subwayPointButton = AddButton(this, "Manual\nSubway", 320f, 48f, 96f, 42f, (c, p) => ActivateMode(PedestrianToolMode.SubwayPointToPoint));
            _subwayPointButton.tooltip = "Place a subway between two selected entrances.";

            _bridgeButton = AddButton(this, "Bridge", 424f, 48f, 80f, 42f, (c, p) => ActivateMode(PedestrianToolMode.PedestrianBridge));
            _bridgeButton.tooltip = "Place a pedestrian bridge.";

            _statusLabel = AddLabel(this, margin, 100f, contentWidth, 42f);

            _removeButton = AddButton(this, "Remove A\nCrossing", margin, 154f, 116f, 42f, (c, p) => ActivateMode(PedestrianToolMode.RemoveCrossing));
            _removeButton.tooltip = "Remove a crossing by clicking its location.";

            _validateButton = AddButton(this, "Validate\nCrossings", 140f, 154f, 116f, 42f, OnValidateClicked);
            _validateButton.tooltip = "Run a one-time health check for placed toolkit crossings.";

            _clearButton = AddButton(this, "Clear All\nCrossings", 264f, 154f, 116f, 42f, OnClearClicked);
            _clearButton.tooltip = "Remove all crossings from this city.";

            _autoScanButton = AddButton(this, "Auto\nScan", 388f, 154f, 116f, 42f, OnAutoScanClicked);
            _autoScanButton.tooltip = "Run a one-time scan for pedestrian-caused traffic hotspots.";

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

            if (isVisible
                && Input.GetMouseButtonDown(1)
                && PedestrianCrossingToolkitState.ActiveMode == PedestrianToolMode.None
                && _ignoreRightClickCloseFrame != Time.frameCount)
            {
                Hide();
                _uiPointerCaptured = false;
                Debug.Log("[PedestrianCrossingToolkit] Toolkit closed: right-click with no active crossing tool.");
                return;
            }

            if (!isVisible || !Input.GetMouseButtonDown(0))
                return;

            if (!IsMouseOverExternalBlockingUi())
                return;

            CloseForExternalUiSelection();
            Debug.Log("[PedestrianCrossingToolkit] Toolkit closed: external UI selected while panel was open.");
        }

        public static void CreateIfNeeded(UIView view)
        {
            if (view == null || Instance != null)
                return;

            view.AddUIComponent(typeof(PedestrianCrossingToolkitPanel));
        }

        public static void DestroyInstance()
        {
            if (Instance == null)
                return;

            UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        public static void Toggle()
        {
            if (Instance == null)
                return;

            if (Instance.isVisible)
            {
                Instance.CancelActiveTool();
                Instance.Hide();
            }
            else
            {
                Instance.Show();
            }
        }

        public static void RefreshInstance()
        {
            if (Instance != null)
                Instance.Refresh();
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
            Instance.Hide();
            _uiPointerCaptured = false;
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

        private void ActivateMode(PedestrianToolMode mode)
        {
            if (PedestrianCrossingToolkitState.IsAutoScanObservationActive)
            {
                Refresh();
                return;
            }

            PedestrianCrossingToolkitState.SetActiveMode(mode);
            PedestrianCrossingInteractionTool tool = PedestrianCrossingInteractionTool.EnsureOnToolController();
            if (tool != null && ToolsModifierControl.toolController != null)
            {
                ToolsModifierControl.toolController.CurrentTool = tool;
                Debug.Log("[PedestrianCrossingToolkit] Interaction tool activated for mode: " + mode);
            }

            Refresh();
        }

        private void OnCloseClicked(UIComponent component, UIMouseEventParameter p)
        {
            CancelActiveTool();
            Hide();
        }

        private void OnClearClicked(UIComponent component, UIMouseEventParameter p)
        {
            ConfirmPanel.ShowModal(
                "Clear All Crossings",
                "This removes all crossings in your city. Are you sure?",
                OnClearAllConfirmed);
        }

        private void OnAutoScanClicked(UIComponent component, UIMouseEventParameter p)
        {
            CancelActiveTool();
            if (PedestrianCrossingToolkitState.BeginAutoScanObservation())
                Debug.Log("[PedestrianCrossingToolkit] Auto scan button started observation.");
        }

        private void OnValidateClicked(UIComponent component, UIMouseEventParameter p)
        {
            CancelActiveTool();
            PedestrianCrossingToolkitState.ValidateCrossings();
        }

        private void OnInfoClicked(UIComponent component, UIMouseEventParameter p)
        {
            CancelActiveTool();
            string report = PedestrianCrossingToolkitState.BuildUserInfoReport();
            bool copied = TryCopyToClipboard(report);
            PedestrianCrossingLog.Info("User info report requested:\n" + report);
            PedestrianCrossingToolkitState.ShowUserInfoStatus(copied);
        }

        private void OnClearAllConfirmed(UIComponent component, int result)
        {
            if (result != 1)
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
            SetButtonState(_midBlockButton, mode == PedestrianToolMode.MidBlockCrossing);
            SetButtonState(_signalButton, mode == PedestrianToolMode.SignalCrossing);
            SetButtonState(_subwayButton, mode == PedestrianToolMode.SubwayLink);
            SetButtonState(_subwayPointButton, mode == PedestrianToolMode.SubwayPointToPoint);
            SetButtonState(_bridgeButton, mode == PedestrianToolMode.PedestrianBridge);
            SetButtonState(_removeButton, mode == PedestrianToolMode.RemoveCrossing);

            _statusLabel.text = GetSelectedModeStatusText(mode);
            bool hasPendingAssets = CrossingPlacementRegistry.Count > 0;
            bool scanning = PedestrianCrossingToolkitState.IsAutoScanObservationActive;
            _midBlockButton.isEnabled = !scanning;
            _signalButton.isEnabled = !scanning;
            _subwayButton.isEnabled = !scanning;
            _subwayPointButton.isEnabled = !scanning;
            _bridgeButton.isEnabled = !scanning;
            _removeButton.isEnabled = !scanning;
            _validateButton.isEnabled = hasPendingAssets && !scanning;
            _clearButton.isEnabled = hasPendingAssets && !scanning;
            _autoScanButton.isEnabled = PedestrianCrossingToolkitState.Enabled && !scanning;
            _autoScanButton.text = scanning ? "Scanning..." : "Auto\nScan";
            _infoButton.isEnabled = PedestrianCrossingToolkitState.Enabled;
        }

        private static string GetSelectedModeStatusText(PedestrianToolMode mode)
        {
            switch (mode)
            {
                case PedestrianToolMode.MidBlockCrossing:
                    return "Standard Crossing: place a zebra crossing on a valid road segment or join.";
                case PedestrianToolMode.SignalCrossing:
                    return "Signalled Crossing: add vanilla signal control and Pedestrian Crossing Toolkit stop lines.";
                case PedestrianToolMode.SubwayLink:
                    return "Auto Subway: create a compact subway crossing across a valid road, rail, or metro target.";
                case PedestrianToolMode.SubwayPointToPoint:
                    return "Manual Subway: pick a start entrance, then an end entrance within range.";
                case PedestrianToolMode.PedestrianBridge:
                    return "Bridge: place a pedestrian bridge across a valid road, rail, or metro target.";
                case PedestrianToolMode.RemoveCrossing:
                    return "Remove A Crossing: click an existing Pedestrian Crossing Toolkit crossing to remove its owned assets.";
                default:
                    if (!string.IsNullOrEmpty(PedestrianCrossingToolkitState.StatusMessage)
                        && PedestrianCrossingToolkitState.StatusMessage != "No pedestrian crossing tool selected.")
                    {
                        return PedestrianCrossingToolkitState.StatusMessage;
                    }

                    return "Select a crossing tool. Right-click closes this panel when no tool is selected.";
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

        private static bool TryCopyToClipboard(string text)
        {
            try
            {
                GUIUtility.systemCopyBuffer = text ?? string.Empty;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PedestrianCrossingToolkit] Clipboard copy failed: " + e.Message);
                return false;
            }
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
                   || IsMouseOverComponent(PedestrianCrossingToolkitLauncherButton.Instance)
                   || IsMouseOverComponent(UnifiedTransitLauncherToolbar.Current);
        }

        private static bool IsToolkitComponentOrChild(UIComponent component)
        {
            while (component != null)
            {
                if (component == Instance
                    || component == PedestrianCrossingToolkitLauncherButton.Instance
                    || component == UnifiedTransitLauncherToolbar.Current)
                    return true;

                component = component.parent;
            }

            return false;
        }
    }
}
