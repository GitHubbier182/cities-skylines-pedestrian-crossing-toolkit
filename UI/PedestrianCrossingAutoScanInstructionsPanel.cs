using ColossalFramework.UI;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    public class PedestrianCrossingAutoScanInstructionsPanel : UIPanel
    {
        public static PedestrianCrossingAutoScanInstructionsPanel Instance;

        private const float PanelWidth = 464f;
        private const float PanelHeight = 356f;
        private const string StandardSpriteName = "pct-preview-standard";
        private const string SignalSpriteName = "pct-preview-signal";
        private const string SubwaySpriteName = "pct-preview-subway";
        private const string BridgeSpriteName = "pct-preview-bridge";
        private static UITextureAtlas _standardIconAtlas;
        private static UITextureAtlas _signalIconAtlas;
        private static UITextureAtlas _subwayIconAtlas;
        private static UITextureAtlas _bridgeIconAtlas;
        private static bool _pendingShow;

        private UIButton _rejectButton;
        private UIButton _applyButton;
        private UIButton _cancelButton;
        private bool _started;

        public static void CreateIfNeeded(UIView view)
        {
            if (view == null || Instance != null)
                return;

            Instance = view.AddUIComponent(typeof(PedestrianCrossingAutoScanInstructionsPanel)) as PedestrianCrossingAutoScanInstructionsPanel;
        }

        public static void ShowIfNeeded()
        {
            if (!PedestrianCrossingToolkitState.HasAutoScanPreviewPlan)
                return;

            UIView view = UIView.GetAView();
            if (view == null)
                return;

            _pendingShow = true;
            CreateIfNeeded(view);
            if (Instance == null)
                return;

            if (Instance._started && _pendingShow)
                Instance.ShowNow(view);
        }

        public static void HideInstance()
        {
            _pendingShow = false;
            if (Instance != null)
                Instance.Hide();
        }

        public static void DestroyInstance()
        {
            if (Instance == null)
                return;

            _pendingShow = false;
            UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        public override void Start()
        {
            base.Start();

            Instance = this;
            name = "PedestrianCrossingToolkitAutoScanInstructions";
            width = PanelWidth;
            height = PanelHeight;
            backgroundSprite = "MenuPanel2";
            color = new Color32(36, 44, 52, 248);
            canFocus = true;
            isInteractive = true;
            RegisterInputShield(this);

            UIPanel titleBar = AddUIComponent<UIPanel>();
            titleBar.width = width;
            titleBar.height = 32f;
            titleBar.relativePosition = Vector3.zero;
            titleBar.backgroundSprite = "MenuPanel";
            titleBar.isInteractive = true;
            RegisterInputShield(titleBar);

            UILabel title = AddLabel(titleBar, "Auto Scan Preview", 12f, 8f, PanelWidth - 84f, 20f, 0.86f);
            title.isInteractive = true;

            UIButton close = AddButton(titleBar, "x", PanelWidth - 36f, 5f, 24f, 22f, OnCloseClicked);
            close.tooltip = "Close";

            UIPanel titleJoin = AddUIComponent<UIPanel>();
            titleJoin.width = PanelWidth - 4f;
            titleJoin.height = 8f;
            titleJoin.relativePosition = new Vector3(2f, 29f);
            titleJoin.backgroundSprite = "GenericPanel";
            titleJoin.color = new Color32(36, 44, 52, 248);
            RegisterInputShield(titleJoin);

            UILabel intro = AddLabel(
                this,
                "Yellow markers are suggested crossings. Review them before creating anything.",
                18f,
                46f,
                PanelWidth - 36f,
                38f,
                0.62f);
            intro.wordWrap = true;

            AddIconPreview(PedestrianToolMode.MidBlockCrossing, "Standard", 22f);
            AddIconPreview(PedestrianToolMode.SignalCrossing, "Signal", 136f);
            AddIconPreview(PedestrianToolMode.SubwayLink, "Subway", 250f);
            AddIconPreview(PedestrianToolMode.PedestrianBridge, "Bridge", 364f);

            _rejectButton = AddActionRow(
                "Reject\nProposal",
                "Select this, then click a yellow marker to reject that suggestion.",
                18f,
                196f,
                OnRejectClicked);
            _applyButton = AddActionRow(
                "Apply\nPreview",
                "Build every suggestion that is still accepted.",
                18f,
                238f,
                OnApplyClicked);
            _cancelButton = AddActionRow(
                "Cancel\nPreview",
                "Discard the staged scan without changing crossings.",
                18f,
                280f,
                OnCancelClicked);

            _started = true;
            if (_pendingShow)
                ShowNow(UIView.GetAView());
            else
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

            bool pending = PedestrianCrossingToolkitState.HasAutoScanPreviewPlan;
            if (isVisible && !pending)
            {
                Hide();
                return;
            }

            int accepted = PedestrianCrossingToolkitState.AutoScanPreviewAcceptedCount;
            if (_rejectButton != null)
                _rejectButton.isEnabled = pending && accepted > 0;
            if (_applyButton != null)
                _applyButton.isEnabled = pending && accepted > 0;
            if (_cancelButton != null)
                _cancelButton.isEnabled = pending;
        }

        private void ShowNow(UIView view)
        {
            if (view == null)
                view = UIView.GetAView();
            if (view == null)
                return;

            _pendingShow = false;
            PositionPanel(view);
            Show();
            BringToFront();
            PedestrianCrossingLog.Advanced("[PedestrianCrossingToolkit] Auto Scan preview controls shown.");
        }

        private void PositionPanel(UIView view)
        {
            float x = Mathf.Max(16f, (view.fixedWidth - width) * 0.5f);
            float y = Mathf.Max(80f, (view.fixedHeight - height) * 0.5f);

            relativePosition = new Vector3(
                Mathf.Clamp(x, 8f, Mathf.Max(8f, view.fixedWidth - width - 8f)),
                Mathf.Clamp(y, 8f, Mathf.Max(8f, view.fixedHeight - height - 8f)),
                0f);
        }

        private void AddIconPreview(PedestrianToolMode mode, string labelText, float x)
        {
            UIPanel border = AddUIComponent<UIPanel>();
            border.width = 72f;
            border.height = 72f;
            border.relativePosition = new Vector3(x, 92f);
            border.backgroundSprite = "GenericPanel";
            border.color = new Color32(255, 204, 32, 255);
            RegisterInputShield(border);

            UIPanel inner = border.AddUIComponent<UIPanel>();
            inner.width = 66f;
            inner.height = 66f;
            inner.relativePosition = new Vector3(3f, 3f);
            inner.backgroundSprite = "GenericPanel";
            inner.color = new Color32(14, 19, 24, 245);
            RegisterInputShield(inner);

            UITextureAtlas atlas = GetOrCreateIconAtlas(mode);
            if (atlas != null)
            {
                UISprite icon = inner.AddUIComponent<UISprite>();
                icon.atlas = atlas;
                icon.spriteName = GetIconSpriteName(mode);
                icon.width = 48f;
                icon.height = 48f;
                icon.relativePosition = new Vector3(9f, 5f);
                icon.isInteractive = false;
            }
            else
            {
                UILabel fallback = AddLabel(inner, labelText.Substring(0, 1), 0f, 10f, 66f, 30f, 1.2f);
                fallback.textAlignment = UIHorizontalAlignment.Center;
            }

            UILabel label = AddLabel(this, labelText, x - 4f, 166f, 80f, 18f, 0.58f);
            label.textAlignment = UIHorizontalAlignment.Center;
        }

        private UIButton AddActionRow(
            string buttonText,
            string description,
            float x,
            float y,
            MouseEventHandler onClick)
        {
            UIButton button = AddButton(this, buttonText, x, y, 116f, 34f, onClick);

            UILabel label = AddLabel(this, description, x + 136f, y + 2f, PanelWidth - x - 154f, 34f, 0.57f);
            label.wordWrap = true;
            return button;
        }

        private UILabel AddLabel(UIComponent parent, string text, float x, float y, float labelWidth, float labelHeight, float scale)
        {
            UILabel label = parent.AddUIComponent<UILabel>();
            label.text = text;
            label.textScale = scale;
            label.autoSize = false;
            label.autoHeight = false;
            label.width = labelWidth;
            label.height = labelHeight;
            label.relativePosition = new Vector3(x, y);
            RegisterInputShield(label);
            return label;
        }

        private UIButton AddButton(UIComponent parent, string text, float x, float y, float buttonWidth, float buttonHeight, MouseEventHandler onClick)
        {
            UIButton button = parent.AddUIComponent<UIButton>();
            button.text = text;
            button.textScale = 0.58f;
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
            if (onClick != null)
                button.eventClick += onClick;
            return button;
        }

        private void OnCloseClicked(UIComponent component, UIMouseEventParameter p)
        {
            Hide();
        }

        private void OnRejectClicked(UIComponent component, UIMouseEventParameter p)
        {
            if (!PedestrianCrossingToolkitState.HasAutoScanPreviewPlan)
                return;

            PedestrianCrossingToolkitPanel.NotifyToolkitUiInput(false);
            PedestrianCrossingToolkitPanel.ActivateModeFromUi(PedestrianToolMode.AutoScanReject);
        }

        private void OnApplyClicked(UIComponent component, UIMouseEventParameter p)
        {
            if (!PedestrianCrossingToolkitState.HasAutoScanPreviewPlan)
                return;

            CancelActiveTool();
            PedestrianCrossingToolkitState.ApplyAutoScanPreview();
            Hide();
        }

        private void OnCancelClicked(UIComponent component, UIMouseEventParameter p)
        {
            if (!PedestrianCrossingToolkitState.HasAutoScanPreviewPlan)
                return;

            CancelActiveTool();
            PedestrianCrossingToolkitState.CancelAutoScanPreview();
            Hide();
        }

        private static void CancelActiveTool()
        {
            PedestrianCrossingToolkitPanel.NotifyToolkitUiInput(false);
            PedestrianCrossingToolkitState.SetActiveMode(PedestrianToolMode.None);
            if (ToolsModifierControl.toolController != null
                && ToolsModifierControl.toolController.CurrentTool is PedestrianCrossingInteractionTool)
            {
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }

        private static UITextureAtlas GetOrCreateIconAtlas(PedestrianToolMode mode)
        {
            UITextureAtlas existing = GetCachedIconAtlas(mode);
            if (existing != null)
                return existing;

            UIView view = UIView.GetAView();
            if (view == null || view.defaultAtlas == null || view.defaultAtlas.material == null)
                return null;

            Texture2D texture = CrossingAppliedOverlay.GetAutoScanPreviewIconTextureForUi(mode);
            if (texture == null)
                return null;

            Material material = new Material(view.defaultAtlas.material);
            material.mainTexture = texture;
            UITextureAtlas atlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            atlas.name = "PCTAutoScanPreviewInstructionAtlas" + mode;
            atlas.material = material;
            atlas.AddSprite(new UITextureAtlas.SpriteInfo
            {
                name = GetIconSpriteName(mode),
                texture = texture,
                region = new Rect(0f, 0f, 1f, 1f),
                border = new RectOffset()
            });

            SetCachedIconAtlas(mode, atlas);
            return atlas;
        }

        private static UITextureAtlas GetCachedIconAtlas(PedestrianToolMode mode)
        {
            switch (mode)
            {
                case PedestrianToolMode.SignalCrossing:
                    return _signalIconAtlas;
                case PedestrianToolMode.SubwayLink:
                    return _subwayIconAtlas;
                case PedestrianToolMode.PedestrianBridge:
                    return _bridgeIconAtlas;
                case PedestrianToolMode.MidBlockCrossing:
                default:
                    return _standardIconAtlas;
            }
        }

        private static void SetCachedIconAtlas(PedestrianToolMode mode, UITextureAtlas atlas)
        {
            switch (mode)
            {
                case PedestrianToolMode.SignalCrossing:
                    _signalIconAtlas = atlas;
                    break;
                case PedestrianToolMode.SubwayLink:
                    _subwayIconAtlas = atlas;
                    break;
                case PedestrianToolMode.PedestrianBridge:
                    _bridgeIconAtlas = atlas;
                    break;
                case PedestrianToolMode.MidBlockCrossing:
                default:
                    _standardIconAtlas = atlas;
                    break;
            }
        }

        private static string GetIconSpriteName(PedestrianToolMode mode)
        {
            switch (mode)
            {
                case PedestrianToolMode.SignalCrossing:
                    return SignalSpriteName;
                case PedestrianToolMode.SubwayLink:
                    return SubwaySpriteName;
                case PedestrianToolMode.PedestrianBridge:
                    return BridgeSpriteName;
                case PedestrianToolMode.MidBlockCrossing:
                default:
                    return StandardSpriteName;
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
            PedestrianCrossingToolkitPanel.NotifyToolkitUiInput(Input.GetMouseButton(0) || Input.GetMouseButtonDown(0));
        }

        private static void OnShieldMouseUp(UIComponent component, UIMouseEventParameter p)
        {
            PedestrianCrossingToolkitPanel.NotifyToolkitUiInput(false);
        }

        private static void OnShieldMouseLeave(UIComponent component, UIMouseEventParameter p)
        {
            PedestrianCrossingToolkitPanel.NotifyToolkitUiInput(Input.GetMouseButton(0));
        }
    }
}
