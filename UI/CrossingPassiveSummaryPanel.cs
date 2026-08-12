using ColossalFramework.UI;
using UnityEngine;

namespace PedestrianCrossingToolkit
{
    /// <summary>
    /// Retained close-up crossing summary.  The former immediate-mode version rebuilt
    /// every rectangle, label and lamp during every Unity GUI repaint; this component
    /// keeps the same presentation alive and changes only position or live state.
    /// </summary>
    internal sealed class CrossingPassiveSummaryPanel : UIPanel
    {
        internal const string ComponentNamePrefix = "PCT-Retained-Crossing-Summary";

        private const float PanelWidth = 286f;
        private const float PanelHeight = 126f;
        private const float StaticRefreshSeconds = 0.75f;

        private UIPanel _crossingAccent;
        private UIPanel _vehicleBody;
        private UIPanel _pedestrianBody;
        private UILabel[] _labels;
        private SignalLamp[] _vehicleLamps;
        private SignalLamp[] _pedestrianLamps;
        private int _assetId;
        private PedestrianToolMode _mode;
        private RoadBaseAI.TrafficLightState _vehicleState;
        private RoadBaseAI.TrafficLightState _pedestrianState;
        private string _phase;
        private bool _waiting;
        private bool _crossing;
        private float _nextStaticRefresh;
        private float _scaleX = -1f;
        private float _scaleY = -1f;
        private bool _signalPresentation;
        private bool _presentationReady;
        private bool _initialized;

        internal void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            name = ComponentNamePrefix;
            backgroundSprite = "GenericPanel";
            color = new Color32(0, 0, 0, 199);
            opacity = 1f;
            isInteractive = false;
            canFocus = false;

            _crossingAccent = CreatePanel(this, "Accent", new Color32(235, 245, 255, 240));
            UIPanel accentInner = CreatePanel(_crossingAccent, "AccentInner", new Color32(14, 17, 19, 250));
            CreatePanel(accentInner, "AccentStripe1", Color.white);
            CreatePanel(accentInner, "AccentStripe2", Color.white);
            CreatePanel(accentInner, "AccentStripe3", Color.white);
            CreatePanel(accentInner, "AccentHighlight", new Color32(255, 255, 255, 87));

            _vehicleBody = CreateSignalBody("VehicleSignal", 3);
            _pedestrianBody = CreateSignalBody("PedestrianSignal", 2);
            _vehicleLamps = CreateLampSet(_vehicleBody, 3);
            _pedestrianLamps = CreateLampSet(_pedestrianBody, 2);

            _labels = new UILabel[6];
            for (int i = 0; i < _labels.Length; i++)
            {
                UILabel label = AddUIComponent<UILabel>();
                label.name = ComponentNamePrefix + "-Label" + i;
                label.autoSize = false;
                label.autoHeight = false;
                label.wordWrap = false;
                label.isInteractive = false;
                label.textColor = i == 0
                    ? new Color32(255, 255, 255, 245)
                    : new Color32(219, 235, 255, 235);
                _labels[i] = label;
            }

            isVisible = false;
        }

