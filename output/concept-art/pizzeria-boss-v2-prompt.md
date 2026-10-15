# COLA_COLA — 로우폴리 UI · 테이블 위 3D 아이템

생성 방식: built-in image_gen으로 v1 컨셉아트 수정. Unity 실제 플레이 캡처가 아닙니다.

입력 이미지: pizzeria-boss-v1.png
최종 이미지: pizzeria-boss-v2.png

## 생성 프롬프트

Use case: precise-object-edit / UI-mockup.
Edit target: the supplied COLA_COLA pizzeria boss-duel concept image.
Primary request: Revise ONLY the interface design and inventory presentation to strongly match the low-poly PSX 3D asset style. The user explicitly wants visible, physical 3D inventory props on the table, never inventory-slot UI.

Preserve invariants: the same camera and first-person table composition, pizzeria brick room and red seating, warm lights, seated single boss (dark hair, pale square glasses, white T-shirt, wristwatch), central COLA CLASSIC red-cap PET bottle, and the two teal-sleeved player hands. Keep the overall scene, focal hierarchy and recognizable project-asset look. No new opponents, no weapons, no change of venue.

Change 1 — low-poly interface:
Replace the current detailed rusty-metal 2D fantasy panel frames with intentionally simple, thick, angular, flat-shaded PSX-era game objects and crisp bitmap typography. No detailed rivets, ornamental borders, fine scratches or realistic brushed metal.
The left pressure gauge becomes a chunky faceted physical instrument with a 12-sided dial and a cream face in a blocky dark-teal housing. Make its thickness, planar facets, small tabletop support and contact shadow visible so it feels like a low-poly object in the scene. It sits at the left side of the table, away from the central bottle and boss face. Place three square physical pushbuttons labeled "1", "2", "3" on its simple angled base. These are the shake-strength controls, not inventory. Use cream, dark teal and muted red matching the pizzeria. Keep a small pixel-font label "현재 압력", current reading "15", prominent "?" for the undisclosed pressure limit, and "흔들기 강도" over the three keys.
The boss status in the upper-right and player status in the lower-right remain compact readable HUD elements, but redesign them as simple angular low-poly plaques with visible beveled thickness, discrete rectangular HP segments and low-resolution bitmap lettering, visually consistent with PSX gameplay. No ornate metal frames. Keep exact labels and values: "보스", "1000 / 3000", token icon "12"; "플레이어", "2000 / 3000", token icon "15". Keep them at the screen edges and away from the bottle. Avoid smooth vector curves and modern slick mobile UI.
Keep the whole scene readable: pixel-art treatment primarily in textures, labels and UI edges, not an excessive blur or giant pixelation filter covering the entire image.

Change 2 — PHYSICAL inventory props, absolutely no slots:
Completely remove BOTH labeled inventory panels: the near-left "플레이어 아이템" panel and far-right "보스 아이템" panel. Remove every associated rectangular slot, icon tile, quantity badge, backdrop and inventory label. Restore the ordinary speckled table surface under them. NO trays, NO mats, NO outlined rectangles, NO card rows, NO grid markings, NO hovering inventory icons.
In the former near-left inventory area place a small loose group of actual three-dimensional consumable props directly on the tabletop: a short low-poly silver pressure-release valve with red turning wheel, a small teal cold pack with a cap, and a folded cream paper pizza coupon. They must have distinct silhouettes, object thickness, perspective, appropriate physical scale smaller than the central cola bottle, and convincing warm contact shadows. They are world objects a player could physically reach and pick up, not pictures printed on a board.
In the former far-right boss inventory area place two or three simple physical props directly on the tabletop, such as a red low-poly soda can, metal kitchen tongs and folded cream paper coupon, using the existing pizzeria prop vocabulary. Again actual volumes and contact shadows, no UI display.
Keep the two groups separated by distance on opposite sides of the table, with the central cola and hands unobstructed. Keep enough bare table to read the scene; don't surround the cola with clutter.

Style: coherent low-poly 3D PSX game screenshot, visibly faceted silhouettes, flat shading, low-resolution nearest-neighbor textures, subtle dithering, angular button models, restrained pixel text. Integrate new props into the existing room lighting and perspective. The edited frame should feel more like a single cohesive playable 3D scene.
Output: one finished wide gameplay concept frame, matching the original framing. No comparison panels, captions or watermarks.
