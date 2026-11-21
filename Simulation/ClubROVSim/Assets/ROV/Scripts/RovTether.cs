using UnityEngine;
using UnityEngine.Rendering;

namespace ClubROV.Simulation
{
    // Lightweight training approximation, not a cable/connector strength or acoustic model.
    public sealed class RovTether : MonoBehaviour
    {
        public const float DefaultLength = 30.48f;
        [SerializeField] private float diameterMetres = 0.00569f;
        [SerializeField] private float massKgPerMetre = 0.033f;
        [SerializeField] private float cylinderDragCoefficient = 1.2f;
        [SerializeField] private float springNewtonsPerMetre = 80;
        [SerializeField] private float dampingNewtonSecondsPerMetre = 12;
        [SerializeField] private float maximumTensionNewtons = 80;
        [SerializeField] private Vector3 attachmentLocal = new(0, 0.075f, -0.14f);
        private RovDynamics vehicle;
        private RovTrainingMaps maps;
        private LineRenderer line;
        private Material lineMaterial;
        private readonly Vector3[] points = new Vector3[33];
        private Bounds pool;
        private bool anchored, simulationEnabled = true;
        private float cableLength = DefaultLength, dragScale = 1, weightScale, paidOut;
        private double nextDraw;
        public bool SimulationEnabled => simulationEnabled;
        public float LengthMetres => cableLength;
        public float PaidOutMetres => paidOut;
        public Vector3 Anchor { get; private set; }
        public Vector3 LastForce { get; private set; }
        public float TensionNewtons { get; private set; }
        public float DistanceMetres { get; private set; }
        public bool Ready => anchored;
        public string Status => !simulationEnabled ? "TETHER OFF" : !anchored ? "TETHER · waiting for pool"
            : TensionNewtons > 0.1f ? "TETHER TAUT" : DistanceMetres > cableLength - 2 ? "TETHER · near length limit" : "TETHER · slack / auto payout";

        private void Awake() { vehicle = GetComponent<RovDynamics>(); maps = GetComponent<RovTrainingMaps>(); }
        private void OnEnable()
        {
            vehicle = GetComponent<RovDynamics>(); maps = GetComponent<RovTrainingMaps>();
            if (vehicle != null) vehicle.VehicleReset += ResetAnchor;
            if (maps != null && maps.Current >= 0) ResetAnchor();
        }
        private void Start() => ResetAnchor();
        public void Configure(bool active, float length, float drag, float weight)
        {
            if (!float.IsFinite(length) || !float.IsFinite(drag) || !float.IsFinite(weight)
                || length < 3 || length > 60 || drag < 0 || drag > 3 || weight < 0 || weight > 3)
                throw new System.ArgumentOutOfRangeException(nameof(length), "Tether settings exceed training limits.");
            simulationEnabled = active; cableLength = length; dragScale = drag; weightScale = weight;
            paidOut = Mathf.Min(paidOut, cableLength);
            if (!active) { LastForce = Vector3.zero; TensionNewtons = 0; }
            if (line != null) line.enabled = active && anchored && isActiveAndEnabled;
            if (!anchored && maps != null && maps.Current >= 0) ResetAnchor();
        }
        public void ResetAnchor()
        {
            anchored = false; LastForce = Vector3.zero; TensionNewtons = 0; DistanceMetres = 0;
            if (maps == null || maps.Current < 0 || vehicle == null || vehicle.Body == null) return;
            var layout = maps.CopyLayouts()[maps.Current];
            var environment = layout.environment != null ? layout.environment : layout.root;
            foreach (var renderer in environment.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name != "Pool floor" && !renderer.name.StartsWith("Arena floor ")) continue;
                pool = renderer.bounds;
                Anchor = NearestEdge(vehicle.StartPosition, pool);
                DistanceMetres = Vector3.Distance(Anchor, Attachment());
                paidOut = Mathf.Min(cableLength, DistanceMetres + 2);
                anchored = true; EnsureLine();
                return;
            }
        }
        public static Vector3 NearestEdge(Vector3 start, Bounds floor)
        {
            float left = floor.min.x + 0.2f, right = floor.max.x - 0.2f;
            float near = floor.min.z + 0.2f, far = floor.max.z - 0.2f;
            Vector3 result = new(Mathf.Clamp(start.x, left, right), 0.12f, Mathf.Clamp(start.z, near, far));
            float best = Mathf.Abs(start.x - left); result.x = left;
            if (Mathf.Abs(start.x - right) < best) { best = Mathf.Abs(start.x - right); result.x = right; }
            if (Mathf.Abs(start.z - near) < best) { best = Mathf.Abs(start.z - near); result.x = Mathf.Clamp(start.x, left, right); result.z = near; }
            if (Mathf.Abs(start.z - far) < best) { result.x = Mathf.Clamp(start.x, left, right); result.z = far; }
            return result;
        }
        private Vector3 Attachment() => vehicle.Body.position + vehicle.Body.rotation * attachmentLocal;

