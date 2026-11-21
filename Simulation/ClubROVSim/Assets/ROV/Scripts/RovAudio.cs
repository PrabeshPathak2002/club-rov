using UnityEngine;

namespace ClubROV.Simulation
{
    [RequireComponent(typeof(RovDynamics))]
    public sealed class RovAudio : MonoBehaviour
    {
        private RovDynamics vehicle;
        private AudioSource motor, ambience;
        private GameObject voices;
        private float effort;
        public float Master { get; private set; }
        public float Motors { get; private set; }
        public float Ambience { get; private set; }
        public bool Muted { get; private set; }
        public float MotorLevel => motor == null ? 0 : motor.volume;
        public float MotorPitch => motor == null ? 0 : motor.pitch;

        private void Awake()
        {
            vehicle = GetComponent<RovDynamics>();
            Master = PlayerPrefs.GetFloat("sound.master", 0.5f);
            Motors = PlayerPrefs.GetFloat("sound.motors", 0.7f);
            Ambience = PlayerPrefs.GetFloat("sound.ambience", 0.3f);
            Muted = PlayerPrefs.GetInt("sound.muted", 0) != 0;
            voices = new GameObject("ROV underwater audio"); voices.transform.SetParent(transform, false);
            motor = Voice("RovMotor", 0.85f);
            ambience = Voice("RovAmbience", 1);
        }

        private AudioSource Voice(string resource, float pitch)
        {
            var source = voices.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = true; source.spatialBlend = 0;
            source.volume = 0; source.pitch = pitch; source.dopplerLevel = 0;
            source.clip = Resources.Load<AudioClip>(resource);
            if (source.clip == null) Debug.LogWarning("Missing simulation audio: " + resource);
            return source;
        }

        public void SetLevels(float master, float motors, float ambient, bool muted)
        {
            Master = Mathf.Clamp01(master); Motors = Mathf.Clamp01(motors); Ambience = Mathf.Clamp01(ambient); Muted = muted;
            PlayerPrefs.SetFloat("sound.master", Master); PlayerPrefs.SetFloat("sound.motors", Motors);
            PlayerPrefs.SetFloat("sound.ambience", Ambience); PlayerPrefs.SetInt("sound.muted", Muted ? 1 : 0);
            PlayerPrefs.Save();
            if (Muted) motor.volume = ambience.volume = 0;
        }

        public static float MeanOutput(RovDynamics dynamics)
        {
            float sum = 0;
            for (int i = 0; i < RovThrusterLayout.Count; i++) sum += Mathf.Abs(dynamics.Output(i));
            return sum / RovThrusterLayout.Count;
        }

        private void OnEnable()
        {
            if (motor?.clip != null) motor.Play();
            if (ambience?.clip != null) ambience.Play();
        }
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            effort = Mathf.MoveTowards(effort, MeanOutput(vehicle), dt * 3);
            float gain = Muted || Time.timeScale == 0 ? 0 : Master;
            motor.volume = gain * Motors * Mathf.Sqrt(effort);
            motor.pitch = Mathf.Lerp(0.65f, 1.7f, Mathf.Sqrt(effort));
            ambience.volume = Mathf.MoveTowards(ambience.volume, gain * Ambience * 0.45f, dt);
        }
        private void OnDisable()
        {
            motor?.Stop(); ambience?.Stop(); effort = 0;
            if (motor != null) motor.volume = 0;
            if (ambience != null) ambience.volume = 0;
        }
        private void OnDestroy() { if (voices != null) Destroy(voices); }
    }
}
