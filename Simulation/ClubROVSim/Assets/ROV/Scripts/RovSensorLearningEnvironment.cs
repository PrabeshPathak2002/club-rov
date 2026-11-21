using System;
using System.Collections;
using System.IO;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Demonstrations;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ClubROV.Simulation
{
    public static class RovSensorLearningEnvironment
    {
        public const string BehaviorName = "PoolCourseSensors";
        public const int ImageWidth = 84, ImageHeight = 64;
        public static bool Demonstrations => HasArgument("--rov-sensor-demo");
        public static bool Corrections => HasArgument("--rov-sensor-corrections");
        public static bool LaunchedForSensors => Demonstrations || Corrections || HasArgument("--rov-rl-sensors");
        private static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
        public static string Argument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapPlayer()
        {
            if (!Application.isEditor && LaunchedForSensors)
                new GameObject("Sensor RL bootstrap").AddComponent<Bootstrap>();
        }

        public static void Start(bool demonstration = false)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Sensor RL needs rendered camera images; omit --no-graphics/-nographics.");
            Application.runInBackground = true;
            var vehicle = Object.FindAnyObjectByType<RovDynamics>();
            if (vehicle == null || vehicle.ExternalPhysics || vehicle.GetComponent<RovCourseAgent>() != null ||
                vehicle.GetComponent<RovSensorCourseAgent>() != null)
                throw new InvalidOperationException("Sensor RL needs a local, unclaimed PoolTraining vehicle.");
            vehicle.GetComponent<RovDvlSensor>().StopStreaming();
            var scenario = new RovScenario();
            vehicle.GetComponent<RovTrainingSettings>().Change(scenario);
            vehicle.GetComponent<RovTrainingMaps>().Activate(0);
            vehicle.SetStartPose(scenario.startPosition, Quaternion.Euler(0, scenario.startHeading, 0));
            foreach (var hud in Object.FindObjectsByType<RovHud>()) hud.enabled = false;
            foreach (var camera in Object.FindObjectsByType<Camera>()) camera.enabled = false;
            var audio = vehicle.GetComponent<RovAudio>();
            if (audio != null) audio.enabled = false;

            var cameraObject = new GameObject("RL body-mounted RGB camera");
            cameraObject.transform.SetParent(vehicle.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0.04f, 0.31f);
            var view = cameraObject.AddComponent<Camera>();
            float hfov = Academy.Instance.EnvironmentParameters.GetWithDefault("sensor_camera_hfov", 48.7f);
            if (!float.IsFinite(hfov) || hfov < 10 || hfov > 120) throw new ArgumentOutOfRangeException("sensor_camera_hfov");
            view.aspect = (float)ImageWidth / ImageHeight;
            view.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(hfov * Mathf.Deg2Rad / 2) / view.aspect) * Mathf.Rad2Deg;
            view.nearClipPlane = 0.03f; view.farClipPlane = 100;
            view.allowHDR = false; view.allowMSAA = false; view.enabled = false;
            var additional = view.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = false; additional.renderShadows = false;

            RovCourseTeacher teacher = null;
            if (demonstration || Corrections)
            {
                UnityEngine.Random.InitState(int.Parse(Argument("--rov-demo-seed", "731")));
                var model = Resources.Load<ModelAsset>("SensorCourseTeacher");
                if (model == null) throw new InvalidOperationException("Build with the preserved teacher model before collecting demonstrations.");
                var proxy = new GameObject("Training-only privileged teacher");
                proxy.transform.SetParent(vehicle.transform, false);
                teacher = proxy.AddComponent<RovCourseTeacher>();
                teacher.Configure(model, vehicle);
                Time.timeScale = 20; Application.targetFrameRate = -1; Application.runInBackground = true;
            }

            var behavior = vehicle.gameObject.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = BehaviorName;
            behavior.BrainParameters.VectorObservationSize = RovSensorObservationState.Count;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(4);
            if (demonstration) behavior.BehaviorType = BehaviorType.HeuristicOnly;
            var image = vehicle.gameObject.AddComponent<CameraSensorComponent>();
            image.SensorName = "00_body_rgb"; image.Camera = view;
            image.Width = ImageWidth; image.Height = ImageHeight; image.Grayscale = false;
            image.CompressionType = SensorCompressionType.PNG;
            image.RuntimeCameraEnable = false;
            var agent = vehicle.gameObject.AddComponent<RovSensorCourseAgent>();
            agent.Teacher = teacher;
            agent.EmitCorrectionLabels = Corrections;
            agent.ObservationCamera = view;
            if (demonstration)
            {
                string folder = Path.GetFullPath(Argument("--rov-demo-output", Path.Combine(Application.persistentDataPath, "sensor-demos")));
                Directory.CreateDirectory(folder);
                agent.StopAfterEpisodes = int.Parse(Argument("--rov-demo-episodes", "100"));
                if (agent.StopAfterEpisodes < 1) throw new ArgumentOutOfRangeException("--rov-demo-episodes");
                agent.SummaryPath = Path.Combine(folder, "teacher-summary.json");
                var recorder = vehicle.gameObject.AddComponent<DemonstrationRecorder>();
                recorder.DemonstrationName = "SensorCourse";
                recorder.DemonstrationDirectory = folder; recorder.Record = true;
            }
            var requester = vehicle.gameObject.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = 5; requester.TakeActionsBetweenDecisions = true;
            Debug.Log($"{BehaviorName}: RGB {ImageWidth}x{ImageHeight}x3 + {RovSensorObservationState.Count} sensor scalars; teacher={demonstration}.");
        }

        private sealed class Bootstrap : MonoBehaviour
        {
            private IEnumerator Start()
            {
                yield return null;
                RovSensorLearningEnvironment.Start(Demonstrations);
                Destroy(gameObject);
            }
        }
    }
}
