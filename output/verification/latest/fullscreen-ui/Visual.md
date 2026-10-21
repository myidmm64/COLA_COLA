PASS: Original project in actual Unity Play
PASS: Actual Game view is native 1920x1080
PASS: Main-menu boss idle advances while the duel clock remains paused
GAME VIEW: 01-main-1080p.png / 1920x1080
PASS: Settings volume uses real listener gain
PASS: Resolution and fullscreen use shared settings paths
GAME VIEW: 02-settings-1080p.png / 1920x1080
PASS: Start resumes the duel and restores the boss animation clock
PASS: Q sets the actual wallet to 999
PASS: E opens the real free test menu
PASS: Free menu sliders select two different items
GAME VIEW: 03-items-1080p.png / 1920x1080
PASS: FHD menu print fits its actual paper regions
PASS: Actual checkout collider accepts the free batch
PASS: Reopen free menu before disabling E
PASS: Free menu delivers both items; E off before paid service
PASS: Item description and persistent TAB hint coexist
GAME VIEW: 04-hover-1080p.png / 1920x1080
GAME VIEW: 07-hover-1440p.png / 2560x1440
Restored initial Q wallet, W/E flags and boss AI. Editor hook restores language, settings, achievements and Game-view layout.

Visual assessment of the sparse actual Game-view screenshots is recorded separately. No preview cameras, RenderTextures or temporary builds.

## Direct visual assessment

The latest actual Play/Game-view captures were opened after capture, including the final regenerated main menu and tooltip images.

- `01-main-1080p.png`: title and three menu actions are readable, aligned and clear of the background character; corner credit remains subdued.
- `02-settings-1080p.png`: setting labels, volume control and dropdown text have distinct rows and readable sizing.
- `03-items-1080p.png`: item names, row numbers and purchase controls fit the physical paper; the description remains above checkout.
- `04-hover-1080p.png`, `07-hover-1440p.png`: centered title and effect remain readable over the bright table. Return hint stays smaller and quieter; bottom-left TAB text remains separate.
- `05-player-shake-1080p.png`: item description and TAB hint are absent during the player's shake as requested.
- `06-records-1080p.png`, `08-ability-choice-1080p.png`: tabs, ability entries and lower paper descriptions stay within their reserved areas.
- `09-complete-1080p.png`: completion receipt and restart action are readable without colliding.

These are sparse captures of distinct flows, not preview renders or a continuous frame sequence. No audio listening assessment or standalone executable validation is implied.
