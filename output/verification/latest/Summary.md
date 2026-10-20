# Latest Play verification — camera onset

- Result: passed. Request: shake-camera-9c88a40b92ab4b6180efb3547cda5c6c.
- Original MainScene in the already-open Unity Editor. Physical camera transform sampled at end of actual Play frames.
- W off; no purchases, Q/E unnecessary. Original debug and automatic-turn flags restored.
- Button onset (0.01mm threshold): 44–50ms before, 16–18ms after.
- First shake input CPU: 49.80ms before, 12.39ms after. Maximum sampled first-shake frame interval: 62.35ms before, 16.51ms after.
- Ordinary position/rotation continuity retained; no reset of camera velocity on a new interaction.
- Burst textures, particle resources and synthesized audio now prepared during session startup. No explosion is played during preparation.
- Isolated bell/grip camera curves measured separately; this is not a bell-payment or item-balance test.
- No screenshots, mesh baking, preview rendering or disk I/O during measured frames. Continuous Game-view visual review not performed in this timing run.
- Original scene setup, Game-view maximization and editor focus restored; Editor left open.

Latest detailed results: [camera-motion/Play-review.txt](camera-motion/Play-review.txt). These are observed timings from this editor session, not a hardware-independent performance guarantee.