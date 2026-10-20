"""Last Order: a four-section 138 BPM suspense score with rests, evolving phrases and room assets."""
import argparse
from pathlib import Path
import uuid
import wave
import numpy as np

RATE = 22050
BPM = 138
BEAT = 60 / BPM
DURATION = 128 * BEAT
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


def bass_note(midi, seconds, weight=1):
    t = np.arange(round(seconds*RATE))/RATE
    f = frequency(midi)
    # Fixed pitch, short release: weight and grit without a bouncing pitch envelope.
    wave = sum(np.sin(2*np.pi*f*k*t)/(k**1.35) for k in (1,2,3,5,7))
    envelope = (1-np.exp(-t*190))*(.35+.65*np.exp(-t*3.5))*np.clip((seconds-t)/.055,0,1)
    return np.tanh(wave*1.6)*envelope*weight


def low_bed(midi, seconds):
    t = np.arange(round(seconds*RATE))/RATE
    f = frequency(midi)
    rise = np.clip(t/.16,0,1)
    fall = np.clip((seconds-t)/.4,0,1)
    return (np.sin(2*np.pi*f*t)+.19*np.sin(2*np.pi*f*3*t))*rise*fall


def dry_kick():
    t = np.arange(round(.17*RATE))/RATE
    body = np.sin(2*np.pi*57*t)*(1-np.exp(-t*900))*np.exp(-t*27)
    attack = band_noise(.17,450,1900,602)*np.exp(-t*180)*(1-np.exp(-t*1400))*.15
    return body+attack


def factory_hit():
    t = np.arange(round(.14*RATE))/RATE
    gate = (1-np.exp(-t*1700))*np.exp(-t*55)*np.clip((.14-t)/.015,0,1)
    return (band_noise(.14,650,3600,603)*.48+
            np.sin(2*np.pi*397*t)*.16+np.sin(2*np.pi*611*t)*.12)*gate


def closed_tick():
    t = np.arange(round(.045*RATE))/RATE
    return band_noise(.045,3200,7100,604)*(1-np.exp(-t*1800))*np.exp(-t*130)


def room_reflections(track):
    # Short and quiet: avoid the layered ringing of a long reverberant tail.
    dry = track.copy()
    for index,(seconds,gain) in enumerate(((.043,.065),(.079,.040),(.137,.022))):
        reflected = dry[:,::-1] if index%2 else dry
        track += np.roll(reflected,round(seconds*RATE),axis=0)*gain


def answer_note(midi, beats, weight=1):
    seconds = beats*BEAT
    t = np.arange(round(seconds*RATE))/RATE
    f = frequency(midi)
    rise = np.clip(t/.045,0,1)
    fall = np.clip((seconds-t)/.28,0,1)
    return (np.sin(2*np.pi*f*t)+.14*np.sin(2*np.pi*f*2*t))*rise*fall*np.exp(-t*1.1)*weight