        public readonly struct Load
        {
            public readonly Vector3 force;
            public readonly float tension;
            public Load(Vector3 force, float tension) { this.force = force; this.tension = tension; }
        }
        public static Load CalculateLoad(Vector3 anchor, Vector3 attachment, Vector3 endpointVelocity, Vector3 waterCurrent,
            float length, float deployed, float diameter, float linearMass, float dragFactor, float weightFactor,
            float coefficient = 1.2f, float spring = 80, float damping = 12, float maximumTension = 80, float waterDensity = 1000)
        {
            Vector3 radial = attachment - anchor;
            float distance = radial.magnitude;
            if (distance < 0.0001f) return new Load(Vector3.zero, 0);
            radial /= distance;
            float tension = distance >= length ? Mathf.Clamp(spring * (distance - length)
                + damping * Vector3.Dot(endpointVelocity, radial), 0, maximumTension) : 0;
            float wet = 0;
            for (int i = 0; i < 12; i++) if (Vector3.Lerp(anchor, attachment, (i + 0.5f) / 12).y < 0) wet += 1f / 12;
            float wetLength = Mathf.Min(deployed, length) * wet;
            // Average cable speed ~half the endpoint speed. Transverse drag shared with the bank endpoint.
            Vector3 relative = waterCurrent - endpointVelocity * 0.5f;
            Vector3 transverse = relative - radial * Vector3.Dot(relative, radial);
            Vector3 drag = 0.25f * waterDensity * coefficient * diameter * wetLength * dragFactor * transverse.magnitude * transverse;
            float netWeight = 9.81f * wetLength * (linearMass - waterDensity * Mathf.PI * diameter * diameter / 4) * weightFactor * 0.5f;
            return new Load(-radial * tension + Vector3.ClampMagnitude(drag, 40) + Vector3.down * Mathf.Clamp(netWeight, -10, 10), tension);
        }

        // Called exactly once by RovDynamics for each local or externally owned physics step.
        public void ApplyForces(float dt)
        {
            if (!isActiveAndEnabled || !simulationEnabled || !anchored || !(dt > 0) || vehicle.Body == null)
            { LastForce = Vector3.zero; TensionNewtons = 0; return; }
            Vector3 attachment = Attachment();
            DistanceMetres = Vector3.Distance(Anchor, attachment);
            // Pay out slack automatically, but do not reel it in when returning toward shore.
            paidOut = Mathf.Min(cableLength, Mathf.Max(paidOut, DistanceMetres + 2));
            var load = CalculateLoad(Anchor, attachment, vehicle.Body.GetPointVelocity(attachment), vehicle.CurrentVelocity,
                cableLength, paidOut, diameterMetres, massKgPerMetre, dragScale, weightScale,
                cylinderDragCoefficient, springNewtonsPerMetre, dampingNewtonSecondsPerMetre, maximumTensionNewtons, vehicle.WaterDensity);
            LastForce = load.force; TensionNewtons = load.tension;
            vehicle.Body.AddForceAtPosition(LastForce, attachment);
        }
        private void EnsureLine()
        {
            if (line != null) { line.enabled = simulationEnabled; return; }
            var material = Resources.Load<Material>("TetherLine");
            if (material == null) { Debug.LogWarning("Missing tether line material."); return; }
            var visual = new GameObject("Ethernet tether visual") { layer = 2 };
            visual.transform.SetParent(transform, false);
            line = visual.AddComponent<LineRenderer>();
            lineMaterial = new Material(material); line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true; line.positionCount = points.Length; line.widthMultiplier = diameterMetres * 1.5f;
            line.numCornerVertices = 2; line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.enabled = simulationEnabled;
        }
        private void LateUpdate()
        {
            if (line == null || !line.enabled || !anchored || Time.realtimeSinceStartupAsDouble < nextDraw) return;
            nextDraw = Time.realtimeSinceStartupAsDouble + 0.05;
            Vector3 end = Attachment(); float distance = Vector3.Distance(Anchor, end);
            float sag = Mathf.Sqrt(Mathf.Max(0, paidOut * paidOut - distance * distance)) * 0.25f;
            Vector3 bow = Vector3.ClampMagnitude(new Vector3(vehicle.CurrentVelocity.x, 0, vehicle.CurrentVelocity.z) * sag * 0.2f, 0.5f);
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1), curve = 4 * t * (1 - t);
                Vector3 point = Vector3.Lerp(Anchor, end, t) + (Vector3.down * sag + bow) * curve;
                if (i > 0 && i < points.Length - 1)
                {
                    point.y = Mathf.Max(pool.max.y + 0.025f, point.y);
                    point.x = Mathf.Clamp(point.x, pool.min.x + 0.2f, pool.max.x - 0.2f);
                    point.z = Mathf.Clamp(point.z, pool.min.z + 0.2f, pool.max.z - 0.2f);
                }
                points[i] = point;
            }
            line.SetPositions(points);
        }
        private void OnDisable()
        {
            if (vehicle != null) vehicle.VehicleReset -= ResetAnchor;
            LastForce = Vector3.zero; TensionNewtons = 0;
            if (line != null) line.enabled = false;
        }
        private void OnDestroy()
        {
            if (line != null) Destroy(line.gameObject);
            if (lineMaterial != null) Destroy(lineMaterial);
        }
    }
}
