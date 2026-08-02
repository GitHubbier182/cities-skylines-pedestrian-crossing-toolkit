using ColossalFramework.UI;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    internal sealed class PedestrianCrossingAutoScanProgressPanel : UIPanel
    {
        private const float PanelWidth = 470f;
        private const float PanelHeight = 132f;

        private static PedestrianCrossingAutoScanProgressPanel Instance;

        private UILabel _messageLabel;
        private UILabel _progressLabel;
        private string _messageText = "Preparing Auto Scan";
        private int _progressPercent;
        private string _progressDetail = "Preparing...";
        private bool _showWhenStarted;
        private bool _started;

        public static bool ShowProgress(
            string message,
            int progressPercent,
            string progressDetail)
        {
            UIView view = UIView.GetAView();
            if (view == null)
                return false;

            if (Instance == null)
            {
                Instance = view.AddUIComponent(
                    typeof(PedestrianCrossingAutoScanProgressPanel))
                    as PedestrianCrossingAutoScanProgressPanel;
            }

            if (Instance == null)
                return false;

            Instance.SetMessage(message);
            Instance.SetProgress(progressPercent, progressDetail);
            if (Instance._started)
                Instance.ShowNow(view);
            else
                Instance._showWhenStarted = true;

            return true;
        }

        public static void UpdateProgress(
            string message,
            int progressPercent,
            string progressDetail)
        {
            // A completed scan hides but deliberately retains this panel. Route
            // every later update through ShowProgress so repeat scans make the
            // retained instance visible again instead of running silently.
            ShowProgress(message, progressPercent, progressDetail);
        }

        public static void HidePanel()
        {
            if (Instance == null)
                return;

            Instance._showWhenStarted = false;
            Instance.Hide();
        }

        public static void DestroyInstance()
        {
            if (Instance == null)
                return;

            UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        public override void Start()
        {
            base.Start();

            Instance = this;
            name = "PedestrianCrossingToolkitAutoScanProgressPanel";
            width = PanelWidth;
            height = PanelHeight;
            backgroundSprite = "MenuPanel2";
            color = new Color32(36, 44, 52, 248);
            canFocus = true;
            isInteractive = true;

            UILabel title = AddLabel(
                this,
                "PCT Auto Scan",
                18f,
                16f,
                PanelWidth - 36f,
                24f,
                0.78f);
            title.wordWrap = false;

            _messageLabel = AddLabel(
                this,
                string.Empty,
                18f,
                49f,
                PanelWidth - 36f,
                38f,
                0.68f);
            _messageLabel.wordWrap = true;
            _messageLabel.textColor = new Color32(190, 220, 240, 255);

            _progressLabel = AddLabel(
                this,
                string.Empty,
                18f,
                96f,
                PanelWidth - 36f,
                22f,
                0.72f);
            _progressLabel.wordWrap = false;
            _progressLabel.textColor = new Color32(246, 211, 112, 255);
            SetMessage(_messageText);
            SetProgress(_progressPercent, _progressDetail);

            _started = true;
            if (_showWhenStarted)
            {
                _showWhenStarted = false;
                UIView view = UIView.GetAView();
                if (view != null)
                    ShowNow(view);
                else
                    Hide();
            }
            else
            {
                Hide();
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            base.OnDestroy();
        }

        private void SetMessage(string message)
        {
            _messageText = string.IsNullOrEmpty(message)
                ? "Monitoring pedestrian movement"
                : message;
            if (_messageLabel != null)
                _messageLabel.text = _messageText;
        }

        private void SetProgress(int progressPercent, string progressDetail)
        {
            _progressPercent = Mathf.Clamp(progressPercent, 0, 100);
            _progressDetail = string.IsNullOrEmpty(progressDetail)
                ? "Preparing..."
                : progressDetail;
            if (_progressLabel != null)
            {
                _progressLabel.text = _progressPercent
                                      + "% complete  •  "
                                      + _progressDetail;
            }
        }

        private void ShowNow(UIView view)
        {
            SetMessage(_messageText);
            SetProgress(_progressPercent, _progressDetail);
            relativePosition = new Vector3(
                Mathf.Clamp(
                    (view.fixedWidth - width) * 0.5f,
                    8f,
                    Mathf.Max(8f, view.fixedWidth - width - 8f)),
                Mathf.Clamp(
                    (view.fixedHeight - height) * 0.42f,
                    8f,
                    Mathf.Max(8f, view.fixedHeight - height - 8f)),
                0f);
            Show();
            BringToFront();
        }

        private static UILabel AddLabel(
            UIComponent parent,
            string text,
            float x,
            float y,
            float labelWidth,
            float labelHeight,
            float scale)
        {
            UILabel label = parent.AddUIComponent<UILabel>();
            label.text = text;
            label.textScale = scale;
            label.autoSize = false;
            label.autoHeight = false;
            label.width = labelWidth;
            label.height = labelHeight;
            label.relativePosition = new Vector3(x, y);
            return label;
        }
    }
}