        internal void Show(Rect screenRect, UIView view, CrossingPlacementAsset asset)
        {
            Initialize();
            if (view == null || asset.Id == 0)
            {
                HidePanel();
                return;
            }

            float scaleX = view.fixedWidth > 0f ? Screen.width / view.fixedWidth : 1f;
            float scaleY = view.fixedHeight > 0f ? Screen.height / view.fixedHeight : 1f;
            CrossingPathBuilder.SignalControllerDebugSnapshot snapshot =
                default(CrossingPathBuilder.SignalControllerDebugSnapshot);
            bool hasSignal = asset.Placement.Mode == PedestrianToolMode.SignalCrossing
                             && CrossingPathBuilder.TryGetSignalControllerDebugSnapshot(asset.Id, out snapshot)
                             && snapshot.IsValid;
            bool presentationChanged = SetSignalPresentation(hasSignal);
            bool scaleChanged = ApplyLayout(scaleX, scaleY);
            if (presentationChanged || scaleChanged)
                ApplyLabelLayout(hasSignal, scaleX, scaleY);

            Vector3 nextPosition = new Vector3(screenRect.x / scaleX, screenRect.y / scaleY);
            if ((relativePosition - nextPosition).sqrMagnitude > 0.0001f)
                relativePosition = nextPosition;

            bool newAsset = _assetId != asset.Id || _mode != asset.Placement.Mode;
            bool becameVisible = !isVisible;
            if (becameVisible)
                isVisible = true;

            if (newAsset || presentationChanged || Time.unscaledTime >= _nextStaticRefresh)
            {
                RefreshStaticText(asset, hasSignal, snapshot);
                _assetId = asset.Id;
                _mode = asset.Placement.Mode;
                _nextStaticRefresh = Time.unscaledTime + StaticRefreshSeconds;
            }

            if (hasSignal)
                RefreshLiveSignalState(snapshot, newAsset || presentationChanged || becameVisible);
        }

        internal void HidePanel()
        {
            if (isVisible)
                isVisible = false;
        }

        private void RefreshStaticText(
            CrossingPlacementAsset asset,
            bool hasSignal,
            CrossingPathBuilder.SignalControllerDebugSnapshot snapshot)
        {
            if (hasSignal)
            {
                _labels[0].text = "Signal Crossing #" + asset.Id;
                _labels[3].text = "Path: " + PedestrianCrossingInteractionTool.FormatPathQueryState(asset);
                _labels[4].text = "Owned assets: " + PedestrianCrossingInteractionTool.FormatOwnedAssetQueryState(asset);
                _labels[5].text = "Crossing tab inspection.";
                return;
            }

            _labels[0].text = PedestrianCrossingInteractionTool.ToTitleCase(
                PedestrianCrossingToolkitState.GetModeLabel(asset.Placement.Mode)) + " #" + asset.Id;
            _labels[1].text = "Path: " + PedestrianCrossingInteractionTool.FormatPathQueryState(asset);
            _labels[2].text = "Suppression: " + PedestrianCrossingInteractionTool.FormatSuppressionQueryState(asset);
            _labels[3].text = "Signal: " + PedestrianCrossingInteractionTool.FormatSignalQueryState(asset);
            _labels[4].text = "Owned assets: " + PedestrianCrossingInteractionTool.FormatOwnedAssetQueryState(asset);
            _labels[5].text = "Crossing tab inspection.";
        }

        private void RefreshLiveSignalState(
            CrossingPathBuilder.SignalControllerDebugSnapshot snapshot,
            bool force)
        {
            string phase = PedestrianCrossingInteractionTool.FormatSignalPhase(snapshot);
            if (force || phase != _phase)
            {
                _phase = phase;
                _labels[1].text = "Signal: " + phase;
            }

            if (force
                || snapshot.HasPedestriansWaitingAtEntrance != _waiting
                || snapshot.HasPedestriansOnCrossing != _crossing)
            {
                _waiting = snapshot.HasPedestriansWaitingAtEntrance;
                _crossing = snapshot.HasPedestriansOnCrossing;
                _labels[2].text = "Waiting: " + (_waiting ? "yes" : "no")
                                  + "  Crossing: " + (_crossing ? "yes" : "no");
            }

            if (force || snapshot.VehicleState != _vehicleState)
            {
                _vehicleState = snapshot.VehicleState;
                bool red = _vehicleState != RoadBaseAI.TrafficLightState.Green
                           && _vehicleState != RoadBaseAI.TrafficLightState.RedToGreen
                           && _vehicleState != RoadBaseAI.TrafficLightState.GreenToRed;
                bool amber = _vehicleState == RoadBaseAI.TrafficLightState.RedToGreen
                             || _vehicleState == RoadBaseAI.TrafficLightState.GreenToRed;
                bool green = _vehicleState == RoadBaseAI.TrafficLightState.Green;
                SetLamp(_vehicleLamps[0], red, new Color32(255, 46, 36, 255), new Color32(61, 8, 6, 255));
                SetLamp(_vehicleLamps[1], amber, new Color32(255, 163, 20, 255), new Color32(64, 31, 5, 255));
                SetLamp(_vehicleLamps[2], green, new Color32(46, 245, 82, 255), new Color32(5, 46, 13, 255));
            }

            if (force || snapshot.PedestrianState != _pedestrianState)
            {
                _pedestrianState = snapshot.PedestrianState;
                bool green = _pedestrianState == RoadBaseAI.TrafficLightState.Green;
                SetLamp(_pedestrianLamps[0], !green, new Color32(255, 46, 36, 255), new Color32(61, 8, 6, 255));
                SetLamp(_pedestrianLamps[1], green, new Color32(46, 245, 82, 255), new Color32(5, 46, 13, 255));
            }
        }

