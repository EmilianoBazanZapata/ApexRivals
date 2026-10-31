using UnityEngine;
using UnityEngine.EventSystems;

namespace ApexRivals.Audio.Runtime.Bench
{
    /// <summary>True while the pointer holds this control down (THROTTLE / BRAKE on the engine bench).</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }

        public void OnPointerDown(PointerEventData eventData) => Held = true;
        public void OnPointerUp(PointerEventData eventData) => Held = false;
        public void OnPointerExit(PointerEventData eventData) => Held = false;

        private void OnDisable() => Held = false;
    }
}
