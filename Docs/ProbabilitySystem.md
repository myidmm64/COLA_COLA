# Pressure probability, revision 10

This is the implemented design, not a Play verification or simulation report.

## Probability

The conditional-risk calculation introduced in revision 9 was reverted at the user's request because bursts felt too rare. The original per-shake pressure curve is restored:

`baseChance = (finalPressure / limit)^exponent`

The current exponent is 2. Current pressure affects the result through final pressure; survival on previous shakes does not reduce this shake's base chance. The immediate final-percentage display remains in place. Explosion draws now use UnityEngine.Random as requested.

1. Final pressure includes the selected strength, incoming pressure, cooling and multiplier together. It never decreases below current pressure as a result of a shake. Items that release pressure directly update the current pressure before the prediction.
2. Apply safety/passive reductions and spicy-order increases in percentage points to this base chance, then clamp the net result to 0–100%.
3. Reaching/exceeding the limit always means 100%, including with safety effects. Editor W also forces 100%.
4. Preview, boss planning and actual resolution call the same model. Only a committed shake consumes one explosion draw; hovering/changing selection does not reroll anything. `UnityEngine.Random.Range(0, 16777216)` is divided by 16777216 to produce a value in `[0,1)`. This avoids `Random.value`'s inclusive upper endpoint. Normal play uses Unity's shared global random state; other Unity random calls can advance that state. Bottle limits and boss decisions still use their existing separate System.Random instances.
5. The numeric percentage switches to its final value immediately. The single final-pressure forecast needle still moves smoothly. Display rounds to 0.1 percentage point; values below 0.1% use `<0.1%`.

Each committed shake is a separate draw at the pressure-based probability. This is not a hidden pity or streak-breaking system; a low probability can still burst and a high probability can still survive.

Analytical examples with limit 30, exponent 2, no effects (not measured match results):

| Current pressure | Strength 1 (+2) | Strength 2 (+4) | Strength 3 (+8) |
|---|---:|---:|---:|
| 0 | 0.4% | 1.8% | 7.1% |
| 10 | 16.0% | 21.8% | 36.0% |
| 20 | 53.8% | 64.0% | 87.1% |

The existing bottle limits 25–35 and exponent 2 remain in effect. Match duration and item weights have not been retuned through simulated or actual matches in this change.

## Random source and diagnostics

- The explosion source was originally System.Random, then OS cryptographic randomness. It now uses UnityEngine.Random via `ColaBurstRandom.NextDouble()`. No cryptographic provider is created by this adapter.
- Exactly one draw is taken after committing a valid shake. The result is `roll < finalProbability`; the stored result drives the subsequent animation. There is no reroll during the suspense pause.
- Explicit seeded Editor probes use Unity's seeded state with save/restore around each draw, preserving other systems' global state. Their sequence changes from the old System.Random sequence. This replay path is compiled out of players, including Development builds. Unseeded Editor play uses normal Unity random too.
- Editor Console entries beginning `[BurstRoll]` include bottle/action, participant, full-precision final probability, sampled value, outcome, random source, and the forced-burst debug flag. Existing `LastResult` also retains the committed probability and sampled value. This adds diagnostics; it is not evidence of an executed test.
- A better entropy source cannot promise eight bursts in each ten 80% attempts or prevent clustered 7% bursts. No hidden pity, shuffle-bag or streak correction is applied.

## Display and settings

- One forecast needle shows only the final combined pressure. Intermediate increase/reduction arrows and the extra base-pressure dash are retired.
- The first application of the new defaults selects the primary display's maximum supported pixel area, fullscreen, and English, then saves them. Later player choices remain saved.
- Resolution choices are sorted by pixel count descending, then width descending. Only actual supported monitor modes are listed; authored presets and unsupported saved values are no longer inserted. Windows queries its primary display with EnumDisplaySettingsEx (no raw or rotated-mode flags). The browser uses its canvas dimensions rather than desktop display modes.
- Standalone Windows positions its own game window on the primary display using Unity's display layout and window API. The Editor window is never moved.

Unity API references: [display layout ordering](https://discussions.unity.com/t/how-to-get-game-build-to-run-on-main-display/879642?page=2), [screen/window API](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/screen).

## Stage presentation

| Stage | Shirt | Idle emphasis | Background accents |
|---|---|---|---|
| 1 | Original light cotton | Slow glances, exhale | Original olive/green and red |
| 2 | Slate blue | Fingers, impatience | Cool blue/teal |
| 3 | Ochre | Gauge and item inspection | Warm amber |
| 4 | Muted burgundy | Deliberate head movement | Deeper red |
| 5 | Muted violet | Tense hands, sustained focus | Violet and cool blue |

The original cloth atlas detail is retained. Explicit shirt/sleeve UV islands and a neck-skin exclusion define the recolor area, independently of pixel brightness. The garment's original luminance preserves fabric grain, folds and print while applying the stage hue uniformly. Face/glasses remain on their original materials. Front lighting for the bottle, dial and table stays at the existing readability values. Gesture selection uses presentation RNG, separate from explosion and item outcomes.

Debug tools are available only under `UNITY_EDITOR`; release and Development players reject the shared debug command API and have no debug keyboard handlers.
