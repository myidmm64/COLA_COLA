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
PASS: Right-click shared path returns immediately and pays exactly once
PASS: The same removed item cannot be returned twice
PASS: Actual bell-hand request accepted with E off
PASS: Real bell contact charges the real price and delivers the configured count
PASS: Bell becomes reusable after the shortened hand animation
PASS: Normal probability player shake accepted with W off
PASS: Only the player shake hides descriptions and TAB
GAME VIEW: 05-player-shake-1080p.png / 1920x1080
PASS: Boss shaking leaves TAB and item inspection available
PASS: Boss-shake item description is visible through the real inspection path
PASS: Player can return a stored item during the boss shake
PASS: Duel returned to player control
Normal sequence observed boss shake: True.
PASS: Open ability test page: ko
PASS: Long ability text fits: ko
PASS: Open ability test page: en
PASS: Long ability text fits: en
PASS: Open ability test page: ja
PASS: Long ability text fits: ja
PASS: Open ability test page: zh-Hans
PASS: Long ability text fits: zh-Hans
PASS: Open ability test page: zh-Hant
PASS: Long ability text fits: zh-Hant
PASS: Open ability test page: fr
PASS: Long ability text fits: fr
PASS: Open ability test page: de
PASS: Long ability text fits: de
PASS: Open ability test page: es
PASS: Long ability text fits: es
PASS: Open ability test page: pt-BR
PASS: Long ability text fits: pt-BR
PASS: Open ability test page: ru
PASS: Long ability text fits: ru
PASS: TAB shared path opens the physical records
GAME VIEW: 06-records-1080p.png / 1920x1080
PASS: Record text and tabs fit at 1080p
PASS: Native 1440p uses proportional UI scale
GAME VIEW: 07-hover-1440p.png / 2560x1440
PASS: Records open at the small-screen preset
PASS: Small-screen records fit without overflowing the paper
PASS: W on only for forced-burst and ability checks
PASS: Guaranteed-burst action 1
PASS: Burst/blackout/fade returned control 1
PASS: Guaranteed-burst action 2
PASS: Burst/blackout/fade returned control 2
PASS: Guaranteed-burst action 3
PASS: Burst/blackout/fade returned control 3
PASS: Three real bursts open the mandatory ability choice
GAME VIEW: 08-ability-choice-1080p.png / 1920x1080
PASS: Actual choice collider grants the ability and releases the pending reward
PASS: Duel returned to player control
PASS: Stage shared debug path 1
PASS: Correct boss health for stage 1
PASS: Stage shared debug path 2
PASS: Correct boss health for stage 2
PASS: Stage shared debug path 3
PASS: Correct boss health for stage 3
PASS: Stage shared debug path 4
PASS: Correct boss health for stage 4
PASS: Stage shared debug path 5
PASS: Correct boss health for stage 5
PASS: Final stage reaches challenge complete
GAME VIEW: 09-complete-1080p.png / 1920x1080
PASS: New run resets after completion
PASS: ESC settings shared path resumes the active game
PASS: Return to main menu pauses gameplay
PASS: Build scene exists: Assets/Scenes/MainScene.unity
PASS: Build starts in the intended main scene only
Build target: StandaloneWindows64. No standalone build was generated.
Restored initial Q wallet, W/E flags and boss AI. Editor hook restores language, settings, achievements and Game-view layout.

Visual assessment of the sparse actual Game-view screenshots is recorded separately. No preview cameras, RenderTextures or temporary builds.

## Final review

- 76 Play assertions passed in the full run `readability-fullscreen-recheck-20261021`.
- Final tooltip contrast was checked again in actual native 1920x1080 and 2560x1440 Game views (`readability-visual-final-20261021`).
- The actual Game-view screenshots were opened and visually inspected: main menu, settings, item menu, item tooltip, player shaking, records, ability choice and challenge completion. Font size, screen margins, description wrapping and overlap were reviewed. No separate preview camera was used.
- 1366x768 was an automated paper-region fit check; no screenshot-based visual assessment is claimed for that preset.
- Windows64 release player scripts compiled successfully, with no executable or resource packaging build generated. Temporary script compiler outputs were deleted.
- Forced bursts and stage progression used shared debug commands; this was a functional flow review, not an unassisted full-campaign playthrough or balance simulation.
