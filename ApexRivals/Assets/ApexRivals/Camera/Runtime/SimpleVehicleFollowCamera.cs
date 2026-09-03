using UnityEngine;

namespace ApexRivals.Camera.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SimpleVehicleFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [SerializeField] private Vector3 offset = new Vector3(0f, 4.5f, -8f);

        [SerializeField, Min(0f)] private float positionSmoothing = 8f;

        [SerializeField, Min(0f)] private float rotationSmoothing = 10f;

        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);

        private void LateUpdate()
        {
            if (target == null)
                return;

            var desiredPosition = target.TransformPoint(offset);
            var positionT = 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionT);

            var lookDirection = target.position + lookOffset - transform.position;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            var rotationT = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
        }
    }
}
