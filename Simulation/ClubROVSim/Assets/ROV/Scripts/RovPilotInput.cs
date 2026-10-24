using UnityEngine;
using UnityEngine.InputSystem;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(RovDynamics))]
    public sealed class RovPilotInput : MonoBehaviour
    {
        private RovDynamics vehicle;
        private void Awake() => vehicle = GetComponent<RovDynamics>();

        private void Update()
        {
            Vector3 command = Vector3.zero;
            float yaw = 0;
            Keyboard keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.homeKey.wasPressedThisFrame) vehicle.ResetVehicle();
                command.x = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
                command.z = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
                command.y = (keys.rKey.isPressed ? 1 : 0) - (keys.fKey.isPressed ? 1 : 0);
                yaw = (keys.eKey.isPressed ? 1 : 0) - (keys.qKey.isPressed ? 1 : 0);
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 move = pad.leftStick.ReadValue();
                Vector2 turn = pad.rightStick.ReadValue();
                command += new Vector3(move.x, turn.y, move.y);
                yaw += turn.x;
                if (pad.startButton.wasPressedThisFrame) vehicle.ResetVehicle();
            }
            if (!Application.isFocused || (keys != null && keys.spaceKey.isPressed) || (pad != null && pad.buttonSouth.isPressed))
            {
                vehicle.StopThrusters();
                return;
            }
            vehicle.SetPilotCommand(command, yaw);
        }
        private void OnApplicationFocus(bool focused) { if (!focused && vehicle != null) vehicle.StopThrusters(); }
        private void OnDisable() { if (vehicle != null) vehicle.StopThrusters(); }
    }
}
