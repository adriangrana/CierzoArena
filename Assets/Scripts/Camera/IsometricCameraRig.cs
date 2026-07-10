using UnityEngine;

namespace CierzoArena.CameraSystem
{
    public sealed class IsometricCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -12f);
        [SerializeField] private float followSharpness = 8f;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
