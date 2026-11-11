using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PortalEscape
{
    internal sealed class RoomPanel
    {
        private readonly DeveloperRoom developer;
        private readonly RoomWorkspace workspace;
        private readonly DeveloperPanel ui;
        private readonly RectTransform overlay;
        private readonly RectTransform content;
        private readonly ScrollRect scroll;
        private readonly Text heading;
        private readonly Text message;
        private readonly Text summary;
        private readonly InputField nameInput;
        private readonly Button create;
        private readonly Button save;
        private readonly Button load;
        private readonly Button rename;
        private readonly Button delete;
        private readonly Button open;
        private readonly RectTransform actionRow;
        private readonly RectTransform confirmationRow;
        private readonly List<Button> rows = new();
        private List<RoomData> rooms = new();
        private RoomData selected;
        private Action pending;
        private int unreadable;

        internal RoomPanel(DeveloperPanel ui, DeveloperRoom developer, RectTransform safeArea)
        {
            this.ui = ui;
            this.developer = developer;
            workspace = new RoomWorkspace(developer);

            var toolbar = TopRow(safeArea, 8, 46);
            toolbar.gameObject.AddComponent<Image>().color = DeveloperPanel.PanelColor;
            var labelArea = DeveloperPanel.Rect("Active room", toolbar);
            Stretch(labelArea, 5, 0, 110, 0);
            summary = ui.AddText(labelArea, "", 14);
            summary.alignment = TextAnchor.MiddleLeft;
            var openArea = DeveloperPanel.Rect("Open rooms", toolbar);
            openArea.anchorMin = Vector2.right;
            openArea.anchorMax = Vector2.one;
            openArea.offsetMin = new Vector2(-104, 3);
            openArea.offsetMax = new Vector2(-3, -3);
            open = ui.AddButton(openArea, "Rooms", Show);

            overlay = DeveloperPanel.Rect("Room management", safeArea);
            Stretch(overlay, 0, 0, 0, 0);
            overlay.gameObject.AddComponent<Image>().color = DeveloperPanel.PanelColor;
            var header = TopRow(overlay, 12, 44);
            var titleArea = DeveloperPanel.Rect("Rooms title", header);
            Stretch(titleArea, 0, 0, 94, 0);
            heading = ui.AddText(titleArea, "", 17);
            heading.alignment = TextAnchor.MiddleLeft;
            var closeArea = DeveloperPanel.Rect("Close rooms", header);
            closeArea.anchorMin = Vector2.right;
            closeArea.anchorMax = Vector2.one;
            closeArea.offsetMin = new Vector2(-90, 0);
            closeArea.offsetMax = Vector2.zero;
            ui.AddButton(closeArea, "Închide", Close);

            var notice = ui.AddText(TopRow(overlay, 63, 50),
                "Previzualizare locală. După restart, layout-ul se deschide relativ la poziția curentă a telefonului.", 14);
            notice.alignment = TextAnchor.MiddleLeft;

            var list = DeveloperPanel.Rect("Room list", overlay);
            Stretch(list, 8, 235, 8, 122);
            list.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.15f, 0.20f);
            list.gameObject.AddComponent<RectMask2D>();
            scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = list;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            content = DeveloperPanel.Rect("Saved rooms", list);
            content.anchorMin = Vector2.up;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            scroll.content = content;

            var inputRow = DeveloperPanel.Row(overlay, 180, 44);
            var background = inputRow.gameObject.AddComponent<Image>();
            background.color = new Color(0.17f, 0.23f, 0.30f);
            nameInput = inputRow.gameObject.AddComponent<InputField>();
            nameInput.targetGraphic = background;
            nameInput.textComponent = ui.AddText(inputRow, "", 16);
            nameInput.textComponent.alignment = TextAnchor.MiddleLeft;
            nameInput.textComponent.supportRichText = false;
            var placeholder = ui.AddText(inputRow, "Nume cameră", 16);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.75f, 0.81f, 0.87f);
            nameInput.placeholder = placeholder;
            nameInput.characterLimit = RoomData.MaxNameLength;
            nameInput.keyboardType = TouchScreenKeyboardType.Default;

            var saveRow = DeveloperPanel.Row(overlay, 127, 45);
            create = ui.AddButton(saveRow, "New Room", Create);
            save = ui.AddButton(saveRow, "Save Room", () => Run(workspace.Save, "Camera activă a fost salvată.", true));

            actionRow = DeveloperPanel.Row(overlay, 75, 45);
            load = ui.AddButton(actionRow, "Load Room", Load);
            rename = ui.AddButton(actionRow, "Rename", Rename);
            delete = ui.AddButton(actionRow, "Delete Room", Delete);
            confirmationRow = DeveloperPanel.Row(overlay, 75, 45);
            ui.AddButton(confirmationRow, "Confirmă", Confirm);
            ui.AddButton(confirmationRow, "Anulează", () => SetPending(null, ""));

            message = ui.AddText(DeveloperPanel.Row(overlay, 8, 58), "", 14);
            message.alignment = TextAnchor.MiddleLeft;
            summary.supportRichText = heading.supportRichText = message.supportRichText = false;
            overlay.gameObject.SetActive(false);
        }

        internal void Update()
        {
            open.interactable = !developer.Busy;
            summary.text = ActiveLabel();
        }

        private string ActiveLabel() => workspace.Active == null ? "Fără cameră activă"
            : $"{workspace.Active.name}{(workspace.Dirty ? " *" : "")}";

        private void Show()
        {
            if (developer.Busy)
                return;
            developer.SetRoomMenuOpen(true);
            overlay.gameObject.SetActive(true);
            SetPending(null, "");
            Refresh(workspace.Active?.id);
        }

        private void Close()
        {
            pending = null;
            overlay.gameObject.SetActive(false);
            developer.SetRoomMenuOpen(false);
        }

        private void Create()
        {
            string name = nameInput.text;
            Action create = () => Run(() => workspace.Create(name), "Camera a fost creată și salvată.", true);
            if (workspace.Active != null && workspace.Dirty)
                SetPending(create, "Crearea unei camere va abandona modificările nesalvate din camera activă.");
            else
                create();
        }

        private void Load()
        {
            var target = selected;
            if (target == null)
                return;
            Action loadRoom = () => Run(() => workspace.Load(target), "Camera a fost încărcată.", true);
            if (workspace.Dirty)
                SetPending(loadRoom, "Încărcarea va abandona modificările nesalvate din scenă.");
            else
                loadRoom();
        }

        private void Rename()
        {
            var target = selected;
            string name = nameInput.text;
            if (target != null)
                Run(() => workspace.Rename(target, name), "Camera a fost redenumită.");
        }

        private void Delete()
        {
            var target = selected;
            if (target != null)
                SetPending(() => Run(() => workspace.Delete(target), "Camera a fost ștearsă."),
                    $"Ștergi camera «{target.name}» și toate artefactele ei?");
        }

        private void Confirm()
        {
            var action = pending;
            SetPending(null, "");
            action?.Invoke();
        }

        private void SetPending(Action action, string prompt)
        {
            pending = action;
            actionRow.gameObject.SetActive(action == null);
            confirmationRow.gameObject.SetActive(action != null);
            UpdateSelection();
            SetMessage(prompt);
        }

        private void Run(Action operation, string success, bool selectActive = false)
        {
            if (developer.Busy)
                return;

            try
            {
                operation();
                Refresh(selectActive ? workspace.Active?.id : selected?.id, success);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Portal Escape: {exception}");
                SetMessage(exception.Message);
            }
        }

        private void Refresh(string selectedId, string success = "")
        {
            try
            {
                rooms = workspace.List(out unreadable);
            }
            catch (Exception exception)
            {
                rooms.Clear();
                unreadable = 0;
                success = $"Lista camerelor nu a putut fi citită. {exception.Message}";
                Debug.LogWarning($"Portal Escape: {exception}");
            }

            foreach (var row in rows)
            {
                var wrapper = row.transform.parent.gameObject;
                wrapper.SetActive(false);
                UnityEngine.Object.Destroy(wrapper);
            }
            rows.Clear();
            selected = rooms.Find(room => room.id == selectedId);
            nameInput.SetTextWithoutNotify(selected?.name ?? "");
            content.sizeDelta = new Vector2(0, rooms.Count * 48f);
            for (int i = 0; i < rooms.Count; i++)
            {
                var item = rooms[i];
                var row = TopRow(content, i * 48f + 2f, 44);
                string label = $"{item.name}  ·  {item.artifacts.Length}  ·  {item.id.Substring(0, 6)}";
                var button = ui.AddButton(row, label, () => Select(item));
                button.GetComponentInChildren<Text>().supportRichText = false;
                rows.Add(button);
            }

            scroll.verticalNormalizedPosition = 1;
            UpdateSelection();
            SetMessage(string.IsNullOrEmpty(success) && rooms.Count == 0 ? "Nu există camere salvate." : success);
        }

        private void Select(RoomData item)
        {
            selected = item;
            nameInput.SetTextWithoutNotify(item.name);
            SetPending(null, "");
        }

        private void UpdateSelection()
        {
            heading.text = ActiveLabel();
            create.interactable = pending == null;
            save.interactable = pending == null && workspace.Active != null;
            load.interactable = rename.interactable = delete.interactable = selected != null;
            for (int i = 0; i < rows.Count; i++)
                DeveloperPanel.SetActiveColor(rows[i], rooms[i] == selected);
        }

        private void SetMessage(string value)
        {
            string warning = unreadable > 0 ? $"{unreadable} fișier(e) de cameră nu au putut fi citite." : "";
            message.text = string.IsNullOrEmpty(value) ? warning
                : string.IsNullOrEmpty(warning) ? value : $"{value}\n{warning}";
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static RectTransform TopRow(Transform parent, float top, float height)
        {
            var row = DeveloperPanel.Rect("Row", parent);
            row.anchorMin = Vector2.up;
            row.anchorMax = Vector2.one;
            row.pivot = new Vector2(0.5f, 1);
            row.anchoredPosition = new Vector2(0, -top);
            row.sizeDelta = new Vector2(-16, height);
            return row;
        }
    }
}
