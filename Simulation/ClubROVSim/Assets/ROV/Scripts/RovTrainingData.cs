using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ClubROV.Simulation
{
    [Serializable]
    public sealed class RovScenario
    {
        public string name = "Pool practice";
        public Vector3 startPosition = new(0, -1.2f, -1);
        public float startHeading;
        public Vector3 current;
        public float buoyancyPercent;
        public int failedThruster = -1;
        public float thrusterPercent = 0;
        public float visibility = 8;
        public float videoDelayMs;
        public float particles = 0.35f;
        public float light = 0.65f;
        public RovScenario Copy() => JsonUtility.FromJson<RovScenario>(JsonUtility.ToJson(this));
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 40) throw new ArgumentException("Use a scenario name of 1–40 characters.");
            foreach (float value in new[] {startPosition.x,startPosition.y,startPosition.z,startHeading,current.x,current.y,current.z,
                         buoyancyPercent,thrusterPercent,visibility,videoDelayMs,particles,light})
                if (!float.IsFinite(value)) throw new ArgumentException("Scenario contains a non-finite number.");
            if (Mathf.Abs(startPosition.x)>9 || Mathf.Abs(startPosition.z)>11 || startPosition.y> -0.2f || startPosition.y< -3.1f)
                throw new ArgumentException("Start must be inside the pool, 0.2–3.1 m deep.");
            if (current.magnitude>1.001f || Mathf.Abs(buoyancyPercent)>10 || failedThruster< -1 || failedThruster>5 ||
                thrusterPercent<0 || thrusterPercent>100 || visibility<1 || visibility>20 || videoDelayMs<0 || videoDelayMs>500 ||
                particles<0 || particles>1 || light<0 || light>1) throw new ArgumentException("Scenario settings exceed the supported range.");
        }
        public string Key()
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(this)))).Replace("-", "");
        }
    }
    [Serializable] public sealed class RovScenarioFile { public int version = 1; public List<RovScenario> scenarios = new(); }
    [Serializable] public sealed class RovRunResult
    {
        public string scenarioKey, scenarioName, completedUtc;
        public float seconds, depthRms, headingRms;
        public int collisions, score;
    }
    [Serializable] public sealed class RovRunFile { public int version = 1; public List<RovRunResult> runs = new(); public List<RovRunResult> bestRuns = new(); }
    public static class RovTrainingData
    {
        public static T Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 1024 * 1024) throw new IOException("Training file exceeds 1 MB.");
            return JsonUtility.FromJson<T>(File.ReadAllText(path)) ?? throw new IOException("Training file is invalid.");
        }
        public static void Write<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        public static int Score(float seconds, int collisions, float depthRms, float headingRms) =>
            Mathf.Max(0, Mathf.RoundToInt(1000 - 2 * seconds - 50 * collisions - 100 * depthRms - 2 * headingRms));
        public static RovRunFile AppendRun(RovRunFile history,RovRunResult run)
        {
            var next=new RovRunFile {runs=new List<RovRunResult>(history.runs),bestRuns=new List<RovRunResult>(history.bestRuns)};
            next.runs.Add(run); if(next.runs.Count>100) next.runs.RemoveAt(0);
            int best=next.bestRuns.FindIndex(r=>r.scenarioKey==run.scenarioKey);
            if(best<0) next.bestRuns.Add(run); else if(run.score>next.bestRuns[best].score) next.bestRuns[best]=run;
            if(next.bestRuns.Count>256) next.bestRuns.RemoveAt(0);
            return next;
        }
    }
}
