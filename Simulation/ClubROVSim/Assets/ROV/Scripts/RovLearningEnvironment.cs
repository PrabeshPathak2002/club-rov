using System.Collections;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace ClubROV.Simulation
{
    public static class RovLearningEnvironment
    {
        public const string BehaviorName = "PoolCourse";

        // mlagents-learn launches standalone players with --mlagents-port; normal player builds never receive it.
        public static bool LaunchedByTrainer => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--mlagents-port") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartInTrainingPlayer()
        {
            if (Application.isEditor || !LaunchedByTrainer || RovSensorLearningEnvironment.LaunchedForSensors) return;
            new GameObject("RL training bootstrap").AddComponent<Bootstrap>();
        }

        public static void Start(ModelAsset model = null)
        {
            var vehicle = Object.FindAnyObjectByType<RovDynamics>();
            if (vehicle == null || vehicle.ExternalPhysics)
                throw new System.InvalidOperationException("Open PoolTraining and disconnect SITL before training.");
            if (vehicle.GetComponent<RovCourseAgent>() != null) return;
            var sensor = vehicle.GetComponent<RovDvlSensor>();
            sensor?.StopStreaming();
            var maps = vehicle.GetComponent<RovTrainingMaps>();
            var settings = vehicle.GetComponent<RovTrainingSettings>();
            var scenario = new RovScenario();
            settings.Change(scenario);
            maps.Activate(0);
            vehicle.SetStartPose(scenario.startPosition, Quaternion.Euler(0, scenario.startHeading, 0));
            foreach (var hud in Object.FindObjectsByType<RovHud>()) hud.enabled = false;
            foreach (var camera in Object.FindObjectsByType<RovCameraRig>())
                camera.SetView(RovCameraRig.ViewMode.Chase);
            var audio = vehicle.GetComponent<RovAudio>();
            if (audio != null) audio.enabled = false;
            var behavior = vehicle.gameObject.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = BehaviorName;
            behavior.BrainParameters.VectorObservationSize = 26;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(4);
            if (model != null)
            {
                behavior.Model = model;
                behavior.BehaviorType = BehaviorType.InferenceOnly;
            }
            vehicle.gameObject.AddComponent<RovCourseAgent>();
            var requester = vehicle.gameObject.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = 5;
            requester.TakeActionsBetweenDecisions = true;
            Debug.Log("PoolCourse PPO environment active. Stop Play to leave the training environment.");
        }

        private sealed class Bootstrap : MonoBehaviour
        {
            // Wait one frame so scene components finish Start before the scenario is overridden.
            private IEnumerator Start()
            {
                yield return null;
                RovLearningEnvironment.Start();
                Destroy(gameObject);
            }
        }
    }
}
