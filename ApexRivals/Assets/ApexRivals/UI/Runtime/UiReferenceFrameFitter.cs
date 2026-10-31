using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    /// <summary>
    /// Uniformly scales a fixed-size layout frame (1920x1080 by default) so it always fits inside its parent,
    /// for screens hosted on canvases whose scaler is not set up for that reference resolution.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiReferenceFrameFitter : MonoBehaviour
    {
        [SerializeField, Tooltip("Disable to keep the RectTransform scale authored in the Editor.")] private bool automaticScaling = true;
        [SerializeField, Tooltip("Opt in to previewing the calculated scale while editing the scene.")] private bool previewInEditMode;
        [SerializeField, Tooltip("Authored scale multiplied by the parent fit ratio.")] private Vector3 baseScale = Vector3.one;

        [SerializeField] private Vector2 referenceSize = new Vector2(1920f, 1080f);

        private Vector2 _fittedParentSize;

        private void OnEnable()
        {
            _fittedParentSize = Vector2.zero;
            Fit();
        }

        private void LateUpdate()
        {
            Fit();
        }

        private void OnValidate()
        {
            _fittedParentSize = Vector2.zero;
        }

        private void Fit()
        {
            if (!automaticScaling || (!Application.isPlaying && !previewInEditMode)) return;
            var parent = transform.parent as RectTransform;
            if (parent == null || referenceSize.x <= 0f || referenceSize.y <= 0f)
            {
                return;
            }

            var size = parent.rect.size;
            if (size == _fittedParentSize || size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            _fittedParentSize = size;
            var scale = Mathf.Min(size.x / referenceSize.x, size.y / referenceSize.y);
            transform.localScale = new Vector3(baseScale.x * scale, baseScale.y * scale, baseScale.z);
        }
    }
}
