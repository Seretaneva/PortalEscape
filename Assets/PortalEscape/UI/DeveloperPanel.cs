using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PortalEscape
{
    [RequireComponent(typeof(DeveloperRoom))]
    public sealed class DeveloperPanel : MonoBehaviour
    {
        private static readonly Color PanelColor = new(0.07f, 0.10f, 0.14f, 0.96f);
        private static readonly Color ButtonColor = new(0.17f, 0.23f, 0.30f);
        private static readonly Color ActiveColor = new(0.08f, 0.40f, 0.46f);

        private DeveloperRoom room;
        private Font font;
        private RectTransform safeArea;
        private RectTransform marker;
        private RectTransform inventoryPanel;
        private RectTransform editPanel;
        private RectTransform deletePanel;
        private Text status;
        private Button rotateLeft;
        private Button rotateRight;
        private Button delete;
        private readonly List<Button> modes = new();
        private readonly List<Button> choices = new();
        private Rect previousSafeArea;
        private Vector2Int previousResolution;

        private void Start()
        {
            room = GetComponent<DeveloperRoom>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var overlay = new GameObject("Developer UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(420, 860);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var overlayRect = overlay.GetComponent<RectTransform>();
            marker = Rect("Selection marker", overlayRect);
            marker.sizeDelta = new Vector2(32, 32);
            var symbol = AddText(marker, "●", 22);
            symbol.color = new Color(0.30f, 0.93f, 0.95f);
            var shadow = symbol.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1, -1);
            marker.gameObject.SetActive(false);

            safeArea = Rect("Safe area", overlayRect);
            var panel = Rect("Developer Mode controls", safeArea);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.right;
            panel.pivot = new Vector2(0.5f, 0);
            panel.anchoredPosition = new Vector2(0, 8);
            panel.sizeDelta = new Vector2(-16, 204);
            panel.gameObject.AddComponent<Image>().color = PanelColor;

            status = AddText(Row(panel, 157, 42), "", 15);
            status.alignment = TextAnchor.MiddleLeft;
            var modeRow = Row(panel, 106, 46);
            foreach (DeveloperMode mode in System.Enum.GetValues(typeof(DeveloperMode)))
                modes.Add(AddButton(modeRow, mode.ToString(), () => room.SetMode(mode)));

            inventoryPanel = Row(panel, 5, 96);
            const int columns = 4;
            int rows = Mathf.Max(1, Mathf.CeilToInt(room.Inventory.Count / (float)columns));
            float rowHeight = 96f / rows;
            for (int rowIndex = 0; rowIndex < rows; rowIndex++)
            {
                var row = Row(inventoryPanel, (rows - rowIndex - 1) * rowHeight, rowHeight - 2, 0);
                int end = Mathf.Min((rowIndex + 1) * columns, room.Inventory.Count);
                for (int index = rowIndex * columns; index < end; index++)
                {
                    var definition = room.Inventory[index];
                    choices.Add(AddButton(row, definition.DisplayName, () => room.Choose(definition)));
                }
            }

            editPanel = Row(panel, 49, 46);
            rotateLeft = AddButton(editPanel, "-15°", () => room.Rotate(-15f));
            rotateRight = AddButton(editPanel, "+15°", () => room.Rotate(15f));
            deletePanel = Row(panel, 49, 46);
            delete = AddButton(deletePanel, "Sterge selectia", room.DeleteSelection);
        }

        private void LateUpdate()
        {
            if (safeArea == null)
                return;

            var resolution = new Vector2Int(Screen.width, Screen.height);
            var area = Screen.safeArea;
            if (area != previousSafeArea || resolution != previousResolution)
            {
                safeArea.anchorMin = area.min / (Vector2)resolution;
                safeArea.anchorMax = area.max / (Vector2)resolution;
                safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
                previousSafeArea = area;
                previousResolution = resolution;
            }

            inventoryPanel.gameObject.SetActive(room.Mode == DeveloperMode.Place);
            editPanel.gameObject.SetActive(room.Mode == DeveloperMode.Edit);
            deletePanel.gameObject.SetActive(room.Mode == DeveloperMode.Delete);
            for (int i = 0; i < modes.Count; i++)
            {
                modes[i].interactable = !room.Busy;
                SetActiveColor(modes[i], (int)room.Mode == i);
            }

            for (int i = 0; i < choices.Count; i++)
            {
                choices[i].interactable = !room.Busy;
                SetActiveColor(choices[i], room.Inventory[i] == room.Choice);
            }

            var selection = room.Selection;
            bool canAct = selection != null && !room.Busy;
            delete.interactable = canAct;
            rotateLeft.interactable = rotateRight.interactable = canAct && !room.HoldingArtifact;
            string label = room.Mode == DeveloperMode.Place ? room.Choice?.DisplayName
                : selection != null ? selection.Definition.DisplayName : null;
            status.text = label == null ? room.Status : $"{label}  |  {room.Status}";
            UpdateMarker(selection);
        }

        private void UpdateMarker(PlacedArtifact selection)
        {
            if (selection == null)
            {
                marker.gameObject.SetActive(false);
                return;
            }

            Vector3 point = room.ArCamera.WorldToScreenPoint(selection.MarkerPosition);
            bool visible = point.z > 0 && point.x >= 0 && point.x <= Screen.width &&
                point.y >= 0 && point.y <= Screen.height;
            marker.gameObject.SetActive(visible);
            if (visible)
            {
                marker.anchorMin = marker.anchorMax = new Vector2(point.x / Screen.width, point.y / Screen.height);
                marker.anchoredPosition = Vector2.zero;
            }
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform Row(Transform parent, float bottom, float height, float padding = 8)
        {
            var row = Rect("Row", parent);
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.right;
            row.pivot = new Vector2(0.5f, 0);
            row.anchoredPosition = new Vector2(0, bottom);
            row.sizeDelta = new Vector2(-2 * padding, height);
            return row;
        }

        private Button AddButton(RectTransform row, string label, UnityEngine.Events.UnityAction action)
        {
            if (!row.TryGetComponent<HorizontalLayoutGroup>(out _))
            {
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 5;
                layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            }

            var rect = Rect(label, row);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            AddText(rect, label, 14);
            return button;
        }

        private Text AddText(RectTransform parent, string label, int size)
        {
            var rect = Rect("Label", parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(3, 0);
            rect.offsetMax = new Vector2(-3, 0);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.color = Color.white;
            text.text = label;
            return text;
        }

        private static void SetActiveColor(Button button, bool active)
        {
            button.targetGraphic.color = active ? ActiveColor : ButtonColor;
        }
    }
}
