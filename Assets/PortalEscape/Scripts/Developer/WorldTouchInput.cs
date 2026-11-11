using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace PortalEscape
{
    internal enum WorldTouchPhase { Began, Tap, Drag, Drop, Canceled }

    internal readonly struct WorldTouch
    {
        internal readonly WorldTouchPhase Phase;
        internal readonly Vector2 Point;
        internal readonly bool OverUi;

        internal WorldTouch(WorldTouchPhase phase, Vector2 point = default, bool overUi = false)
        {
            Phase = phase;
            Point = point;
            OverUi = overUi;
        }
    }

    internal sealed class WorldTouchInput
    {
        private readonly List<RaycastResult> uiHits = new();
        private int? touchId;
        private Vector2 start;
        private bool dragging;
        private bool rejected;

        internal void Reset()
        {
            touchId = null;
            dragging = false;
            rejected = false;
        }

        internal bool TryRead(bool acceptNewTouch, out WorldTouch gesture)
        {
            gesture = default;
            var touches = Touch.activeTouches;
            if (touches.Count == 0)
            {
                bool interrupted = touchId.HasValue;
                Reset();
                gesture = new WorldTouch(WorldTouchPhase.Canceled);
                return interrupted;
            }

            if (touches.Count > 1 || rejected)
            {
                bool interrupted = touchId.HasValue;
                touchId = null;
                rejected = true;
                bool anyPressed = false;
                foreach (var active in touches)
                    anyPressed |= active.isInProgress;
                if (!anyPressed)
                    Reset();

                gesture = new WorldTouch(WorldTouchPhase.Canceled);
                return interrupted;
            }

            var touch = touches[0];
            var point = touch.screenPosition;
            if (touch.phase == TouchPhase.Began)
            {
                rejected = !acceptNewTouch || IsOverUi(point);
                if (rejected)
                    return false;

                touchId = touch.touchId;
                start = point;
                dragging = false;
                gesture = new WorldTouch(WorldTouchPhase.Began, point);
                return true;
            }

            if (touchId != touch.touchId)
                return false;

            if (touch.phase == TouchPhase.Canceled)
            {
                Reset();
                gesture = new WorldTouch(WorldTouchPhase.Canceled);
                return true;
            }

            float slop = Mathf.Max(12f, Mathf.Min(Screen.width, Screen.height) * 0.018f);
            dragging |= (point - start).sqrMagnitude > slop * slop;
            if (touch.phase == TouchPhase.Ended)
            {
                bool overUi = IsOverUi(point);
                var phase = dragging ? WorldTouchPhase.Drop
                    : overUi ? WorldTouchPhase.Canceled : WorldTouchPhase.Tap;
                gesture = new WorldTouch(phase, point, overUi);
                Reset();
                return true;
            }

            if (!dragging)
                return false;

            gesture = new WorldTouch(WorldTouchPhase.Drag, point, IsOverUi(point));
            return true;
        }

        private bool IsOverUi(Vector2 point)
        {
            var events = EventSystem.current;
            if (events == null)
                return true;

            events.RaycastAll(new PointerEventData(events) { position = point }, uiHits);
            foreach (var hit in uiHits)
                if (hit.module is GraphicRaycaster)
                    return true;

            return false;
        }
    }
}
