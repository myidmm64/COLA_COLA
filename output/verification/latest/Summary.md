# Promotional GIF capture

- Requested three promotional clips captured from the original Unity project in actual Play/Game view at 1920x1080.
- Exported to output/publishing/promo-gifs/ at 960x540, looping, with original captured timing and no audio.
- Player shake and burst staged with ColaDuelController.ApplyDebugCommand(ToggleGuaranteedBurst); boss shake through PassPlayerTurn; IceCup supplied through ToggleFreeItemMenu/TryPurchase and used through TryUseSlot.
- Editor debug switches, tokens, AI/HUD state and persistent language/achievement preferences restored. Original scene and Game-view configuration restored; Editor remains open in Edit mode.
- Temporary PNG sequences and capture-only script/bridge hooks removed. No gameplay implementation changes, standalone build or general verification suite.
- Representative original Game-view frames inspected for the player's held bottle, burst effects/blackout, boss holding/shaking, and lifted ice cup.