        private bool SetSignalPresentation(bool signal)
        {
            if (_presentationReady && _signalPresentation == signal)
                return false;

            _presentationReady = true;
            _signalPresentation = signal;
            _vehicleBody.isVisible = signal;
            _pedestrianBody.isVisible = signal;
            _crossingAccent.isVisible = !signal;
            return true;
        }

        private bool ApplyLayout(float scaleX, float scaleY)
        {
            scaleX = Mathf.Max(0.01f, scaleX);
            scaleY = Mathf.Max(0.01f, scaleY);
            if (Mathf.Abs(scaleX - _scaleX) < 0.001f && Mathf.Abs(scaleY - _scaleY) < 0.001f)
                return false;

            _scaleX = scaleX;
            _scaleY = scaleY;
            width = PanelWidth / scaleX;
            height = PanelHeight / scaleY;

            LayoutPanel(_crossingAccent, 12f, 12f, 34f, 102f, scaleX, scaleY);
            UIComponent accentInner = _crossingAccent.components[0];
            LayoutPanel(accentInner, 2f, 2f, 30f, 98f, scaleX, scaleY);
            LayoutPanel(accentInner.components[0], 5f, 8f, 20f, 5f, scaleX, scaleY);
            LayoutPanel(accentInner.components[1], 5f, 23f, 20f, 5f, scaleX, scaleY);
            LayoutPanel(accentInner.components[2], 5f, 38f, 20f, 5f, scaleX, scaleY);
            LayoutPanel(accentInner.components[3], 2f, 2f, 26f, 1f, scaleX, scaleY);

            LayoutSignalBody(_vehicleBody, 12f, 12f, 33f, 51f, scaleX, scaleY, _vehicleLamps, new[] { 3f, 17f, 31f });
            LayoutSignalBody(_pedestrianBody, 54f, 18f, 33f, 39f, scaleX, scaleY, _pedestrianLamps, new[] { 5f, 20f });
            return true;
        }

        private void ApplyLabelLayout(bool signal, float scaleX, float scaleY)
        {
            float labelX = signal ? 98f : 58f;
            float labelWidth = signal ? 178f : 218f;
            for (int i = 0; i < _labels.Length; i++)
            {
                _labels[i].relativePosition = new Vector3(labelX / scaleX, (10f + i * 19f) / scaleY);
                _labels[i].width = labelWidth / scaleX;
                _labels[i].height = 18f / scaleY;
                _labels[i].textScale = 0.78f / scaleY;
            }
        }

        private static UIPanel CreatePanel(UIComponent parent, string suffix, Color color)
        {
            UIPanel panel = parent.AddUIComponent<UIPanel>();
            panel.name = ComponentNamePrefix + "-" + suffix;
            panel.backgroundSprite = "GenericPanel";
            panel.color = color;
            panel.isInteractive = false;
            panel.canFocus = false;
            return panel;
        }

