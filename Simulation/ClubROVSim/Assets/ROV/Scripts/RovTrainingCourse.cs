using UnityEngine;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(RovDynamics))]
    public sealed class RovTrainingCourse : MonoBehaviour
    {
        [SerializeField] private Transform[] gates;
        [SerializeField] private Transform hoverTarget;
        [SerializeField] private Transform dockTarget;
        [SerializeField] private Transform marker;
        [SerializeField] private bool freeSwim;
        private float orbitDegrees;
        private Vector3 orbitPrevious;
        public string PracticeObjective { get; set; } = "FREE SWIM | no scored objective";
        public bool MarkerRounded => Mathf.Abs(orbitDegrees)>=355;
        private RovDynamics vehicle;
        private Vector3 previousPosition;
        private float holdSeconds;
        private float lastImpact = float.NegativeInfinity;
        private double depthSquared, headingSquared, metricSeconds;
        private string scenarioKey="default:LOCAL", scenarioName="Pool practice";
        private RovRunFile history;
        private bool historyReady=true;
        private readonly RovRunRecorder recorder = new();
        private readonly float[] reviewOutputs = new float[6];
        public RovRunReview LastReview { get; private set; }
        public bool ReviewEnded { get; private set; }
        public bool CanEndPractice => recorder.Started && !Complete && !ReviewEnded;
        public float RecordedSeconds => recorder.Seconds;
        public event System.Action<RovRunReview> ReviewReady;
        public bool Qualified { get; private set; } = true;
        public bool RecordResults { get; set; } = true;
        public float DepthRms => metricSeconds>0 ? (float)System.Math.Sqrt(depthSquared/metricSeconds) : 0;
        public float HeadingRms => metricSeconds>0 ? (float)System.Math.Sqrt(headingSquared/metricSeconds) : 0;
        public int Score => RovTrainingData.Score(ElapsedSeconds,CollisionCount,DepthRms,HeadingRms);
        public int BestScore { get; private set; } = -1;
        public string SaveNotice { get; private set; } = "";
        public void SetRunConditions(string key, string name)
        {
            if (key==scenarioKey) return;
            if (Started || recorder.Started) Qualified=false;
            scenarioKey=key; scenarioName=name; RefreshBest();
        }
        private string HistoryPath => System.IO.Path.Combine(Application.persistentDataPath,"training-runs.json");
        public int GatesPassed { get; private set; }
        public int CollisionCount { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public bool Started { get; private set; }
        public bool HoverComplete { get; private set; }
        public bool Complete { get; private set; }
        public bool Configured => marker!=null || !freeSwim && gates != null && gates.Length > 0 && hoverTarget != null && dockTarget != null;
        public float HoldSeconds => holdSeconds;
        public int ResetRevision { get; private set; }
        public bool PillarCourse => marker != null;
        public int ObjectiveCount => !Configured ? 0 : marker != null ? 3 : gates.Length + 2;
        public int ObjectivesCompleted => !Configured ? 0 : Complete ? ObjectiveCount : marker != null
            ? (GatesPassed > 0 ? 1 : 0) + (MarkerRounded ? 1 : 0) : GatesPassed + (HoverComplete ? 1 : 0);
        public bool HoldingStage => Configured && marker == null && GatesPassed >= gates.Length && !Complete && !ReviewEnded;
        public float TargetHeading => !Configured ? 0 : marker != null ? (MarkerRounded ? 180 : 0)
            : GatesPassed < gates.Length ? gates[GatesPassed].eulerAngles.y : (HoverComplete ? dockTarget : hoverTarget).eulerAngles.y;
        public string ObjectiveStep(int index)
        {
            if (index < 0 || index >= ObjectiveCount) return "";
            if (marker != null) return index == 0 ? "Pass under the gate, heading 000°" : index == 1
                ? "Circle the pillar once; stay 0.6–3 m away" : "Return under the gate, heading 180°";
            if (index < gates.Length) return $"Gate {index + 1} · depth {-gates[index].position.y:0.00} m · heading {gates[index].eulerAngles.y:000}°";
            Transform aim = index == gates.Length ? hoverTarget : dockTarget;
            return $"{(index == gates.Length ? "Cyan hover ring" : "Green docking target")} · hold 3 s · depth {-aim.position.y:0.00} m · heading {aim.eulerAngles.y:000}°";
        }
        public string Briefing => !Configured ? "Explore and practise piloting. This map has no automatically judged objectives; use End practice & review when finished."
            : marker != null ? "Pass below the yellow bar, make one full circle around the pillar in one direction, then return under the bar. Stay submerged."
            : $"Fly through all {gates.Length} numbered yellow gates in order and in the indicated heading direction. Then hold in the cyan ring and finish at the green dock.";
        public string PilotInstruction
        {
            get
            {
                if (ReviewEnded) return "Practice ended. Restart the course to record another run.";
                if (Complete) return "All objectives complete! Open your run review or restart for another attempt.";
                if (!Configured) return "Free exploration · no automatic task completion on this map.";
                if (marker != null) return GatesPassed == 0 ? "Go UNDER the yellow bar toward heading 000°. Aim for 1.60 m depth."
                    : !MarkerRounded ? $"Circle the pillar in one direction. Keep 0.6–3 m horizontal distance · {Mathf.Min(360, Mathf.Abs(orbitDegrees)):0}/360°"
                    : "Return UNDER the yellow bar toward heading 180°. Aim for 1.60 m depth.";
                if (GatesPassed < gates.Length) return $"Pass THROUGH yellow Gate {GatesPassed + 1}, toward heading {TargetHeading:000}°. Aim for the centre, depth {-TargetPosition.y:0.00} m.";
                return HoverComplete ? $"Move to the GREEN dock, face {TargetHeading:000}°, and hold for 3 continuous seconds."
                    : $"Move inside the CYAN ring, face {TargetHeading:000}°, and hold for 3 continuous seconds.";
            }
        }
        public readonly struct HoldStatus
        {
            public readonly bool position, heading, speed;
            public bool Ready => position && heading && speed;
            public HoldStatus(bool position, bool heading, bool speed) { this.position = position; this.heading = heading; this.speed = speed; }
        }
        public HoldStatus CheckHold(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (!HoldingStage) return new HoldStatus(false, false, false);
            Transform aim = HoverComplete ? dockTarget : hoverTarget;
            Vector3 offset = position - aim.position;
            bool inside = HoverComplete ? new Vector2(offset.x, offset.z).magnitude <= 0.4f && Mathf.Abs(offset.y) <= 0.18f : offset.magnitude <= 0.45f;
            return new HoldStatus(inside, Mathf.Abs(Mathf.DeltaAngle(rotation.eulerAngles.y, aim.eulerAngles.y)) <= 15,
                velocity.magnitude < (HoverComplete ? 0.1f : 0.15f));
        }
        public string Objective => ReviewEnded ? "PRACTICE ENDED · restart for another run" : freeSwim ? PracticeObjective : marker!=null ? (Complete ? "GATE & PILLAR COMPLETE" : GatesPassed==0 ? "Pass under the gate bar" : !MarkerRounded ? $"Circle the pillar · {Mathf.Abs(orbitDegrees):0}/360°" : "Return under the gate bar") : !Configured ? "Course unavailable" : Complete ? "COURSE COMPLETE" :
            GatesPassed < gates.Length ? $"Gate {GatesPassed + 1}/{gates.Length} — cross toward the far end" :
            !HoverComplete ? "Hover — hold 3 s, heading 000°, speed < 0.15 m/s" :
            "Dock — hold 3 s, heading 180°, speed < 0.10 m/s";
        public Vector3 TargetPosition => !Configured ? transform.position : marker!=null ? (GatesPassed==0 || MarkerRounded ? gates[0].position+Vector3.down*0.6f : new Vector3(marker.position.x,-1.6f,marker.position.z)) : GatesPassed < gates.Length ?
            gates[GatesPassed].position : !HoverComplete ? hoverTarget.position : dockTarget.position;

        public void Configure(Transform[] courseGates, Transform hover, Transform dock)
        { gates = courseGates; hoverTarget = hover; dockTarget = dock; marker=null; freeSwim=false; }
        public void ConfigureMap(Transform[] courseGates, Transform hover, Transform dock, Transform pillar, bool empty)
        { Configure(courseGates,hover,dock); marker=pillar; freeSwim=empty; ResetCourse(); }
        private void OnEnable()
        {
            vehicle = GetComponent<RovDynamics>();
            vehicle.VehicleReset += ResetCourse;
            vehicle.PhysicsStepped += Step;
            try
            {
                history=RovTrainingData.Read<RovRunFile>(HistoryPath) ?? new RovRunFile();
                if(history.version!=1 || history.runs==null || history.runs.Count>100) throw new System.IO.IOException("Unsupported training history.");
                history.practiceReviews ??= new System.Collections.Generic.List<RovRunResult>();
                if (history.practiceReviews.Count > 20) throw new System.IO.IOException("Practice review list is too large.");
                if(history.bestRuns==null || history.bestRuns.Count==0) history.bestRuns=new System.Collections.Generic.List<RovRunResult>(history.runs);
                if(history.bestRuns.Count>256) throw new System.IO.IOException("Training best-score list is too large.");
            }
            catch(System.Exception error) { historyReady=false; SaveNotice="History preserved; saving disabled: " + error.Message; Debug.LogWarning(SaveNotice); }
            ResetCourse();
        }
        private void OnDisable() { if (vehicle != null) { vehicle.VehicleReset -= ResetCourse; vehicle.PhysicsStepped -= Step; } }
        public void Restart() => vehicle.ResetVehicle();
        // Unscored RL resets only. The normal judge and its hold tolerances are unchanged.
        public void BeginHoldPractice(bool dock)
        {
            if (RecordResults || !Configured || marker != null || freeSwim)
                throw new System.InvalidOperationException("Hold practice requires an unscored gate course.");
            ResetCourse();
            GatesPassed = gates.Length;
            HoverComplete = dock;
        }
        public void ResetCourse()
        {
            ResetRevision++;
            orbitDegrees=0; orbitPrevious=Vector3.zero;
            GatesPassed = 0; CollisionCount = 0; ElapsedSeconds = 0; holdSeconds = 0;
            Started = false; HoverComplete = false; Complete = false;
            ReviewEnded = false;
            depthSquared=headingSquared=metricSeconds=0; Qualified=true; RefreshBest();
            if(historyReady) SaveNotice="";
            previousPosition = vehicle != null && vehicle.Body != null ? vehicle.Body.position : transform.position;
            recorder.Reset(previousPosition);
            lastImpact = float.NegativeInfinity;
        }
        private void Step(float dt)
        {
            if (vehicle.Body != null)
                EvaluatePose(vehicle.Body.position, vehicle.Body.rotation, vehicle.Body.linearVelocity, dt);
        }

        public void EvaluatePose(Vector3 position, Quaternion rotation, Vector3 velocity, float deltaSeconds)
        {
            if (Complete || ReviewEnded) return;
            for (int i = 0; i < reviewOutputs.Length; i++) reviewOutputs[i] = vehicle == null ? 0 : vehicle.Output(i);
            bool movementStarted = Started || velocity.magnitude > 0.05f || Vector3.Distance(position, previousPosition) > 0.05f;
            recorder.Sample(position, velocity, deltaSeconds, reviewOutputs, !Configured || movementStarted);
            if (!Configured || Complete) return;
            if (velocity.magnitude > 0.05f || Vector3.Distance(position, previousPosition) > 0.05f) Started = true;
            if (Started) ElapsedSeconds += Mathf.Max(0, deltaSeconds);
            if(Started && deltaSeconds>0)
            {
                Transform aim=marker!=null ? gates[0] : GatesPassed<gates.Length ? gates[GatesPassed] : HoverComplete ? dockTarget : hoverTarget;
                float depthError=position.y-(marker!=null ? -1.6f : aim.position.y);
                float headingError=Mathf.DeltaAngle(rotation.eulerAngles.y,marker!=null && MarkerRounded ? 180 : aim.eulerAngles.y);
                depthSquared+=depthError*depthError*deltaSeconds; headingSquared+=headingError*headingError*deltaSeconds; metricSeconds+=deltaSeconds;
            }
            if(marker!=null)
            {
                EvaluateMarker(position); previousPosition=position; return;
            }
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
                bool steady = CheckHold(position, rotation, velocity).Ready;
                holdSeconds = steady ? holdSeconds + Mathf.Max(0, deltaSeconds) : 0;
                if (holdSeconds >= 3)
                {
                    if (HoverComplete) { Complete = true; SaveResult(); }
                    else { HoverComplete = true; holdSeconds = 0; }
                }
            }
            previousPosition = position;
        }
        private void EvaluateMarker(Vector3 position)
        {
            if(position.y> -0.2f) Qualified=false;
            var from=gates[0].InverseTransformPoint(previousPosition); var to=gates[0].InverseTransformPoint(position);
            bool outbound=from.z<0 && to.z>=0, inbound=from.z>0 && to.z<=0;
            if(outbound || inbound)
            {
                var hit=Vector3.Lerp(from,to,-from.z/(to.z-from.z));
                bool clear=Mathf.Abs(hit.x)<=0.77f && hit.y<= -0.25f && hit.y>= -2.1f;
                if(clear && GatesPassed==0 && outbound) {GatesPassed=1; orbitPrevious=Vector3.zero;}
                else if(clear && GatesPassed==1 && MarkerRounded && inbound) {Complete=true; SaveResult();}
            }
            if(GatesPassed!=1 || MarkerRounded) return;
            Vector3 radial=position-marker.position; radial.y=0;
            if(radial.magnitude<0.6f || radial.magnitude>3) {orbitPrevious=Vector3.zero; return;}
            if(orbitPrevious!=Vector3.zero)
            {
                float angle=Vector3.SignedAngle(orbitPrevious,radial,Vector3.up);
                if(Mathf.Abs(angle)<45) orbitDegrees+=angle;
            }
            orbitPrevious=radial;
        }
        private void OnCollisionEnter(Collision collision) => RecordImpact(recorder.Seconds,
            collision.contactCount > 0 ? collision.GetContact(0).point : (Vector3?)null);
        private void RefreshBest()
        {
            BestScore=-1;
            if(history==null) return;
            foreach(var run in history.bestRuns) if(run.scenarioKey==scenarioKey) BestScore=Mathf.Max(BestScore,run.score);
        }
        private void SaveResult()
        {
            PublishResult(true);
        }
        public void EndPracticeReview()
        {
            if (!CanEndPractice) return;
            ReviewEnded = true;
            PublishResult(false);
        }
        private void PublishResult(bool completed)
        {
            var run=new RovRunResult {scenarioKey=scenarioKey,scenarioName=scenarioName,completedUtc=System.DateTime.UtcNow.ToString("o"),
                seconds=recorder.Seconds,collisions=CollisionCount,depthRms=DepthRms,headingRms=HeadingRms,score=completed ? Score : 0};
            var previous = Qualified ? RovTrainingData.PreviousReview(history, scenarioKey, completed) : null;
            LastReview = recorder.Snapshot(run, previous, completed, Qualified, Configured, GetComponent<RovTrainingMaps>()?.Current ?? 0);
            if (Qualified && historyReady && RecordResults)
            {
                var next = completed ? RovTrainingData.AppendRun(history, run) : RovTrainingData.AppendPracticeReview(history, run);
                try { RovTrainingData.Write(HistoryPath,next); history=next; RefreshBest(); SaveNotice=completed ? "Run saved locally." : "Practice review saved locally."; }
                catch(System.Exception error) { SaveNotice="Run could not be saved: " + error.Message; Debug.LogWarning(SaveNotice); }
            }
            else if (!Qualified) SaveNotice = "Conditions/control changed · review only; no comparison or saved score.";
            ReviewReady?.Invoke(LastReview);
        }
        public void RecordImpact(float time, Vector3? contact = null)
        {
            if ((!Started && !recorder.Started) || Complete || ReviewEnded || time - lastImpact < 0.4f) return;
            CollisionCount++; lastImpact = time;
            recorder.Impact(contact ?? previousPosition);
        }
    }
}
