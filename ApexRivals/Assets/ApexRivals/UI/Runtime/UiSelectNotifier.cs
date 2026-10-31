using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ApexRivals.UI.Runtime
{
    /// <summary>Raises <see cref="Selected"/> when this control gains EventSystem focus (keyboard, gamepad or hover).</summary>
    [DisallowMultipleComponent]
    public sealed class UiSelectNotifier : MonoBehaviour, ISelectHandler
    {
        public event Action Selected;

        public void OnSelect(BaseEventData eventData)
        {
            Selected?.Invoke();
        }
    }
}
