using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace ClubROV.Simulation
{
    public sealed class RovCourseAgent : Agent
    {
        private RovDynamics vehicle;
        private RovTrainingCourse course;
        private RovPilotInput pilot;
        private bool pilotWasEnabled, recordResults;
        private float previousDistance, elapsed;
        private int previousObjectives, previousCollisions;
        private RovCourseRandomizer randomizer;
        private float randomisationScale;

        public override void Initialize()
        {
            vehicle = GetComponent<RovDynamics>();
            course = GetComponent<RovTrainingCourse>();
            pilot = GetComponent<RovPilotInput>();
            if (vehicle.ExternalPhysics)
                throw new System.InvalidOperationException("Disconnect SITL before starting reinforcement learning.");
            pilotWasEnabled = pilot.enabled;
            pilot.enabled = false;
            recordResults = course.RecordResults;
            course.RecordResults = false;
            randomizer = new RovCourseRandomizer(GetComponent<RovTrainingMaps>().CopyLayouts()[0]);
            vehicle.PhysicsStepped += RewardStep;
            // RewardStep owns every termination so completion/objective stats are never skipped.
            MaxStep = 0;
        }

        private Vector3 Aim
        {
            get
            {
                Vector3 target = course.TargetPosition;
                if (!course.HoldingStage)
                    target += Quaternion.Euler(0, course.TargetHeading, 0) * Vector3.forward * 0.7f;
                return target;
            }
        }

        public override void OnEpisodeBegin()
        {
            if (vehicle == null) return;
            if (vehicle.ExternalPhysics) { enabled = false; return; }
            var ranges = new RovCourseRandomizer.Ranges(Academy.Instance.EnvironmentParameters.GetWithDefault);
            randomisationScale = ranges.Scale;
            randomizer.Randomize(ranges);
            vehicle.ResetVehicle();
            vehicle.Body.position += new Vector3(Random.Range(-ranges.StartLateral, ranges.StartLateral),
                Random.Range(-ranges.StartDepth, ranges.StartDepth), 0);
            vehicle.Body.rotation *= Quaternion.Euler(0, Random.Range(-ranges.StartHeading, ranges.StartHeading), 0);
            Physics.SyncTransforms();
            course.ResetCourse();
            previousObjectives = previousCollisions = 0;
            elapsed = 0;
            previousDistance = Vector3.Distance(vehicle.Body.position, Aim);
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            var body = vehicle.Body;
            Quaternion inverse = Quaternion.Inverse(body.rotation);
            sensor.AddObservation(Vector3.ClampMagnitude(inverse * (Aim - body.position) / 10, 1));
            sensor.AddObservation(Vector3.ClampMagnitude(inverse * body.linearVelocity / 2, 1));
            sensor.AddObservation(Vector3.ClampMagnitude(inverse * body.angularVelocity / 2, 1));
            sensor.AddObservation(inverse * Vector3.up);
            float headingError = Mathf.DeltaAngle(vehicle.Heading, course.TargetHeading) * Mathf.Deg2Rad;
            sensor.AddObservation(Mathf.Sin(headingError));
            sensor.AddObservation(Mathf.Cos(headingError));
            for (int i = 0; i < 6; i++) sensor.AddObservation(vehicle.Output(i));
            for (int stage = 0; stage < 5; stage++) sensor.AddObservation(course.ObjectivesCompleted == stage ? 1f : 0f);
            sensor.AddObservation(Mathf.Clamp01(course.HoldSeconds / 3));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (vehicle.ExternalPhysics) { vehicle.StopThrusters(); enabled = false; return; }
            var values = actions.ContinuousActions;
            vehicle.SetPilotCommand(new Vector3(values[0], values[1], values[2]), values[3], 0.8f);
        }

        public override void Heuristic(in ActionBuffers actions)
        {
            var values = actions.ContinuousActions;
            for (int i = 0; i < values.Length; i++) values[i] = 0;
        }

        private void RewardStep(float dt)
        {
            if (!isActiveAndEnabled || dt <= 0 || vehicle.ExternalPhysics) return;
            elapsed += dt;
            int objectives = course.ObjectivesCompleted;
            float distance = Vector3.Distance(vehicle.Body.position, Aim);
            if (objectives == previousObjectives)
                AddReward(Mathf.Clamp(previousDistance - distance, -0.2f, 0.2f) * 0.35f);
            else
                AddReward(2f * (objectives - previousObjectives));
            previousObjectives = objectives;
            previousDistance = distance;
            AddReward(-0.005f * dt);
            if (course.HoldingStage && course.CheckHold(vehicle.Body.position, vehicle.Body.rotation, vehicle.Body.linearVelocity).Ready)
                AddReward(0.2f * dt);
            if (course.CollisionCount > previousCollisions)
            {
                AddReward(-0.3f * (course.CollisionCount - previousCollisions));
                previousCollisions = course.CollisionCount;
            }
            if (course.Complete)
            {
                AddReward(5);
                FinishEpisode(true);
                return;
            }
            Vector3 position = vehicle.Body.position;
            if (elapsed > 150 || Mathf.Abs(position.x) > 10 || Mathf.Abs(position.z) > 12 || position.y > -0.1f || position.y < -3.4f)
            {
                AddReward(-1);
                FinishEpisode(false);
            }
        }

        private void FinishEpisode(bool completed)
        {
            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Course/Completed", completed ? 1 : 0);
            stats.Add("Course/Objectives", course.ObjectivesCompleted);
            stats.Add("Course/RandomisationScale", randomisationScale);
            EndEpisode();
        }

        protected override void OnDisable()
        {
            if (vehicle != null) { vehicle.PhysicsStepped -= RewardStep; vehicle.StopThrusters(); }
            randomizer?.Restore();
            if (vehicle != null && !vehicle.ExternalPhysics) vehicle.ResetVehicle();
            if (pilot != null) pilot.enabled = pilotWasEnabled;
            if (course != null) course.RecordResults = recordResults;
            base.OnDisable();
        }
    }
}