def compose():
    DEST.mkdir(parents=True, exist_ok=True)
    base = np.zeros((COUNT,2))
    tension = np.zeros_like(base)
    kick, metal, tick = dry_kick(), factory_hit(), closed_tick()
    # Four composed eight-bar sections: sparse opening, movement, breathing room, then a short build.
    # Events are on a stable musical grid, with deliberate gaps rather than a continuous eighth-note pump.
    patterns = (
        (
            ((0,0,1.1,1),(1.5,7,.55,.65),(2.75,0,.85,.85)),
            ((.5,0,1.6,.8),(2.5,3,.7,.75)),
            ((0,0,.7,.9),(1.25,3,.7,.7),(3,1,.75,.7)),
            ((0,0,1.7,.85),(3,-1,.55,.6)),
        ),
        (
            ((0,0,.8,1),(1,0,.5,.65),(1.75,7,.65,.7),(2.75,3,.55,.8),(3.5,0,.4,.65)),
            ((0,0,1,.9),(1.5,3,.65,.8),(2.5,7,.5,.7),(3.25,1,.6,.7)),
            ((.25,0,.6,.8),(1,7,.7,.7),(2,3,1,.85),(3.5,0,.4,.65)),
            ((0,0,1.4,.9),(2,-1,.65,.75)),
        ),
        (
            ((0,0,2.8,.65),),
            ((1,3,2,.6),),
            ((.5,0,1.7,.6),(3,7,.7,.5)),
            ((0,-1,2.2,.6),),
        ),
        (
            ((0,0,.6,1),(1,0,.5,.7),(1.75,3,.5,.7),(2.5,7,.6,.85),(3.5,1,.4,.6)),
            ((.5,0,.7,.8),(1.5,3,.6,.7),(2.75,0,.8,.85)),
            ((0,0,.7,.9),(1.25,7,.5,.7),(2,3,.7,.8),(3,-1,.5,.6)),
            ((0,0,1.8,.9),(2.5,1,.6,.7)),
        ),
    )
    for bar in range(32):
        section, phrase = bar//8, bar%4
        at = bar*4
        root = (38,38,36,37)[(bar%8)//2]
        for beat,offset,length,accent in patterns[section][phrase]:
            # The last half-bar rests before the loop returns to its sparse opening.
            if bar == 31 and beat >= 2: continue
            place(base,at+beat,bass_note(root+offset,BEAT*length,.085*accent),-.04)
            if section != 2 and (beat%1 != 0 or phrase%2 == 0):
                place(tension,at+beat+.25,answer_note(root+offset+12,.6,.025*accent),.08)
        if section == 0:
            kicks = ((0,2.5),(.5,),(.0,2.75),(0,))[phrase]
            hits = ((3,), (2.5,), (1.5,), ())[phrase]
            ticks = (1,3.5) if phrase%2 == 0 else ()
        elif section == 1:
            kicks = ((0,2.5),(.0,2.75),(.25,2),(0,))[phrase]
            hits = (1,3) if phrase < 3 else (1.5,)
            ticks = ((.5,2,3.5),(1,2.25),(.75,3),())[phrase]
        elif section == 2:
            kicks = (0,) if phrase == 0 else ()
            hits = (3,) if phrase == 2 else ()
            ticks = ()
        else:
            kicks = ((0,2.5),(.5,2.75),(0,1.75,3),(0,))[phrase]
            hits = (1,3) if phrase < 3 else (1.25,)
            ticks = ((.5,1.75,3.5),(1,3.25),(.75,2.5),(1.5,))[phrase]
        for beat in kicks: place(base,at+beat,kick*(.070 if section != 2 else .038),0)
        for beat in hits: place(base,at+beat,metal*(.036 if section != 2 else .018),.06)
        for beat in ticks: place(base,at+beat,tick*.009,.10)
        if section in (1,3) and phrase == 2:
            # One short fill per phrase, followed by a less dense bar.
            for beat in (3.25,3.75): place(tension,at+beat,tick*.014,-.10)
        if bar%2 == 0:
            place(base,at,low_bed(root,BEAT*6.2)*(.013 if section != 2 else .022),0)
    # A small unanswered phrase changes register between sections; it is not a constant tick.
    answers = ((3,2.5,62,1.3),(6,1.5,61,1.5),(10,3,57,1.6),(13,2,60,1.2),
               (16,1,62,2.8),(18,2.5,65,2.2),(20,.5,61,2.4),(22,1.5,57,2.0),
               (25,2,62,1.4),(27,1.5,63,1.5),(29,2.5,61,1.2))
    for bar,beat,note,length in answers:
        place(base,bar*4+beat,answer_note(note,length,.022),.08)
        place(tension,bar*4+beat+.5,answer_note(note-12,length*.8,.016),-.08)
    for name,track,ceiling in (('Pizzeria_Night_Base.wav',base,.47),('Pizzeria_Night_Tension.wav',tension,.25)):
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
