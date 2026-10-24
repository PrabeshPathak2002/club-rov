using UnityEngine;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RovDynamics : MonoBehaviour
    {
        [Header("Provisional values - tune against the physical ROV")]
        [SerializeField, Min(0.001f)] private float displacedVolumeM3 = 0.008f;
        [SerializeField, Min(1)] private float waterDensity = 1000;
        [SerializeField, Min(0.01f)] private float fullThrustNewtons = 15;
        [SerializeField, Range(0.1f, 1)] private float reverseThrustRatio = 0.8f;
        [SerializeField, Min(0.01f)] private float motorResponseSeconds = 0.15f;
        [SerializeField] private Vector3 centerOfBuoyancy = new(0, 0.035f, 0);
        [SerializeField] private Vector3 quadraticDrag = new(8, 12, 9);
        [SerializeField] private Vector3 angularDrag = new(2, 3, 2);
        [SerializeField] private Vector3 currentVelocity;
        [SerializeField] private float surfaceY;
        [SerializeField, Min(0.01f)] private float hullHeight = 0.3f;

        private readonly float[] desired = new float[RovThrusterLayout.Count];
        private readonly float[] actual = new float[RovThrusterLayout.Count];
        private Rigidbody body;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private readonly float[] health = { 1, 1, 1, 1, 1, 1 };
        private float buoyancyBias;
        public Vector3 CurrentVelocity => currentVelocity;
        public float BuoyancyBias => buoyancyBias;
        public event System.Action<float> PhysicsStepped;
        public float ThrusterHealth(int index) => health[index];
        public void SetCurrent(Vector3 velocity) => currentVelocity = Vector3.ClampMagnitude(velocity, 1);
        public void SetBuoyancyBias(float fraction) => buoyancyBias = Mathf.Clamp(fraction, -0.1f, 0.1f);
        public void SetThrusterHealth(int index, float fraction) => health[index] = Mathf.Clamp01(fraction);
        public void SetStartPose(Vector3 position, Quaternion rotation)
        {
            if (ExternalPhysics) throw new System.InvalidOperationException("Disconnect SITL before changing the start pose.");
            startPosition = position; startRotation = rotation; ResetVehicle();
        }
        public void CompletePhysicsStep(float dt) => PhysicsStepped?.Invoke(dt);
        public float Depth => Mathf.Max(0, surfaceY - transform.position.y);
        public float Speed => body == null ? 0 : body.linearVelocity.magnitude;
        public float Heading => transform.eulerAngles.y;
        public Rigidbody Body => body;
        public bool ExternalPhysics { get; set; }
        public event System.Action VehicleReset;
        public float Output(int i) => actual[i] * health[i];

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.maxAngularVelocity = 1.2f;
            startPosition = body.position;
            startRotation = body.rotation;
        }

        public void SetPilotCommand(Vector3 translation, float yaw, float thrustLimit = 1)
        {
            if (ExternalPhysics) return;
            RovThrusterLayout.Mix(Vector3.ClampMagnitude(translation, 1), Mathf.Clamp(yaw, -1, 1), desired);
            // Mix in force space: honor the weaker reverse limit without introducing
            // an unrequested translation during yaw or strafe. Outputs are force fractions.
            float scale = 1;
            for (int i = 0; i < desired.Length; i++)
                if (desired[i] < 0) scale = Mathf.Max(scale, -desired[i] / reverseThrustRatio);
            for (int i = 0; i < desired.Length; i++) desired[i] *= Mathf.Clamp01(thrustLimit) / scale;
        }

        public void SetActuator(int index, float command)
        {
            if (!ExternalPhysics) return;
            command = Mathf.Clamp(command, -1, 1);
            desired[index] = command < 0 ? command * reverseThrustRatio : command;
        }

        public void StopThrusters()
        {
            System.Array.Clear(desired, 0, desired.Length);
            System.Array.Clear(actual, 0, actual.Length);
        }

        public void ResetVehicle()
        {
            if (ExternalPhysics) return; // Restart the SITL session together with its physics origin.
            StopThrusters();
            body.position = startPosition;
            body.rotation = startRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            VehicleReset?.Invoke();
        }

        private void OnDisable() => StopThrusters();

        private void FixedUpdate()
        {
            if (!ExternalPhysics) { CompletePhysicsStep(Time.fixedDeltaTime); ApplyForces(Time.fixedDeltaTime); }
        }

        public void ApplyForces(float stepSeconds)
        {
            float wetFraction = Mathf.Clamp01((surfaceY - (body.position.y - hullHeight * 0.5f)) / hullHeight);
            Vector3 buoyancy = -Physics.gravity * (waterDensity * displacedVolumeM3 * (1 + buoyancyBias) * wetFraction);
            body.AddForceAtPosition(buoyancy, transform.TransformPoint(centerOfBuoyancy));

            Vector3 relative = transform.InverseTransformDirection(body.linearVelocity - currentVelocity);
            Vector3 drag = new(-quadraticDrag.x * relative.x * Mathf.Abs(relative.x),
                -quadraticDrag.y * relative.y * Mathf.Abs(relative.y),
                -quadraticDrag.z * relative.z * Mathf.Abs(relative.z));
            body.AddRelativeForce(drag * wetFraction);
            Vector3 spin = transform.InverseTransformDirection(body.angularVelocity);
            body.AddRelativeTorque(-Vector3.Scale(angularDrag, spin) * wetFraction);

            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = Mathf.MoveTowards(actual[i], desired[i], stepSeconds / motorResponseSeconds);
                float thrust = actual[i] * health[i] * fullThrustNewtons;
                body.AddForceAtPosition(transform.TransformDirection(RovThrusterLayout.Direction(i)) * (thrust * wetFraction),
                    transform.TransformPoint(RovThrusterLayout.Position(i)));
            }
        }
    }
}
