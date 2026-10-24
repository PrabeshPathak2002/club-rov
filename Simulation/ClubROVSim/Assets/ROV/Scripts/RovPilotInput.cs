using UnityEngine;
using UnityEngine.InputSystem;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(RovDynamics))]
    public sealed class RovPilotInput : MonoBehaviour
    {
        [SerializeField, Range(0, 0.5f)] private float stickDeadZone = 0.15f;
        [SerializeField, Range(0.1f, 1)] private float translationSensitivity = 0.65f;
        [SerializeField, Range(0.1f, 1)] private float yawSensitivity = 0.5f;
        [SerializeField, Range(0, 1)] private float thrustLimit = 0.6f;
        private RovDynamics vehicle;
        public bool SuppressInput { get; set; }
        private void Awake() => vehicle = GetComponent<RovDynamics>();

        private void Update()
        {
            if(SuppressInput) { vehicle.StopThrusters(); return; }
            ReadDevices(Keyboard.current, Gamepad.current, out Vector3 command, out float yaw, out bool cut, out bool reset);
            if (reset) vehicle.ResetVehicle();
            if (!Application.isFocused || cut) { vehicle.StopThrusters(); return; }
            vehicle.SetPilotCommand(command, yaw, thrustLimit);
        }

        // Device sampling is separate from focus/physics so mappings can be verified safely.
        public void ReadDevices(Keyboard keys, Gamepad pad, out Vector3 command, out float yaw, out bool cut, out bool reset)
        {
            command = Vector3.zero;
            yaw = 0;
            cut = false;
            reset = false;
            if (keys != null)
            {
                reset = keys.homeKey.wasPressedThisFrame;
                cut = keys.spaceKey.isPressed;
                command.x = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
                command.z = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
                command.y = (keys.rKey.isPressed ? 1 : 0) - (keys.fKey.isPressed ? 1 : 0);
                yaw = (keys.eKey.isPressed ? 1 : 0) - (keys.qKey.isPressed ? 1 : 0);
            }
            if (pad != null)
            {
                // Read raw sticks so the configurable dead zone is applied exactly once.
                Vector2 move = ShapeStick(pad.leftStick.ReadUnprocessedValue(), stickDeadZone);
                Vector2 turn = pad.rightStick.ReadUnprocessedValue();
                command += new Vector3(move.x, ShapeAxis(turn.y, stickDeadZone), move.y);
                yaw += ShapeAxis(turn.x, stickDeadZone);
                reset |= pad.startButton.wasPressedThisFrame;
                cut |= pad.buttonSouth.isPressed;
            }
            command = Vector3.ClampMagnitude(command, 1) * translationSensitivity;
            yaw = Mathf.Clamp(yaw, -1, 1) * yawSensitivity;
        }
        public static float ShapeAxis(float value, float deadZone)
        {
            deadZone = Mathf.Clamp(deadZone, 0, 0.95f);
            return Mathf.Sign(value) * Mathf.Clamp01((Mathf.Abs(value) - deadZone) / (1 - deadZone));
        }
        public static Vector2 ShapeStick(Vector2 value, float deadZone)
        {
            float magnitude = value.magnitude;
            return magnitude < 0.00001f ? Vector2.zero : value / magnitude * ShapeAxis(magnitude, deadZone);
        }
        private void OnApplicationFocus(bool focused) { if (!focused && vehicle != null && !vehicle.ExternalPhysics) vehicle.StopThrusters(); }
        private void OnDisable() { if (vehicle != null) vehicle.StopThrusters(); }
    }
}
