using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RovHud : MonoBehaviour
    {
        [SerializeField] private RovDynamics vehicle;
        [SerializeField] private RovTrainingCourse course;
        [SerializeField] private RovWaterVisibility water;
        private Label telemetry;
        private Label thrusters;
        private Label objective;
        private Label run;
        private Label target;
        private Label visibilityText;
        private Slider visibilitySlider;
        private Button restart;
        private Label cameraMode;
        private RovFpvCameraFeed cameraFeed;
        private float nextRefresh;
        public void Configure(RovDynamics dynamics) => vehicle = dynamics;
        public void ConfigureTraining(RovTrainingCourse training, RovWaterVisibility visibility) { course = training; water = visibility; }
        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            telemetry = root.Q<Label>("telemetry");
            thrusters = root.Q<Label>("thrusters");
            cameraMode = root.Q<Label>("camera-mode");
            if (water != null) cameraFeed = water.GetComponent<RovFpvCameraFeed>();
            objective = root.Q<Label>("objective"); run = root.Q<Label>("run"); target = root.Q<Label>("target");
            visibilityText = root.Q<Label>("visibility-text");
            visibilitySlider = root.Q<Slider>("visibility");
            restart = root.Q<Button>("restart");
            if (visibilitySlider != null && water != null)
            {
                visibilitySlider.SetValueWithoutNotify(water.VisibilityMetres);
                visibilitySlider.RegisterValueChangedCallback(OnVisibility);
            }
            if (restart != null) restart.clicked += Restart;
        }
        private void OnVisibility(ChangeEvent<float> change) { if (water != null) water.SetVisibility(change.newValue); }
        private void Restart() { if (course != null) course.Restart(); else if (vehicle != null) vehicle.ResetVehicle(); }
        private void OnDisable()
        {
            visibilitySlider?.UnregisterValueChangedCallback(OnVisibility);
            if (restart != null) restart.clicked -= Restart;
        }
        private void Update()
        {
            if (vehicle == null || telemetry == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            telemetry.text = $"DEPTH  {vehicle.Depth:0.00} m     SPEED  {vehicle.Speed:0.00} m/s     HEADING  {vehicle.Heading:000}°";
            thrusters.text = $"H1 {vehicle.Output(0):+0.00;-0.00;0.00}   H2 {vehicle.Output(1):+0.00;-0.00;0.00}   H3 {vehicle.Output(2):+0.00;-0.00;0.00}   H4 {vehicle.Output(3):+0.00;-0.00;0.00}   V1 {vehicle.Output(4):+0.00;-0.00;0.00}   V2 {vehicle.Output(5):+0.00;-0.00;0.00}";
            if (cameraMode != null && cameraFeed != null) cameraMode.text = cameraFeed.Active ?
                $"FPV · PI CAM STYLE · {cameraFeed.Width}×{cameraFeed.Height} / {cameraFeed.FramesPerSecond} fps" : "CHASE · native display";
            if (course != null && objective != null)
            {
                objective.text = course.Objective;
                run.text = $"TIME {System.TimeSpan.FromSeconds(course.ElapsedSeconds):mm\\:ss\\.f}    COLLISIONS {course.CollisionCount}    HOLD {Mathf.Min(3, course.HoldSeconds):0.0}/3 s";
                Vector3 delta = course.TargetPosition - vehicle.transform.position;
                float bearing = Mathf.Repeat(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 360);
                target.text = course.Complete ? "Finished — restart for another run" :
                    $"TARGET {delta.magnitude:0.0} m · bearing {bearing:000}° · depth {-course.TargetPosition.y:0.00} m";
            }
            if (visibilityText != null && water != null) visibilityText.text = $"VISIBILITY  {water.VisibilityMetres:0.0} m · visual approximation";
        }
    }
}
