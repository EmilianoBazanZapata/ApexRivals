using System;
using UnityEngine;

namespace ApexRivals.UI.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GarageVehiclePreviewController : MonoBehaviour
    {
        [Serializable]
        private struct Entry
        {
#pragma warning disable CS0649 // Unity assigns these serialized entry fields.
            public string vehicleId;
            public GameObject prefab;
#pragma warning restore CS0649
        }

        [SerializeField] private Transform previewAnchor;
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private float rotationDegreesPerSecond = 25f;

        private GameObject _currentInstance;
        private string _currentVehicleId = string.Empty;

        public void ShowVehicle(string vehicleId)
        {
            if (string.IsNullOrEmpty(vehicleId) || vehicleId == _currentVehicleId)
            {
                return;
            }

            GameObject prefab = FindPrefab(vehicleId);
            if (prefab == null)
            {
                return;
            }

            if (_currentInstance != null)
            {
                Destroy(_currentInstance);
            }

            Transform anchor = previewAnchor != null ? previewAnchor : transform;
            _currentInstance = Instantiate(prefab, anchor.position, anchor.rotation, anchor);
            _currentVehicleId = vehicleId;
            NeutralizeForDisplay(_currentInstance);
        }

        private void Update()
        {
            if (_currentInstance != null)
            {
                _currentInstance.transform.Rotate(Vector3.up, rotationDegreesPerSecond * Time.deltaTime, Space.World);
            }
        }

        private GameObject FindPrefab(string vehicleId)
        {
            for (var index = 0; index < entries.Length; index++)
            {
                if (entries[index].vehicleId == vehicleId)
                {
                    return entries[index].prefab;
                }
            }

            return null;
        }

        private static void NeutralizeForDisplay(GameObject instance)
        {
            Rigidbody instanceRigidbody = instance.GetComponentInChildren<Rigidbody>(true);
            if (instanceRigidbody != null)
            {
                instanceRigidbody.isKinematic = true;
                instanceRigidbody.useGravity = false;
            }

            Behaviour[] behaviours = instance.GetComponentsInChildren<Behaviour>(true);
            for (var index = 0; index < behaviours.Length; index++)
            {
                behaviours[index].enabled = false;
            }
        }
    }
}
