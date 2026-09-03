using ColossalFramework.UI;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    internal sealed class PedestrianCrossingRoadsHoverPreview : UIPanel
    {
        private const float PanelWidth = 340f;
        private const float PanelHeight = 150f;

        private static PedestrianCrossingRoadsHoverPreview Instance;

        private UISprite _previewImage;
        private UILabel _title;
        private UILabel _description;
        private UILabel _hint;
        private bool _started;
        private bool _showWhenStarted;
        private UIButton _pendingAnchor;
        private CrossingRoadsIconKind _pendingIconKind;
        private string _pendingTitle;
        private string _pendingDescription;
        private string _pendingHint;

        internal static void ShowFor(
            UIButton anchor,
            CrossingRoadsIconKind iconKind,
            string title,
            string description,
            string hint)
        {
            UIView view = UIView.GetAView();
            if (view == null || anchor == null)
                return;

            if (Instance == null)
            {
                Instance = view.AddUIComponent(
                    typeof(PedestrianCrossingRoadsHoverPreview))
                    as PedestrianCrossingRoadsHoverPreview;
            }

            if (Instance == null)
                return;

            Instance._pendingAnchor = anchor;
            Instance._pendingIconKind = iconKind;
            Instance._pendingTitle = title;
            Instance._pendingDescription = description;
            Instance._pendingHint = hint;
            Instance._showWhenStarted = true;
            if (Instance._started)
                Instance.ShowPending(view);
        }

        internal static void HideInstance()
        {
            if (Instance != null)
            {
                Instance._showWhenStarted = false;
                Instance.Hide();
            }
        }

        internal static void DestroyInstance()
        {
            if (Instance == null)
                return;

            Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        public override void Start()
        {
            base.Start();

            Instance = this;
            name = "PedestrianCrossingToolkitRoadsHoverPreview";
            width = PanelWidth;
            height = PanelHeight;
            backgroundSprite = "MenuPanel2";
            color = new Color32(34, 42, 50, 252);
            isInteractive = false;

            UIPanel imageFrame = AddUIComponent<UIPanel>();
            imageFrame.width = 116f;
            imageFrame.height = 116f;
            imageFrame.relativePosition = new Vector3(12f, 12f);
            imageFrame.backgroundSprite = "GenericPanel";
            imageFrame.color = new Color32(18, 24, 30, 255);
            imageFrame.isInteractive = false;

            _previewImage = imageFrame.AddUIComponent<UISprite>();
            _previewImage.width = 96f;
            _previewImage.height = 96f;
            _previewImage.relativePosition = new Vector3(10f, 10f);
            _previewImage.isInteractive = false;

            _title = AddLabel(142f, 16f, 184f, 28f, 0.82f);
            _title.textColor = new Color32(240, 245, 247, 255);

            _description = AddLabel(142f, 48f, 184f, 54f, 0.62f);
            _description.wordWrap = true;
            _description.textColor = new Color32(190, 220, 240, 255);

            _hint = AddLabel(142f, 110f, 184f, 26f, 0.56f);
            _hint.wordWrap = true;
            _hint.textColor = new Color32(246, 211, 112, 255);

            _started = true;
            if (_showWhenStarted)
                ShowPending(UIView.GetAView());
            else
                Hide();
        }

        public override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            base.OnDestroy();
        }

        private void SetContent(
            CrossingRoadsIconKind iconKind,
            string title,
            string description,
            string hint)
        {
            if (_previewImage != null)
            {
                // Generated icon art is the intentional placeholder until the
                // player-supplied preview images are added under PCT-UI-03.
                _previewImage.atlas = CrossingRoadsIconFactory.GetAtlas(iconKind);
                _previewImage.spriteName = CrossingRoadsIconFactory.GetSpriteName(iconKind);
                _previewImage.isVisible = _previewImage.atlas != null;
            }

            if (_title != null)
                _title.text = title ?? string.Empty;
            if (_description != null)
                _description.text = description ?? string.Empty;
            if (_hint != null)
                _hint.text = hint ?? string.Empty;
        }

        private void ShowPending(UIView view)
        {
            if (view == null || _pendingAnchor == null)
                return;

            SetContent(
                _pendingIconKind,
                _pendingTitle,
                _pendingDescription,
                _pendingHint);
            PositionBy(_pendingAnchor, view);
            Show();
            BringToFront();
        }

        private void PositionBy(UIButton anchor, UIView view)
        {
            Vector3 anchorPosition = anchor.absolutePosition;
            float x = anchorPosition.x + ((anchor.width - width) * 0.5f);
            float y = anchorPosition.y - height - 10f;
            if (y < 8f)
                y = anchorPosition.y + anchor.height + 10f;

            relativePosition = new Vector3(
                Mathf.Clamp(x, 8f, Mathf.Max(8f, view.fixedWidth - width - 8f)),
                Mathf.Clamp(y, 8f, Mathf.Max(8f, view.fixedHeight - height - 8f)));
        }

        private UILabel AddLabel(
            float x,
            float y,
            float labelWidth,
            float labelHeight,
            float textScale)
        {
            UILabel label = AddUIComponent<UILabel>();
            label.autoSize = false;
            label.autoHeight = false;
            label.width = labelWidth;
            label.height = labelHeight;
            label.textScale = textScale;
            label.relativePosition = new Vector3(x, y);
            label.isInteractive = false;
            return label;
        }
    }
}
