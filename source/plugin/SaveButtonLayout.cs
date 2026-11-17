using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcessorTycoonRussian
{
    internal sealed class SaveButtonLayout
    {
        readonly RectTransform caption, icon;
        readonly RectState captionState, iconState;
        bool applied;

        SaveButtonLayout(RectTransform caption, RectTransform icon)
        {
            this.caption = caption;
            this.icon = icon;
            captionState = new RectState(caption);
            iconState = new RectState(icon);
        }

        internal static SaveButtonLayout Find(TMP_Text text)
        {
            foreach (var component in text.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (component == null) continue;
                var type = component.GetType();
                string field;
                if (type.FullName == "ProcessorTycoon.Save.UI.SaveWindow") field = "saveButton";
                else if (type.FullName == "ProcessorTycoon.Save.UI.LoadWindow") field = "loadButton";
                else continue;
                var button = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(component) as Component;
                if (button == null || text.transform.parent != button.transform) continue;
                Image found = null;
                foreach (var image in button.GetComponentsInChildren<Image>(true))
                {
                    if (image.transform.parent != button.transform || image.sprite == null) continue;
                    var rect = image.rectTransform.rect;
                    if (rect.width <= 0 || rect.height <= 0 || rect.width > 32f || rect.height > 32f) continue;
                    if (found != null) return null;
                    found = image;
                }
                if (found != null) return new SaveButtonLayout(text.rectTransform, found.rectTransform);
            }
            return null;
        }

        internal void Apply()
        {
            if (applied || !caption || !icon) return;
            var size = icon.rect.size;
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = size;
            icon.anchoredPosition = new Vector2(18f, 0f);
            caption.anchorMin = new Vector2(0f, caption.anchorMin.y);
            caption.anchorMax = new Vector2(1f, caption.anchorMax.y);
            caption.offsetMin = new Vector2(32f, caption.offsetMin.y);
            caption.offsetMax = new Vector2(-8f, caption.offsetMax.y);
            applied = true;
        }

        internal void Restore()
        {
            if (!applied) return;
            if (caption) captionState.Restore(caption);
            if (icon) iconState.Restore(icon);
            applied = false;
        }

        struct RectState
        {
            readonly Vector2 anchorMin, anchorMax, pivot, size, position;
            internal RectState(RectTransform rect)
            {
                anchorMin = rect.anchorMin; anchorMax = rect.anchorMax; pivot = rect.pivot;
                size = rect.sizeDelta; position = rect.anchoredPosition;
            }
            internal void Restore(RectTransform rect)
            {
                rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
                rect.sizeDelta = size; rect.anchoredPosition = position;
            }
        }
    }
}
