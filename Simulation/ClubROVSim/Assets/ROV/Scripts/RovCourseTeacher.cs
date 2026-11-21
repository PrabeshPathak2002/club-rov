using Unity.InferenceEngine;
using UnityEngine;

namespace ClubROV.Simulation
{
    // TRAINING DEMONSTRATIONS ONLY. Privileged legacy policy proposes action labels;
    // it has no actuators, and is never instantiated in sensor-policy training/evaluation.
    public sealed class RovCourseTeacher : MonoBehaviour
    {
        private RovDynamics vehicle;
        private RovTrainingCourse course;
        private readonly float[] commands = new float[4];
        private Worker worker;
        private Transform[] landmarks;
        public float Command(int index) => commands[index];
        // Supervision labels only. Never appended to student observations.
        public float[] StateLabels()
        {
            var body = vehicle.Body;
            Vector3 offset = Quaternion.Inverse(body.rotation) * (course.TargetPosition - body.position);
            float error = Mathf.DeltaAngle(body.rotation.eulerAngles.y, course.TargetHeading) * Mathf.Deg2Rad;
            return new[] { offset.x, offset.y, offset.z, Mathf.Sin(error), Mathf.Cos(error),
                (float)Mathf.Clamp(course.ObjectivesCompleted, 0, 4) };
        }
        public void Configure(ModelAsset model, RovDynamics dynamics)
        {
            vehicle = dynamics;
            course = vehicle.GetComponent<RovTrainingCourse>();
            var layout = vehicle.GetComponent<RovTrainingMaps>().CopyLayouts()[0];
            landmarks = new[] { layout.gates[0], layout.gates[1], layout.gates[2], layout.hover, layout.dock };
            worker = new Worker(ModelLoader.Load(model), BackendType.CPU);
        }
        public void ResetLabels() => System.Array.Clear(commands, 0, commands.Length);
        public float[] LandmarkLabels()
        {
            var result = new float[25];
            var body = vehicle.Body;
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = Quaternion.Inverse(body.rotation) * (landmarks[i].position - body.position);
                float angle = Mathf.DeltaAngle(body.rotation.eulerAngles.y, landmarks[i].eulerAngles.y) * Mathf.Deg2Rad;
                result[i * 5] = offset.x; result[i * 5 + 1] = offset.y; result[i * 5 + 2] = offset.z;
                result[i * 5 + 3] = Mathf.Sin(angle); result[i * 5 + 4] = Mathf.Cos(angle);
            }
            return result;
        }
        // Collection-only position/velocity feedback for hold recoveries. This
        // never drives the sensor actor and does not replace the preserved
        // full-course learned teacher used by ordinary demonstrations.
        public void EvaluateHoldCorrection()
        {
            // EndEpisode requests a final observation after the judge marks a
            // dock complete; that terminal observation has no next command.
            if (course.Complete) { ResetLabels(); return; }
            if (!course.HoldingStage) { Evaluate(); return; }
            var body = vehicle.Body;
            Quaternion inverse = Quaternion.Inverse(body.rotation);
            Vector3 target = course.TargetPosition;
            Vector3 horizontal = target - body.position;
            horizontal.y = 0;
            if (course.HoverComplete && horizontal.magnitude > .2f) target += Vector3.up * .5f;
            Vector3 offset = inverse * (target - body.position);
            Vector3 velocity = inverse * body.linearVelocity;
            Vector3 translation = .55f * offset - 1.0f * velocity;
            for (int i = 0; i < 3; i++) commands[i] = Mathf.Clamp(translation[i], -.6f, .6f);
            float error = Mathf.DeltaAngle(body.rotation.eulerAngles.y, course.TargetHeading) * Mathf.Deg2Rad;
            commands[3] = Mathf.Clamp(.8f * error - .8f * (inverse * body.angularVelocity).y, -.5f, .5f);
        }
        public void Evaluate()
        {
            var values = new System.Collections.Generic.List<float>(26);
            void Add(Vector3 value) { values.Add(value.x); values.Add(value.y); values.Add(value.z); }
            var body = vehicle.Body;
            Quaternion inverse = Quaternion.Inverse(body.rotation);
            Vector3 target = course.TargetPosition;
            if (!course.HoldingStage) target += Quaternion.Euler(0, course.TargetHeading, 0) * Vector3.forward * 0.7f;
            Add(Vector3.ClampMagnitude(inverse * (target - body.position) / 10, 1));
            Add(Vector3.ClampMagnitude(inverse * body.linearVelocity / 2, 1));
            Add(Vector3.ClampMagnitude(inverse * body.angularVelocity / 2, 1));
            Add(inverse * Vector3.up);
            float error = Mathf.DeltaAngle(vehicle.Heading, course.TargetHeading) * Mathf.Deg2Rad;
            values.Add(Mathf.Sin(error)); values.Add(Mathf.Cos(error));
            for (int i = 0; i < 6; i++) values.Add(vehicle.Output(i));
            for (int i = 0; i < 5; i++) values.Add(course.ObjectivesCompleted == i ? 1f : 0f);
            values.Add(Mathf.Clamp01(course.HoldSeconds / 3));
            using var input = new Tensor<float>(new TensorShape(1, 26), values.ToArray());
            worker.Schedule(input);
            using var output = (worker.PeekOutput("deterministic_continuous_actions") as Tensor<float>).ReadbackAndClone();
            var labels = output.DownloadToArray();
            for (int i = 0; i < commands.Length; i++)
                commands[i] = float.IsFinite(labels[i]) ? Mathf.Clamp(labels[i], -1, 1) : 0;
        }
        private void OnDestroy()
        {
            worker?.Dispose();
            worker = null;
        }
    }
}