        private UIPanel CreateSignalBody(string suffix, int lampCount)
        {
            UIPanel body = CreatePanel(this, suffix, new Color32(235, 245, 255, 240));
            UIPanel inner = CreatePanel(body, suffix + "Inner", new Color32(14, 17, 19, 250));
            CreatePanel(inner, suffix + "Highlight", new Color32(56, 64, 71, 191));
            return body;
        }

        private static SignalLamp[] CreateLampSet(UIPanel body, int count)
        {
            UIComponent inner = body.components[0];
            SignalLamp[] lamps = new SignalLamp[count];
            for (int i = 0; i < count; i++)
            {
                UITextureSprite outer = inner.AddUIComponent<UITextureSprite>();
                outer.name = ComponentNamePrefix + "-LampOuter" + i;
                outer.texture = PedestrianCrossingInteractionTool.GetSignalLampCircleTexture();
                outer.color = new Color32(4, 5, 5, 255);
                outer.isInteractive = false;

                UITextureSprite lens = outer.AddUIComponent<UITextureSprite>();
                lens.name = ComponentNamePrefix + "-LampLens" + i;
                lens.texture = PedestrianCrossingInteractionTool.GetSignalLampCircleTexture();
                lens.isInteractive = false;

                UITextureSprite highlight = lens.AddUIComponent<UITextureSprite>();
                highlight.name = ComponentNamePrefix + "-LampHighlight" + i;
                highlight.texture = PedestrianCrossingInteractionTool.GetSignalLampCircleTexture();
                highlight.color = new Color32(255, 255, 255, 71);
                highlight.isInteractive = false;
                lamps[i] = new SignalLamp(outer, lens, highlight);
            }

            return lamps;
        }

        private static void LayoutSignalBody(
            UIPanel body,
            float x,
            float y,
            float bodyWidth,
            float bodyHeight,
            float scaleX,
            float scaleY,
            SignalLamp[] lamps,
            float[] lampYs)
        {
            LayoutPanel(body, x, y, bodyWidth, bodyHeight, scaleX, scaleY);
            UIComponent inner = body.components[0];
            LayoutPanel(inner, 2f, 2f, bodyWidth - 4f, bodyHeight - 4f, scaleX, scaleY);
            LayoutPanel(inner.components[0], 1f, 1f, bodyWidth - 6f, 1f, scaleX, scaleY);
            float lampX = ((bodyWidth - 4f) - 13f) * 0.5f;
            for (int i = 0; i < lamps.Length; i++)
            {
                LayoutPanel(lamps[i].Outer, lampX, lampYs[i], 13f, 13f, scaleX, scaleY);
                LayoutPanel(lamps[i].Lens, 2f, 2f, 9f, 9f, scaleX, scaleY);
                LayoutPanel(lamps[i].Highlight, 2f, 2f, 4f, 4f, scaleX, scaleY);
            }
        }

        private static void LayoutPanel(
            UIComponent component,
            float x,
            float y,
            float componentWidth,
            float componentHeight,
            float scaleX,
            float scaleY)
        {
            component.relativePosition = new Vector3(x / scaleX, y / scaleY);
            component.width = componentWidth / scaleX;
            component.height = componentHeight / scaleY;
        }

        private static void SetLamp(SignalLamp lamp, bool active, Color32 activeColor, Color32 inactiveColor)
        {
            lamp.Lens.color = active ? activeColor : inactiveColor;
            lamp.Highlight.isVisible = active;
        }

        private sealed class SignalLamp
        {
            internal readonly UITextureSprite Outer;
            internal readonly UITextureSprite Lens;
            internal readonly UITextureSprite Highlight;

            internal SignalLamp(UITextureSprite outer, UITextureSprite lens, UITextureSprite highlight)
            {
                Outer = outer;
                Lens = lens;
                Highlight = highlight;
            }
        }
    }
}
