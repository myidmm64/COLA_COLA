"""Original 80 BPM, 16-bar dark pizzeria cue. Rebuilds the two synchronized 48s music stems."""
from pathlib import Path
import wave
import numpy as np

RATE = 22050
DURATION = 48
BEAT = 0.75
COUNT = RATE * DURATION
ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'Assets/_COLA_COLA/Resources/Audio'


def hz(note):
    return 440 * 2 ** ((note - 69) / 12)


def write(name, stereo):
    peak = np.max(np.abs(stereo))
    # Keep composition dynamics; reserve headroom for rupture effects in the game mix.
    stereo = np.tanh(stereo * (0.70 / max(peak, 0.001))) * .88
    data = np.round(stereo * 32767).astype('<i2')
    with wave.open(str(DEST / name), 'wb') as target:
        target.setnchannels(2); target.setsampwidth(2); target.setframerate(RATE)
        target.writeframes(data.tobytes())
    print(f'{name}: {DURATION}s, peak={np.max(np.abs(stereo)):.3f}, rms={np.sqrt(np.mean(stereo**2)):.3f}')


def event(track, start, seconds, note, gain, pan, bell=False):
    length = int(seconds * RATE)
    t = np.arange(length) / RATE
    frequency = hz(note)
    if bell:
        sound = (np.sin(2*np.pi*frequency*t) * np.exp(-t*2.0) +
                 .22*np.sin(2*np.pi*frequency*2.007*t)*np.exp(-t*3.1) +
                 .08*np.sin(2*np.pi*frequency*4.13*t)*np.exp(-t*5))
        sound *= np.minimum(t/.012, 1)
    else:
        sound = np.sin(2*np.pi*frequency*t + .15*np.sin(2*np.pi*.7*t))
        sound += .20*np.sin(2*np.pi*frequency*2*t)
        sound *= (1-np.exp(-t*45))*np.exp(-t*5)
    sound *= np.minimum((seconds-t)/.08, 1) * gain
    indices = (int(start*RATE) + np.arange(length)) % COUNT
    np.add.at(track[:, 0], indices, sound * np.sqrt((1-pan)/2))
    np.add.at(track[:, 1], indices, sound * np.sqrt((1+pan)/2))


def compose():
    DEST.mkdir(parents=True, exist_ok=True)
    t = np.arange(COUNT)/RATE
    base = np.zeros((COUNT, 2), dtype=np.float64)
    tension = np.zeros_like(base)
    # D pedal, flattened second and unresolved fifth. Integer loop cycles avoid seam clicks.
    for note, amount, detune in [(26,.14,0), (38,.075,1), (45,.035,-1), (51,.018,2)]:
        cycle = round(hz(note)*DURATION)
        for channel in range(2):
            modulation = .6 + .22*np.sin(2*np.pi*(2+channel)*t/DURATION+.7*channel)
            phase = 2*np.pi*(cycle + channel*detune)*t/DURATION
            base[:, channel] += amount*np.sin(phase + .25*np.sin(2*np.pi*3*t/DURATION))*modulation
    # A damaged restaurant chime: sparse, restrained, with an unresolved response.
    melody = [(0,62), (2.5,69), (6,65), (10.5,63), (16,62), (19,57), (23,61),
              (32,62), (34.5,69), (38,65), (42.5,63), (48,57), (51,61), (57,63), (61,61)]
    for index, (beat, note) in enumerate(melody):
        event(base, beat*BEAT, 3.5, note, .09, (-.48 if index%2 else .48), True)
    # Circular stereo delay preserves the same 48-second wrap point.
    dry = base.copy()
    for delay, gain in [(.375,.22), (.75,.16), (1.5,.10)]:
        base += np.roll(dry[:, ::-1], int(delay*RATE), axis=0)*gain
    # Heartbeat pairs and a restrained mechanical ostinato enter through the runtime danger mix.
    for beat in range(64):
        event(tension, beat*BEAT, .48, 26, .23 if beat%4==0 else .13, 0)
        event(tension, beat*BEAT+.16, .24, 33, .075, .1)
        if beat%2 == 0:
            event(tension, beat*BEAT+.375, .38, [50,51,57,49][(beat//2)%4], .04, -.30 if beat%4 else .30)
    # Band-limited deterministic air texture; no external samples or borrowed melody.
    rng = np.random.default_rng(714023)
    bins = np.fft.rfftfreq(COUNT, 1/RATE)
    for channel in range(2):
        spectrum = rng.normal(size=bins.size)+1j*rng.normal(size=bins.size)
        spectrum *= np.exp(-((bins-950)/650)**2)
        texture = np.fft.irfft(spectrum, n=COUNT)
        texture /= max(np.std(texture), .0001)
        tension[:, channel] += texture*.009*(.5+.5*np.sin(2*np.pi*64*t/DURATION)**2)
    write('Pizzeria_Night_Base.wav', base)
    write('Pizzeria_Night_Tension.wav', tension)


if __name__ == '__main__':
    compose()
