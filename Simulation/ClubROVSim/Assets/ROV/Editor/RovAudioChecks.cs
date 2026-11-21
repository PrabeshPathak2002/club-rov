using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClubROV.Simulation.Editor
{
    public static class RovAudioChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run audio checks in Play Mode.");
            var checks = new List<string>();
            void Check(string name, bool pass) { if (!pass) throw new Exception(name); checks.Add("PASS: " + name); }
            foreach (string name in new[] { "RovMotor", "RovAmbience" })
            {
                var clip = Resources.Load<AudioClip>(name);
                Check(name + " loop is available and mono", clip != null && clip.channels == 1 && clip.length >= 1.9f);
                var data = new float[clip.samples];
                Check(name + " decoded PCM is readable", clip.GetData(data, 0));
                float peak = 0;
                foreach (float value in data) { if (!float.IsFinite(value)) throw new Exception("Nonfinite PCM"); peak = Mathf.Max(peak, Mathf.Abs(value)); }
                Check(name + " has audible signal and headroom", peak > 0.1f && peak < 0.95f);
                Check(name + " has continuous loop boundary", Mathf.Abs(data[0] - data[data.Length - 1]) < 0.1f);
            }
            var go = new GameObject("Temporary audio check");
            var body = go.AddComponent<Rigidbody>(); body.isKinematic = true;
            var dynamics = go.AddComponent<RovDynamics>();
            var audio = go.GetComponent<RovAudio>();
            float master = audio.Master, motors = audio.Motors, ambience = audio.Ambience; bool muted = audio.Muted;
            try
            {
                audio.SetLevels(0.5f, 0.7f, 0.3f, false);
                Check("One audio component and two playing loop voices", go.GetComponents<RovAudio>().Length == 1 && go.GetComponentsInChildren<AudioSource>().Length == 2);
                foreach (var source in go.GetComponentsInChildren<AudioSource>()) Check("Voice loops without camera-distance attenuation", source.loop && source.isPlaying && source.spatialBlend == 0);
                Check("Stopped thrusters produce zero motor effort", RovAudio.MeanOutput(dynamics) == 0);
                dynamics.ExternalPhysics = true;
                for (int i = 0; i < 6; i++) dynamics.SetActuator(i, 1);
                dynamics.ApplyForces(0.2f);
                for (int i = 0; i < 100; i++) audio.SendMessage("Update");
                float forward = RovAudio.MeanOutput(dynamics);
                Check("Actual motor output drives sound", forward > 0.99f && audio.MotorLevel > 0 && audio.MotorPitch > 0.65f);
                for (int i = 0; i < 6; i++) dynamics.SetThrusterHealth(i, 0);
                Check("Failed motors contribute no sound effort", RovAudio.MeanOutput(dynamics) == 0);
                for (int i = 0; i < 6; i++) { dynamics.SetThrusterHealth(i, 1); dynamics.SetActuator(i, -1); }
                dynamics.ApplyForces(0.3f);
                Check("Reverse motor outputs also produce sound effort", RovAudio.MeanOutput(dynamics) > 0.7f);
                audio.SetLevels(master, motors, ambience, true);
                Check("Mute immediately silences both voices", Array.TrueForAll(go.GetComponentsInChildren<AudioSource>(), source => source.volume == 0));
                audio.enabled = false; audio.enabled = true;
                Check("Re-enable reuses the two voices", go.GetComponentsInChildren<AudioSource>().Length == 2);
                audio.enabled = false;
                Check("Disable stops all voices", Array.TrueForAll(go.GetComponentsInChildren<AudioSource>(), source => !source.isPlaying));
            }
            finally { audio.SetLevels(master, motors, ambience, muted); UnityEngine.Object.DestroyImmediate(go); }
            return string.Join("\n", checks);
        }
    }
}
