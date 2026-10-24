using UnityEngine;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class RovHud : MonoBehaviour
    {
        [SerializeField] private RovDynamics vehicle;
        private Label telemetry;
        private Label thrusters;
        private float nextRefresh;
        public void Configure(RovDynamics dynamics) => vehicle = dynamics;
        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            telemetry = root.Q<Label>("telemetry");
            thrusters = root.Q<Label>("thrusters");
        }
        private void Update()
        {
            if (vehicle == null || telemetry == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            telemetry.text = $"DEPTH  {vehicle.Depth:0.00} m     SPEED  {vehicle.Speed:0.00} m/s     HEADING  {vehicle.Heading:000}°";
            thrusters.text = $"H1 {vehicle.Output(0):+0.00;-0.00;0.00}   H2 {vehicle.Output(1):+0.00;-0.00;0.00}   H3 {vehicle.Output(2):+0.00;-0.00;0.00}   H4 {vehicle.Output(3):+0.00;-0.00;0.00}   V1 {vehicle.Output(4):+0.00;-0.00;0.00}   V2 {vehicle.Output(5):+0.00;-0.00;0.00}";
        }
    }
}
