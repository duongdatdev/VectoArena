using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace VectoArena.UI
{
    /// <summary>
    /// Floating UI Toolkit joystick adapted from the Blast Royale HUD.
    /// Pointer events are captured by the containing touch zone so two sticks can be used concurrently.
    /// </summary>
    [UxmlElement]
    public sealed partial class MobileJoystickElement : VisualElement
    {
        private const string BlockClass = "mobile-joystick";
        private const string ActiveClass = "mobile-joystick--active";
        private const string StickClass = "mobile-joystick__stick";
        private const string HaloClass = "mobile-joystick__halo";
        private const float DefaultDeadZone = 0.16f;

        private readonly VisualElement stick;
        private readonly VisualElement halo;
        private Vector3 initialPosition;
        private int capturedPointerId = -1;

        public event Action<Vector2> ValueChanged;
        public event Action<bool> PressedChanged;

        public MobileJoystickElement()
        {
            AddToClassList(BlockClass);

            VisualElement background = new VisualElement { name = "Background" };
            background.AddToClassList("mobile-joystick__background");
            Add(background);

            halo = new VisualElement { name = "DirectionHalo" };
            halo.AddToClassList(HaloClass);
            halo.usageHints = UsageHints.DynamicTransform;
            Add(halo);

            stick = new VisualElement { name = "Stick" };
            stick.AddToClassList(StickClass);
            stick.usageHints = UsageHints.DynamicTransform;
            Add(stick);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (parent == null)
            {
                return;
            }

            parent.RegisterCallback<PointerDownEvent>(OnPointerDown);
            parent.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            parent.RegisterCallback<PointerUpEvent>(OnPointerUp);
            parent.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            parent.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            if (parent != null)
            {
                parent.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                parent.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                parent.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                parent.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
                parent.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            }

            ResetInput(false);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (capturedPointerId >= 0 || parent == null)
            {
                return;
            }

            capturedPointerId = evt.pointerId;
            initialPosition = transform.position;
            parent.CapturePointer(evt.pointerId);

            Vector2 localPosition = parent.WorldToLocal(evt.position);
            transform.position = localPosition - new Vector2(resolvedStyle.width * 0.5f, resolvedStyle.height * 0.5f);
            AddToClassList(ActiveClass);
            PressedChanged?.Invoke(true);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != capturedPointerId || parent == null || !parent.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            float radius = Mathf.Max(1f, Mathf.Min(resolvedStyle.width, resolvedStyle.height) * 0.5f);
            Vector2 localPosition = parent.WorldToLocal(evt.position);
            Vector2 joystickCenter = (Vector2)transform.position + new Vector2(resolvedStyle.width * 0.5f, resolvedStyle.height * 0.5f);
            Vector2 delta = Vector2.ClampMagnitude(localPosition - joystickCenter, radius);

            stick.transform.position = delta;
            float magnitude = Mathf.Clamp01(delta.magnitude / radius);
            Vector2 normalized = magnitude > Mathf.Epsilon ? delta.normalized : Vector2.zero;
            halo.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90f);
            halo.style.opacity = magnitude;

            normalized.y = -normalized.y;
            ValueChanged?.Invoke(MobileInputMath.ApplyRadialDeadZone(normalized * magnitude, DefaultDeadZone));
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != capturedPointerId)
            {
                return;
            }

            if (parent != null && parent.HasPointerCapture(evt.pointerId))
            {
                parent.ReleasePointer(evt.pointerId);
            }

            ResetInput(true);
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == capturedPointerId)
            {
                ResetInput(true);
            }
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == capturedPointerId)
            {
                ResetInput(true);
            }
        }

        public void ResetInput(bool notify)
        {
            capturedPointerId = -1;
            RemoveFromClassList(ActiveClass);
            transform.position = initialPosition;
            stick.transform.position = Vector3.zero;
            halo.transform.rotation = Quaternion.identity;
            halo.style.opacity = 0f;

            if (notify)
            {
                ValueChanged?.Invoke(Vector2.zero);
                PressedChanged?.Invoke(false);
            }
        }

    }
}
