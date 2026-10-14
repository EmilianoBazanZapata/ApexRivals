using UnityEngine;
using UnityEngine.UI;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Mouse clicks select through Selectable.OnPointerDown, outside UiFocus. A modal therefore needs a
    /// transparent raycast target covering the screen so pointer input outside its box is swallowed
    /// instead of reaching the controls behind it.
    /// </summary>
    public static class UiModalPointerBlocker
    {
        private const string BlockerName = "ModalPointerBlocker";
        private const float CoverSize = 100000f;

        public static void Ensure(GameObject modalRoot)
        {
            if (modalRoot == null || modalRoot.transform.Find(BlockerName) != null)
            {
                return;
            }

            var blocker = new GameObject(BlockerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)blocker.transform;
            rect.SetParent(modalRoot.transform, false);
            // First child: drawn under the dialog content, above everything behind the modal.
            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CoverSize, CoverSize);
            rect.anchoredPosition = Vector2.zero;

            var image = blocker.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
        }
    }
}
