"""Still Table: a restrained, regular 80 BPM suspense loop and pizzeria room assets."""
import argparse
from pathlib import Path
import uuid
import wave
import numpy as np

RATE = 22050
BPM = 80
BEAT = 60 / BPM
DURATION = 64 * BEAT
COUNT = round(RATE * DURATION)
ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'Assets/_COLA_COLA/Resources/Audio'


def write(name, audio, ceiling=.46):
    audio = np.tanh(audio * (.70 / max(np.max(np.abs(audio)), .001))) * (ceiling / np.tanh(.70))
    with wave.open(str(DEST / name), 'wb') as target:
        target.setnchannels(1 if audio.ndim == 1 else 2)
        target.setsampwidth(2)
        target.setframerate(RATE)
        target.writeframes(np.round(audio * 32767).astype('<i2').tobytes())
    meta = DEST / (name + '.meta')
    if not meta.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'cola-cola/pizzeria/audio/' + name).hex
        meta.write_text(f'''fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 22050
    compressionFormat: 1
    quality: 0.85
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: {1 if audio.ndim == 1 else 0}
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
''', encoding='utf-8')
    print(f'Authored {name}: {len(audio) / RATE:.2f}s', flush=True)


def place(track, beat, sound, pan=0):
    indices = (round(beat * BEAT * RATE) + np.arange(len(sound))) % len(track)
    np.add.at(track[:, 0], indices, sound * np.sqrt((1-pan)/2))
    np.add.at(track[:, 1], indices, sound * np.sqrt((1+pan)/2))


def band_noise(seconds, low, high, seed):
    count = round(seconds * RATE)
    rng = np.random.default_rng(seed)
    bins = np.fft.rfftfreq(count, 1/RATE)
    spectrum = rng.normal(size=len(bins)) + 1j*rng.normal(size=len(bins))
    band = np.exp(-((bins-(low+high)/2)/((high-low)/2))**2)
    band[bins < 20] = 0
    sound = np.fft.irfft(spectrum*band, n=count)
    return sound/max(np.std(sound), .0001)


def frequency(midi):
    return 440 * 2**((midi-69)/12)


def soft_voice(midi, seconds, attack=.12, release=.5):
    """Warm, stable harmonics and slow edges; no noise, detune or metallic partials."""
    t = np.arange(round(seconds*RATE))/RATE
    f = frequency(midi)
    voice = np.sin(2*np.pi*f*t)+.12*np.sin(2*np.pi*f*2*t)+.045*np.sin(2*np.pi*f*3*t)
    rise = np.clip(t/attack,0,1)
    fall = np.clip((seconds-t)/release,0,1)
    envelope = (rise*rise*(3-2*rise))*(fall*fall*(3-2*fall))
    return voice*envelope*np.exp(-t*.26)


def pulse():
    seconds = BEAT*.70
    t = np.arange(round(seconds*RATE))/RATE
    # One identical soft pulse per beat. No pitch drop or sharp clock attack.
    return soft_voice(50,seconds,.045,.18)*np.sin(np.pi*t/seconds)**2


def room_reflections(track):
    # Short and quiet: avoid the layered ringing of a long reverberant tail.
    dry = track.copy()
    for index,(seconds,gain) in enumerate(((.043,.065),(.079,.040),(.137,.022))):
        reflected = dry[:,::-1] if index%2 else dry
        track += np.roll(reflected,round(seconds*RATE),axis=0)*gain


def compose():
    DEST.mkdir(parents=True, exist_ok=True)
    base = np.zeros((COUNT,2))
    tension = np.zeros_like(base)
    # The same two-bar D-minor figure, on exact half-note beats, repeats eight times.
    # Musical tension comes from the unresolved final E, not dense or random sounds.
    motif = (50,57,53,52)
    beat_sound = pulse()
    for phrase in range(8):
        at = phrase*8
        place(base,at,soft_voice(38,BEAT*8.4,.30,.70)*.13,0)
        place(base,at,soft_voice(53,BEAT*8.3,.55,.75)*.026,-.10)
        for index,midi in enumerate(motif):
            place(base,at+index*2,soft_voice(midi,BEAT*2.5)*.105,0)
            # Pressure adds only a faint, matching upper voice; tempo and density stay fixed.
            place(tension,at+index*2,soft_voice(midi+12,BEAT*2.5,.18,.6)*.09,.10)
        for beat in range(8):
            place(base,at+beat,beat_sound*.035,0)
            place(tension,at+beat,beat_sound*.025,0)
    for name,track,ceiling in (('Pizzeria_Night_Base.wav',base,.50),('Pizzeria_Night_Tension.wav',tension,.34)):
        room_reflections(track)
        write(name,track,ceiling)


def room_sounds():
    # Integer-period tones and periodic FFT noise keep loop boundaries continuous.
    seconds = 12
    t = np.arange(round(seconds*RATE))/RATE
    motor = .19*np.sin(2*np.pi*60*t)+.075*np.sin(2*np.pi*120*t)+.025*np.sin(2*np.pi*180*t)
    motor *= .90+.10*np.sin(2*np.pi*t/seconds)
    motor += band_noise(seconds,60,450,1731)*.026
    write('Pizzeria_Fridge_Room.wav', motor, .32)
    seconds = 10
    t = np.arange(round(seconds*RATE))/RATE
    fan = band_noise(seconds,180,1700,2309)*.07*(.85+.15*np.sin(2*np.pi*3*t/seconds))
    fan += .025*np.sin(2*np.pi*94*t)+.012*np.sin(2*np.pi*188*t)
    write('Pizzeria_Vent_Air.wav', fan, .27)
    seconds = .62
    t = np.arange(round(seconds*RATE))/RATE
    metal = sum(g*np.sin(2*np.pi*f*t)*np.exp(-t*decay)
                for f,g,decay in ((612,.18,13),(997,.11,18),(1583,.06,25)))
    metal *= (1-np.exp(-t*1800))*np.minimum((seconds-t)/.045,1)
    write('Pizzeria_Metal_Settle.wav', metal, .26)
    seconds = .45
    t = np.arange(round(seconds*RATE))/RATE
    phase = 2*np.pi*(850*t+210*(1-np.exp(-t*35))/35)
    drip = (np.sin(phase)*np.exp(-t*29)+.18*np.sin(2.17*phase)*np.exp(-t*48))
    drip *= (1-np.exp(-t*2100))*np.minimum((seconds-t)/.035,1)
    write('Pizzeria_Pipe_Drip.wav', drip, .24)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--music-only', action='store_true')
    args = parser.parse_args()
    compose()
    if not args.music_only:
        room_sounds()
