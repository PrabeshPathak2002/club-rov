using UnityEngine;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(RovDynamics))]
    public sealed class RovTrainingCourse : MonoBehaviour
    {
        [SerializeField] private Transform[] gates;
        [SerializeField] private Transform hoverTarget;
        [SerializeField] private Transform dockTarget;
        private RovDynamics vehicle;
        private Vector3 previousPosition;
        private float holdSeconds;
        private float lastImpact = float.NegativeInfinity;
        public int GatesPassed { get; private set; }
        public int CollisionCount { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public bool Started { get; private set; }
        public bool HoverComplete { get; private set; }
        public bool Complete { get; private set; }
        public bool Configured => gates != null && gates.Length > 0 && hoverTarget != null && dockTarget != null;
        public float HoldSeconds => holdSeconds;
        public string Objective => !Configured ? "Course unavailable" : Complete ? "COURSE COMPLETE" :
            GatesPassed < gates.Length ? $"Gate {GatesPassed + 1}/{gates.Length} — cross toward the far end" :
            !HoverComplete ? "Hover — hold 3 s, heading 000°, speed < 0.15 m/s" :
            "Dock — hold 3 s, heading 180°, speed < 0.10 m/s";
        public Vector3 TargetPosition => !Configured ? transform.position : GatesPassed < gates.Length ?
            gates[GatesPassed].position : !HoverComplete ? hoverTarget.position : dockTarget.position;

        public void Configure(Transform[] courseGates, Transform hover, Transform dock)
        { gates = courseGates; hoverTarget = hover; dockTarget = dock; }
        private void OnEnable()
        {
            vehicle = GetComponent<RovDynamics>();
            vehicle.VehicleReset += ResetCourse;
            ResetCourse();
        }
        private void OnDisable() { if (vehicle != null) vehicle.VehicleReset -= ResetCourse; }
        public void Restart() => vehicle.ResetVehicle();
        public void ResetCourse()
        {
            GatesPassed = 0; CollisionCount = 0; ElapsedSeconds = 0; holdSeconds = 0;
            Started = false; HoverComplete = false; Complete = false;
            previousPosition = vehicle != null && vehicle.Body != null ? vehicle.Body.position : transform.position;
            lastImpact = float.NegativeInfinity;
        }
        private void Update()
        {
            if (vehicle.Body != null)
                EvaluatePose(vehicle.Body.position, vehicle.Body.rotation, vehicle.Body.linearVelocity, Time.deltaTime);
        }

        public void EvaluatePose(Vector3 position, Quaternion rotation, Vector3 velocity, float deltaSeconds)
        {
            if (!Configured || Complete) return;
            if (velocity.magnitude > 0.05f || Vector3.Distance(position, previousPosition) > 0.05f) Started = true;
            if (Started) ElapsedSeconds += Mathf.Max(0, deltaSeconds);
            if (GatesPassed < gates.Length)
            {
                Transform gate = gates[GatesPassed];
                Vector3 from = gate.InverseTransformPoint(previousPosition);
                Vector3 to = gate.InverseTransformPoint(position);
                if (from.z < 0 && to.z >= 0)
                {
                    Vector3 crossing = Vector3.Lerp(from, to, -from.z / (to.z - from.z));
                    // Inset the opening for the hull envelope, not just its center point.
                    if (Mathf.Abs(crossing.x) <= 0.57f && Mathf.Abs(crossing.y) <= 0.55f)
                        GatesPassed++;
                }
            }
            else
            {
                Transform target = HoverComplete ? dockTarget : hoverTarget;
                float headingError = Mathf.Abs(Mathf.DeltaAngle(rotation.eulerAngles.y, target.eulerAngles.y));
                Vector3 offset = position - target.position;
                bool inside = HoverComplete ? new Vector2(offset.x, offset.z).magnitude <= 0.4f && Mathf.Abs(offset.y) <= 0.18f : offset.magnitude <= 0.45f;
                bool steady = inside && headingError <= 15 && velocity.magnitude < (HoverComplete ? 0.1f : 0.15f);
                holdSeconds = steady ? holdSeconds + Mathf.Max(0, deltaSeconds) : 0;
                if (holdSeconds >= 3)
                {
                    if (HoverComplete) Complete = true;
                    else { HoverComplete = true; holdSeconds = 0; }
                }
            }
            previousPosition = position;
        }
        private void OnCollisionEnter(Collision collision) => RecordImpact(Time.time);
        public void RecordImpact(float time)
        {
            if (!Started || Complete || time - lastImpact < 0.4f) return;
            CollisionCount++; lastImpact = time;
        }
    }
}
