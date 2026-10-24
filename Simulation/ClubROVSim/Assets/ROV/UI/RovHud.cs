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
        private Button cameraToggle;
        private RovCameraRig cameraRig;
        private RovFpvCameraFeed cameraFeed;
        private RovSitlBridge sitl;
        private Label sitlStatus;
        private Button sitlToggle;
        private TextField sitlAddress;
        private Label arming, score, runSave;
        private RovTrainingSettings training;
        private float nextRefresh;
        private RovGraphicsSettings graphics;
        public void Configure(RovDynamics dynamics) => vehicle = dynamics;
        public void ConfigureTraining(RovTrainingCourse training, RovWaterVisibility visibility) { course = training; water = visibility; }
        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            graphics = new RovGraphicsSettings(root);
            telemetry = root.Q<Label>("telemetry");
            arming=root.Q<Label>("arming"); score=root.Q<Label>("score");
            runSave=root.Q<Label>("run-save");
            training=vehicle==null?null:vehicle.GetComponent<RovTrainingSettings>();
            sitl = vehicle == null ? null : vehicle.GetComponent<RovSitlBridge>();
            sitlStatus = root.Q<Label>("sitl-status");
            sitlToggle = root.Q<Button>("sitl-toggle");
            sitlAddress = root.Q<TextField>("sitl-address");
            if (sitlAddress != null && sitl != null) sitlAddress.SetValueWithoutNotify(sitl.SimulatorAddress);
            if (sitlToggle != null) sitlToggle.clicked += ToggleSitl;
            thrusters = root.Q<Label>("thrusters");
            cameraMode = root.Q<Label>("camera-mode");
            cameraToggle=root.Q<Button>("camera-toggle");
            cameraRig=water==null?null:water.GetComponent<RovCameraRig>();
            if(cameraToggle!=null) cameraToggle.clicked+=ToggleCamera;
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
        private void OnVisibility(ChangeEvent<float> change)
        {
            if(training!=null) { var values=training.Current; values.visibility=change.newValue; training.Change(values); }
            else if(water!=null) water.SetVisibility(change.newValue);
        }
        private void Restart() { if (course != null) course.Restart(); else if (vehicle != null) vehicle.ResetVehicle(); }
        private void ToggleCamera() { if(cameraRig!=null) cameraRig.ToggleView(); }
        private void ToggleSitl()
        {
            if (sitl == null) return;
            if (sitl.Active) sitl.Disconnect();
            else
            {
                try { sitl.Configure(sitlAddress.value); sitl.Connect(); }
                catch (System.Exception error) { if (sitlStatus != null) sitlStatus.text = error.Message; Debug.LogWarning(error.Message); }
            }
        }
        private void OnDisable()
        {
            graphics?.Dispose(); graphics=null;
            visibilitySlider?.UnregisterValueChangedCallback(OnVisibility);
            if (restart != null) restart.clicked -= Restart;
            if (sitlToggle != null) sitlToggle.clicked -= ToggleSitl;
            if(cameraToggle!=null) cameraToggle.clicked-=ToggleCamera;
        }
        private void Update()
        {
            graphics?.Tick();
            if (vehicle == null || telemetry == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            if (sitl != null)
            {
                if(arming!=null)
                {
                    arming.text=sitl.ControlState;
                    arming.EnableInClassList("armed",sitl.Armed);
                    arming.EnableInClassList("disarmed",sitl.Active && sitl.TelemetryFresh && !sitl.Armed);
                }
                if (sitlStatus != null) sitlStatus.text = sitl.Status;
                if (sitlToggle != null) sitlToggle.text = sitl.Active ? "DISCONNECT SITL" : "CONNECT SITL · SIMULATION ONLY";
                sitlAddress?.SetEnabled(!sitl.Active);
                restart?.SetEnabled(!sitl.Active);
            }
            telemetry.text = $"DEPTH  {vehicle.Depth:0.00} m     SPEED  {vehicle.Speed:0.00} m/s     HEADING  {vehicle.Heading:000}°";
            thrusters.text = $"H1 {vehicle.Output(0):+0.00;-0.00;0.00}   H2 {vehicle.Output(1):+0.00;-0.00;0.00}   H3 {vehicle.Output(2):+0.00;-0.00;0.00}   H4 {vehicle.Output(3):+0.00;-0.00;0.00}   V1 {vehicle.Output(4):+0.00;-0.00;0.00}   V2 {vehicle.Output(5):+0.00;-0.00;0.00}";
            if (cameraMode != null && cameraFeed != null) cameraMode.text = cameraFeed.Active ?
                $"FPV · PI CAM STYLE · {cameraFeed.Width}×{cameraFeed.Height} / {cameraFeed.FramesPerSecond} fps" : "CHASE · native display";
            if(cameraToggle!=null && cameraRig!=null) cameraToggle.text=cameraRig.PilotView?"CAMERA · SWITCH TO CHASE":"CAMERA · SWITCH TO FPV";
            if (course != null && objective != null)
            {
                if(score!=null) score.text=!course.Configured ? "FREE SWIM | no scored course" : $"SCORE {course.Score} · BEST {(course.BestScore<0?"—":course.BestScore.ToString())} · {(course.Qualified?"SCORED":"PRACTICE")}\nDEPTH RMS {course.DepthRms:0.00} m · HEADING RMS {course.HeadingRms:0.0}°";
                if(runSave!=null) runSave.text=course.SaveNotice;
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
