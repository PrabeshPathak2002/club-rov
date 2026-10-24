using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ClubROV.Simulation.Editor
{
    public static class RovControlChecks
    {
        [Serializable] private sealed class Check { public string name; public bool passed; }
        [Serializable] private sealed class Report
        {
            public string inputCoverage = "Synthetic Unity Input System keyboard/gamepad; not physical hardware.";
            public List<Check> checks = new();
        }

        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run control checks in Play Mode.");
            var pilot = UnityEngine.Object.FindAnyObjectByType<RovPilotInput>();
            var camera = UnityEngine.Object.FindAnyObjectByType<RovCameraRig>();
            if (pilot == null || camera == null) throw new InvalidOperationException("Open PoolTraining first.");
            Keyboard originalKeyboard = Keyboard.current;
            Gamepad originalGamepad = Gamepad.current;
            bool originalEnabled = pilot.enabled;
            bool originalView = camera.PilotView;
            Keyboard keyboard = null;
            Gamepad pad = null;
            var report = new Report();
            void Check(string name, bool pass) => report.checks.Add(new Check { name = name, passed = pass });
            try
            {
                pilot.enabled = false;
                keyboard = InputSystem.AddDevice<Keyboard>();
                pad = InputSystem.AddDevice<Gamepad>();
                void Keys(params Key[] keys)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                    InputSystem.Update();
                }
                void Pad(GamepadState state)
                {
                    InputSystem.QueueStateEvent(pad, state);
                    InputSystem.Update();
                }
                Keys(Key.W);
                pilot.ReadDevices(keyboard, null, out Vector3 move, out float yaw, out bool cut, out bool reset);
                Check("Keyboard forward and default sensitivity", move.z > 0.64f && move.z < 0.66f && move.x == 0 && move.y == 0 && yaw == 0 && !cut);
                Keys(Key.S, Key.A, Key.F, Key.Q);
                pilot.ReadDevices(keyboard, null, out move, out yaw, out cut, out reset);
                Check("Keyboard reverse, port, descend and left yaw", move.x < 0 && move.y < 0 && move.z < 0 && yaw < 0 && move.magnitude <= 0.651f);
                Keys(Key.D, Key.R, Key.E);
                pilot.ReadDevices(keyboard, null, out move, out yaw, out cut, out reset);
                Check("Keyboard starboard, ascend and right yaw", move.x > 0 && move.y > 0 && yaw > 0);
                Keys(Key.Space, Key.Home);
                pilot.ReadDevices(keyboard, null, out move, out yaw, out cut, out reset);
                Check("Keyboard cut and reset", cut && reset);
                Keys();
                Pad(new GamepadState { leftStick = new Vector2(0.05f, -0.05f), rightStick = new Vector2(0.05f, -0.05f) });
                pilot.ReadDevices(null, pad, out move, out yaw, out cut, out reset);
                Check("Gamepad neutral drift rejected", move == Vector3.zero && yaw == 0);
                Pad(new GamepadState { leftStick = Vector2.up, rightStick = new Vector2(1, -1) });
                pilot.ReadDevices(null, pad, out move, out yaw, out cut, out reset);
                Check("Gamepad forward, descend and right yaw", move.z > 0 && move.y < 0 && move.x == 0 && Mathf.Abs(yaw - 0.5f) < 0.001f);
                Pad(new GamepadState().WithButton(GamepadButton.South).WithButton(GamepadButton.Start));
                pilot.ReadDevices(null, pad, out move, out yaw, out cut, out reset);
                Check("Gamepad cut and reset", cut && reset);
                Pad(new GamepadState());
                Check("Dead-zone edge is zero", RovPilotInput.ShapeAxis(0.15f, 0.15f) == 0);
                Check("Full stick retains full scale", Mathf.Abs(RovPilotInput.ShapeAxis(-1, 0.15f) + 1) < 0.0001f);
                bool before = camera.PilotView;
                Keys(Key.V);
                camera.SendMessage("LateUpdate");
                Check("Keyboard camera switch", camera.PilotView != before);
                Check("Camera transform remains finite", float.IsFinite(camera.transform.position.sqrMagnitude));
                Keys();
                before = camera.PilotView;
                Pad(new GamepadState().WithButton(GamepadButton.Select));
                camera.SendMessage("LateUpdate");
                Check("Gamepad camera switch", camera.PilotView != before);
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (pad != null) InputSystem.RemoveDevice(pad);
                originalKeyboard?.MakeCurrent();
                originalGamepad?.MakeCurrent();
                if (camera.PilotView != originalView) camera.ToggleView();
                pilot.GetComponent<RovDynamics>().ResetVehicle();
                pilot.enabled = originalEnabled;
            }
            string path = Path.Combine(Application.dataPath, "../ControlCheckResults.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            int failures = report.checks.FindAll(check => !check.passed).Count;
            if (failures > 0) throw new InvalidOperationException($"{failures} control checks failed; inspect ControlCheckResults.json.");
            return $"{report.checks.Count} synthetic input/camera checks passed. No physical gamepad test performed.";
        }
    }
}
