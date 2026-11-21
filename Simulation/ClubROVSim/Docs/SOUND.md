# Underwater sound

CONTROLS → SOUND provides master, thruster and underwater ambience levels plus mute. Settings persist in PlayerPrefs under `sound.*`. Defaults: master 50%, thrusters 70%, ambience 30%, unmuted.

`RovAudio` installs once alongside `RovDynamics`, with two looping 2D sources using the scene's existing AudioListener. This is a pilot listening mix, consistently audible across chase, FPV and top-down views and all map layouts. No added listeners, mixer dependency or physics changes. HUD disable/re-enable does not add voices or reset the sound settings.

Motor sound follows the mean absolute applied output of all six thrusters, including health/derating and motor response. Reverse also contributes. Effort is smoothed; volume and pitch increase with effort, and idle reaches silence. The same observed outputs support local controls and SITL. The ambience fades in quietly. Sources stop on component disable and reuse the existing voices on re-enable; owned audio objects are destroyed with the component.

Both mono loops are original synthesized assets, generated reproducibly by `python Tools/generate-audio.py` using only the Python standard library. Integer cycle counts give periodic waveforms; PCM peaks leave headroom. These are stylized underwater rumble and motor sounds, not recordings or a calibrated acoustic model of the physical ROV. No external recordings or licenses are required.

Validation: Unity compiled; 18 audio checks passed for decoded signal, loop boundaries, source lifecycle, output-driven sound, reverse/failed motors and mute. The 32 menu/view checks passed with the sound controls installed. The SOUND foldout, readable numeric fields and mute interaction were inspected using native mouse input after a clean Play restart. Speaker/headphone tonal balance still needs user listening feedback. No standalone player build or fresh live SITL trial was performed for sound.
