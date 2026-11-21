using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace ClubROV.Simulation
{
    public sealed class RovSensorCourseAgent : Agent
    {
        private RovDynamics vehicle;
        private RovTrainingCourse course;
        private RovPilotInput pilot;
        private RovDvlSensor measurements;
        private readonly float[] commands = new float[4], observations = new float[RovSensorObservationState.Count];
        private bool pilotWasEnabled, recordResults;
        private float previousDistance, elapsed;
        private int previousObjectives, previousCollisions;
        private RovCourseRandomizer randomizer;
        private float randomisationScale;
        private int practiceKind;
        private float previousHoldPotential;
        private bool previousHolding;
        public RovCourseTeacher Teacher { get; set; }
        public bool EmitCorrectionLabels { get; set; }
        public Camera ObservationCamera { get; set; }
        public int StopAfterEpisodes { get; set; }
        public string SummaryPath { get; set; }
        private int episodes, completed;
        private bool quitting;

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
            measurements = GetComponent<RovDvlSensor>();
            measurements.StartLocalSampling(Random.Range(1, int.MaxValue));
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
            float hover = Parameter("practice_hover_probability", 0, 0, 1);
            float dock = Parameter("practice_dock_probability", 0, 0, 1);
            float total = Mathf.Max(1, hover + dock);
            float draw = hover + dock > 0 ? Random.value : 1;
            practiceKind = draw < hover / total ? 1 : draw < (hover + dock) / total ? 2 : 0;
            if (practiceKind != 0)
            {
                course.BeginHoldPractice(practiceKind == 2);
                float radius = Parameter("practice_position_radius", .6f, 0, 2);
                float depth = Parameter("practice_depth_radius", .15f, 0, .5f);
                float heading = Parameter("practice_heading_range", 30, 0, 180);
                float approach = Parameter("practice_approach_distance", 0, 0, 4);
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 position = course.TargetPosition + new Vector3(offset.x, Random.Range(-depth, depth), offset.y);
                position.x = Mathf.Clamp(position.x, -8.5f, 8.5f);
                position.y = Mathf.Clamp(position.y, -3.2f, -.3f);
                position.z = Mathf.Clamp(position.z, -10.8f, 10.8f);
                vehicle.Body.position = position;
                vehicle.Body.rotation = Quaternion.Euler(0, course.TargetHeading + Random.Range(-heading, heading), 0);
                if (approach > 0)
                {
                    var pose = VisiblePracticePose(course.TargetPosition, course.TargetHeading,
                        approach, radius, depth, heading, ObservationCamera,
                        practiceKind == 2 ? Parameter("practice_dock_clearance", .5f, 0, 1) : 0);
                    vehicle.Body.position = pose.position;
                    vehicle.Body.rotation = pose.rotation;
                    Academy.Instance.StatsRecorder.Add("Practice/TargetVisibleAtReset", 1);
                }
                Physics.SyncTransforms();
            }
            if (Teacher != null) Teacher.ResetLabels();
            // Teleports must update the camera transform before the first
            // decision, rather than waiting for a rendered/interpolated pose.
            vehicle.transform.SetPositionAndRotation(vehicle.Body.position, vehicle.Body.rotation);
            Physics.SyncTransforms();
            System.Array.Clear(commands, 0, commands.Length);
            previousObjectives = course.ObjectivesCompleted;
            previousCollisions = 0;
            elapsed = 0;
            previousDistance = Vector3.Distance(vehicle.Body.position, Aim);
            previousHolding = course.HoldingStage;
            previousHoldPotential = HoldPotential();
        }

        private static float Parameter(string name, float fallback, float minimum, float maximum)
        {
            float value = Academy.Instance.EnvironmentParameters.GetWithDefault(name, fallback);
            if (!float.IsFinite(value)) throw new System.ArgumentException("Nonfinite practice parameter: " + name);
            return Mathf.Clamp(value, minimum, maximum);
        }

        // Reset-only privileged geometry. The policy receives only the resulting
        // camera image and sensor packets, with a fresh recurrent memory.
        public static Pose VisiblePracticePose(Vector3 target, float heading, float approach,
            float radius, float depth, float headingJitter, Camera view, float clearance = 0)
        {
            if (view == null) throw new System.InvalidOperationException("Practice approach requires the body camera.");
            Vector3 anchor = target - Quaternion.Euler(0, heading, 0) * Vector3.forward * approach;
            // Enter the dock from above its perimeter rail. The lateral offset
            // leaves enough camera range when the pool wall clips the rear approach.
            if (clearance > 0) anchor += Vector3.up * clearance + Vector3.right * 2;
            for (int attempt = 0; attempt <= 8; attempt++)
            {
                float scale = attempt == 8 ? 0 : Mathf.Pow(.5f, attempt);
                Vector2 offset = Random.insideUnitCircle * radius * scale;
                Vector3 position = anchor + new Vector3(offset.x, Random.Range(-depth, depth) * scale, offset.y);
                position.x = Mathf.Clamp(position.x, -8.5f, 8.5f);
                position.y = Mathf.Clamp(position.y, -3.2f, -.3f);
                position.z = Mathf.Clamp(position.z, -10.8f, 10.8f);
                Vector3 direction = target - position;
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + Random.Range(-headingJitter, headingJitter) * scale;
                Quaternion rotation = Quaternion.Euler(0, yaw, 0);
                Vector3 local = Quaternion.Inverse(rotation * view.transform.localRotation) *
                    (target - position - rotation * view.transform.localPosition);
                float halfHeight = local.z * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad / 2) * .9f;
                if (local.z > view.nearClipPlane && Mathf.Abs(local.y) <= halfHeight && Mathf.Abs(local.x) <= halfHeight * view.aspect)
                    return new Pose(position, rotation);
            }
            throw new System.InvalidOperationException("No in-pool practice pose with target in the body-camera frustum.");
        }

        public static float HoldPotential(float headingError, float speed, float distance)
            => .25f * Mathf.Cos(headingError * Mathf.Deg2Rad) +
               .25f * Mathf.Exp(-4 * Mathf.Max(0, speed)) * Mathf.Clamp01(1 - distance / 2);

        private float HoldPotential() => course.HoldingStage ? HoldPotential(
            Mathf.DeltaAngle(vehicle.Heading, course.TargetHeading), vehicle.Body.linearVelocity.magnitude,
            Vector3.Distance(vehicle.Body.position, course.TargetPosition)) : 0;

        public override void CollectObservations(VectorSensor sensor)
        {
            RovSensorObservationState.Write(measurements.LatestImu, measurements.LatestDvl,
                measurements.SampleSeconds, measurements.SensorOffsetFlu, observations);
            sensor.AddObservation(observations);
            var stats = Academy.Instance.StatsRecorder;
            // Explicit collection mode only: labels use truth but travel outside
            // actor observations. Python chooses student/expert rollout actions.
            if (EmitCorrectionLabels)
            {
                if (Teacher == null) throw new System.InvalidOperationException("Correction collection requires a teacher.");
                Teacher.EvaluateHoldCorrection();
                for (int i = 0; i < 4; i++) stats.Add("Correction/Command" + i, Teacher.Command(i));
                var labels = Teacher.StateLabels();
                for (int i = 0; i < labels.Length; i++) stats.Add("Correction/State" + i, labels[i]);
                var landmarkLabels = Teacher.LandmarkLabels();
                for (int i = 0; i < landmarkLabels.Length; i++) stats.Add("Correction/Landmark" + i, landmarkLabels[i]);
            }
            stats.Add("Sensor/TeacherActive", Teacher == null ? 0 : 1);
            stats.Add("Sensor/ImuValid", observations[15]);
            stats.Add("Sensor/DvlValid", observations[11]);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (quitting) { vehicle.StopThrusters(); return; }
            if (vehicle.ExternalPhysics) { vehicle.StopThrusters(); enabled = false; return; }
            var values = actions.ContinuousActions;
            for (int i = 0; i < commands.Length; i++) commands[i] = float.IsFinite(values[i]) ? Mathf.Clamp(values[i], -1, 1) : 0;
            vehicle.SetPilotCommand(new Vector3(commands[0], commands[1], commands[2]), commands[3], 0.8f);
        }

        public override void Heuristic(in ActionBuffers actions)
        {
            if (Teacher != null) Teacher.Evaluate();
            var values = actions.ContinuousActions;
            for (int i = 0; i < values.Length; i++) values[i] = Teacher == null ? 0 : Teacher.Command(i);
        }

        private void RewardStep(float dt)
        {
            if (!isActiveAndEnabled || quitting || dt <= 0 || vehicle.ExternalPhysics) return;
            elapsed += dt;
            int objectives = course.ObjectivesCompleted;
            float distance = Vector3.Distance(vehicle.Body.position, Aim);
            float potential = HoldPotential();
            // Bounded potential differences cannot pay for circling or camping.
            // Skip stage boundaries; targets and judge state never enter actor inputs.
            if (course.HoldingStage && previousHolding && objectives == previousObjectives)
                AddReward(potential - previousHoldPotential);
            previousHoldPotential = potential;
            previousHolding = course.HoldingStage;
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
            if (practiceKind == 1 && course.HoverComplete)
            {
                AddReward(5); // Completing the practice goal beats repeatedly breaking a nearly-finished hold.
                FinishEpisode(true);
                return;
            }
            Vector3 position = vehicle.Body.position;
            float timeout = practiceKind == 0 ? 150 : Parameter("practice_timeout_seconds", 30, 5, 150);
            if (elapsed > timeout || Mathf.Abs(position.x) > 10 || Mathf.Abs(position.z) > 12 || position.y > -0.1f || position.y < -3.4f)
            {
                AddReward(-1);
                FinishEpisode(false);
            }
        }

        private void FinishEpisode(bool completed)
        {
            var stats = Academy.Instance.StatsRecorder;
            if (practiceKind == 0)
            {
                stats.Add("Course/Completed", completed ? 1 : 0);
                stats.Add("Course/Objectives", course.ObjectivesCompleted);
                stats.Add("Course/RandomisationScale", randomisationScale);
            }
            else
            {
                stats.Add(practiceKind == 1 ? "Practice/HoverCompleted" : "Practice/DockCompleted", completed ? 1 : 0);
                stats.Add("Practice/TargetDistance", Vector3.Distance(vehicle.Body.position, course.TargetPosition));
                stats.Add("Practice/HeadingError", Mathf.Abs(Mathf.DeltaAngle(vehicle.Heading, course.TargetHeading)));
            }
            stats.Add("Sensor/TeacherActive", Teacher == null ? 0 : 1);
            episodes++;
            if (completed) this.completed++;
            EndEpisode();
            if (StopAfterEpisodes > 0 && episodes >= StopAfterEpisodes && !quitting)
                StartCoroutine(FinishCollection());
        }

        private System.Collections.IEnumerator FinishCollection()
        {
            quitting = true;
            vehicle.StopThrusters();
            yield return null; // Let the terminal demonstration record flush before closing.
            var recorder = GetComponent<Unity.MLAgents.Demonstrations.DemonstrationRecorder>();
            if (recorder != null) { recorder.Record = false; recorder.Close(); }
            if (!string.IsNullOrEmpty(SummaryPath))
                System.IO.File.WriteAllText(SummaryPath, JsonUtility.ToJson(new Summary {
                    mode = Teacher == null ? "sensor_policy" : "teacher_demonstrations",
                    episodes = episodes, completed = completed }, true));
            Application.Quit(0);
        }
        [System.Serializable] private class Summary { public string mode; public int episodes, completed; }

        protected override void OnDisable()
        {
            if (vehicle != null) { vehicle.PhysicsStepped -= RewardStep; vehicle.StopThrusters(); }
            randomizer?.Restore();
            if (vehicle != null && !vehicle.ExternalPhysics) vehicle.ResetVehicle();
            if (measurements != null) measurements.StopLocalSampling();
            if (pilot != null) pilot.enabled = pilotWasEnabled;
            if (course != null) course.RecordResults = recordResults;
            base.OnDisable();
        }
    }
}
