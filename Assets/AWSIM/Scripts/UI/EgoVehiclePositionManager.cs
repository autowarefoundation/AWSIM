using UnityEngine;

namespace AWSIM.Scripts.UI
{
    public class EgoVehiclePositionManager : MonoBehaviour
    {
        public Transform EgoTransform { private get; set; }
        private Rigidbody egoRigidbody;
        private Vector3 initialEgoPosition;
        private Quaternion initialEgoRotation;

        private void Awake()
        {
            egoRigidbody = EgoTransform.GetComponent<Rigidbody>();
        }

        private void Start()
        {
            initialEgoPosition = EgoTransform.position;
            initialEgoRotation = EgoTransform.rotation;
        }

        // If the ego transform reference is present, reset the ego to the initial position and rotation.
        public void ResetEgoToSpawnPoint()
        {
            if (!EgoTransform)
            {
                Debug.LogWarning("Ego transform reference is missing. No ego to reset here!");
                return;
            }

            EgoTransform.SetPositionAndRotation(initialEgoPosition, initialEgoRotation);
            egoRigidbody.Sleep();
        }
    }
}