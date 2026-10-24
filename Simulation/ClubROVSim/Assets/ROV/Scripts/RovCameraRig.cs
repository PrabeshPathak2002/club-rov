using UnityEngine;
using UnityEngine.InputSystem;

namespace ClubROV.Simulation
{
    public sealed class RovCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        private bool pilotView;
        public bool PilotView => pilotView;
        public void ToggleView() => pilotView = !pilotView;
        public void Configure(Transform vehicle) => target = vehicle;
        private void LateUpdate()
        {
            if (target == null) return;
            if ((Keyboard.current?.vKey.wasPressedThisFrame ?? false) || (Gamepad.current?.selectButton.wasPressedThisFrame ?? false))
                ToggleView();
            Vector3 offset = pilotView ? new Vector3(0, 0.04f, 0.31f) : new Vector3(1.1f, 0.65f, -1.4f);
            Vector3 position = target.TransformPoint(offset);
            Vector3 look = pilotView ? target.position + target.forward * 3 : target.position;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position, Vector3.up));
        }
    }
}
