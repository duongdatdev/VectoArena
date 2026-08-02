using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace VectoArena.UI
{
    /// <summary>UI Toolkit container that converts Screen.safeArea into panel-space margins.</summary>
    [UxmlElement]
    public sealed partial class SafeAreaElement : VisualElement
    {
        private bool applyTop = true;
        private bool applyBottom = true;
        private bool applyLeft = true;
        private bool applyRight = true;

        [UxmlAttribute("apply-top")]
        public bool ApplyTop
        {
            get => applyTop;
            set => applyTop = value;
        }

        [UxmlAttribute("apply-bottom")]
        public bool ApplyBottom
        {
            get => applyBottom;
            set => applyBottom = value;
        }

        [UxmlAttribute("apply-left")]
        public bool ApplyLeft
        {
            get => applyLeft;
            set => applyLeft = value;
        }

        [UxmlAttribute("apply-right")]
        public bool ApplyRight
        {
            get => applyRight;
            set => applyRight = value;
        }

        public SafeAreaElement()
        {
            style.flexGrow = 1f;
            style.flexShrink = 1f;
            pickingMode = PickingMode.Ignore;
            RegisterCallback<GeometryChangedEvent>(ApplySafeArea);
        }

        private void ApplySafeArea(GeometryChangedEvent evt)
        {
            if (panel == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            try
            {
                Rect safeArea = Screen.safeArea;
                Vector2 topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safeArea.xMin, Screen.height - safeArea.yMax));
                Vector2 bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width - safeArea.xMax, safeArea.yMin));

                style.marginTop = applyTop ? Mathf.Max(0f, topLeft.y) : 0f;
                style.marginBottom = applyBottom ? Mathf.Max(0f, bottomRight.y) : 0f;
                style.marginLeft = applyLeft ? Mathf.Max(0f, topLeft.x) : 0f;
                style.marginRight = applyRight ? Mathf.Max(0f, bottomRight.x) : 0f;
            }
            catch (InvalidCastException)
            {
                // UI Builder can briefly provide an editor panel that cannot convert runtime coordinates.
            }
        }

    }
}
