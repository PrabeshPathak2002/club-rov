using System;
using UnityEngine.UIElements;

namespace ClubROV.Simulation
{
    public sealed class RovSoundSettings : IDisposable
    {
        private readonly RovAudio audio;
        private readonly Slider master, motors, ambience;
        private readonly Toggle mute;
        public RovSoundSettings(VisualElement root, RovDynamics vehicle)
        {
            if (vehicle == null) return;
            audio = vehicle.GetComponent<RovAudio>();
            if (audio == null) audio = vehicle.gameObject.AddComponent<RovAudio>();
            master = root.Q<Slider>("sound-master"); motors = root.Q<Slider>("sound-motors");
            ambience = root.Q<Slider>("sound-ambience"); mute = root.Q<Toggle>("sound-mute");
            master.SetValueWithoutNotify(audio.Master * 100); motors.SetValueWithoutNotify(audio.Motors * 100);
            ambience.SetValueWithoutNotify(audio.Ambience * 100); mute.SetValueWithoutNotify(audio.Muted);
            master.RegisterValueChangedCallback(Changed); motors.RegisterValueChangedCallback(Changed);
            ambience.RegisterValueChangedCallback(Changed); mute.RegisterValueChangedCallback(Muted);
        }
        private void Changed(ChangeEvent<float> evt) => Apply();
        private void Muted(ChangeEvent<bool> evt) => Apply();
        private void Apply() => audio.SetLevels(master.value / 100, motors.value / 100, ambience.value / 100, mute.value);
        public void Dispose()
        {
            master?.UnregisterValueChangedCallback(Changed); motors?.UnregisterValueChangedCallback(Changed);
            ambience?.UnregisterValueChangedCallback(Changed); mute?.UnregisterValueChangedCallback(Muted);
        }
    }
}
