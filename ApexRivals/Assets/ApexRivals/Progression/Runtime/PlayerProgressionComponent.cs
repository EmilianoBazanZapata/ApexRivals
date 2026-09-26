using UnityEngine;

namespace ApexRivals.Progression.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerProgressionComponent : MonoBehaviour
    {
        [SerializeField, Min(0)]
        private int startingCurrency;

        private PlayerProgressionState _state;

        public PlayerProgressionState State => _state ??= new PlayerProgressionState(startingCurrency);

        private void Awake()
        {
            _state = new PlayerProgressionState(startingCurrency);
        }
    }
}
