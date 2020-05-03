# MicGate

MicGate is a digital noise gate (https://en.wikipedia.org/wiki/Noise_gate) to your microphone. Some audio card drivers and VOIP software include this feature, but there are many configurations without an adjustable noise gate. E.g. almost any game with many audio cards.

## How does it work?

Usually audio goes from microphone to the software reading it and then to the speakers. MicGate uses a virtual microphone (see installation instructions below) to modify the audio before it gets passed to the software reading it. See flow chart below.

![Audio flow](/Images/readme_audio_flow.png)

The noise gate configuration parameters (threshold, attack, hold, decay) refer to the same things as in the Wikipedia article (https://en.wikipedia.org/wiki/Noise_gate). Except that threshold is not compared against the absolute audio volume, but the volume integral over the duration of attack phase (200 ms by default). To make it easier to figure out a correct value for this, the user interface shows

1. the integral
2. the threshold
3. the data from the real microphone to the virtual one

The screenshot below shows the blue line (integral) crossing the red line (threshold), which in effect passes the real microphone input (green, up) to the virtual output (green, down).

![Treshold passing](/Images/readme_threshold_above.png)

The screenshot below shows that small noises (e.g. mouse and mechanical keyboard clicks) do not pass the red threshold and thus nothing is passed to the virtual output.

![Treshold passing](/Images/readme_threshold_below.png)

## How to install and use?

1. Pre-requisite: install VB-CABLE (or similar) to pass audio from a virtual speaker to a virtual microphone (https://www.vb-audio.com/Cable/index.htm).
2. Download, extract and launch MicGate.
3. Open the settins page (⚙ icon).
4. Set the real microphone and virtual microphone in the Device settings. Settings are saved automatically.
5. In the main view (〜 icon), check when the blue line crosses the red line. If it happens too often, increase threshold (Noise gate settings in the settings page). If the blue line never crosses the red threshold when you speak, decrease the threshold.

## Anything else?

I will only implement features I will personally use or fix bugs I can reproduce. But I will gladly accept pull requests if you would like to commit improvements.
