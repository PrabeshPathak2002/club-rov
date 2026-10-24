using UnityEngine;
using UnityEngine.InputSystem;

namespace ClubROV.Simulation
{
    public sealed class RovCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        public enum ViewMode { Chase, Fpv, TopDown }
        private ViewMode mode;
        private Camera view;
        private RovFpvCameraFeed feed;
        private RovPilotInput pilot;
        private float chaseFieldOfView;
        private void Awake() { view = GetComponent<Camera>(); feed = GetComponent<RovFpvCameraFeed>(); pilot=target==null?null:target.GetComponent<RovPilotInput>(); chaseFieldOfView = view.fieldOfView; }
        public bool PilotView => mode == ViewMode.Fpv;
        public ViewMode Mode => mode;
        public void SetView(ViewMode value) => mode = value;
        public void CycleView() => mode = (ViewMode)(((int)mode + 1) % 3);
        // Retain the two-view API for existing camera-effect checks.
        public void ToggleView() => mode = PilotView ? ViewMode.Chase : ViewMode.Fpv;
        public void Configure(Transform vehicle) => target = vehicle;
        private void LateUpdate()
        {
            if (target == null) return;
            if (!(pilot!=null && pilot.SuppressInput) && ((Keyboard.current?.vKey.wasPressedThisFrame ?? false) || (Gamepad.current?.selectButton.wasPressedThisFrame ?? false)))
                CycleView();
            bool pilotView = PilotView;
            view.orthographic = mode == ViewMode.TopDown;
            if (view.orthographic)
            {
                view.orthographicSize = 5f;
                view.ResetAspect();
                transform.SetPositionAndRotation(target.position + Vector3.up * 8f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
                return;
            }
            Vector3 offset = pilotView ? new Vector3(0, 0.04f, 0.31f) : new Vector3(1.1f, 0.65f, -1.4f);
            Vector3 position = target.TransformPoint(offset);
            if (feed != null)
            {
                view.fieldOfView = pilotView ? feed.VerticalFieldOfView : chaseFieldOfView;
                if (pilotView) view.aspect = (float)feed.Width / feed.Height; else view.ResetAspect();
            }
            if (pilotView) { transform.SetPositionAndRotation(position, target.rotation); return; }
            Vector3 look = pilotView ? target.position + target.forward * 3 : target.position;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position, Vector3.up));
        }
    }
}
