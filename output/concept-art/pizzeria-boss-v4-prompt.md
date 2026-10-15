# COLA_COLA — 1안 픽셀 체력바

생성 방식: built-in image_gen. v3-a의 픽셀 하트를 픽셀 체력바로 수정한 컨셉아트입니다.

입력: pizzeria-boss-v3-a.png
출력: pizzeria-boss-v4.png

## 생성 프롬프트

Use case: precise-object-edit / game UI.
Edit target: pizzeria-boss-v3-a.png, the provided borderless pixel-heart HUD version of COLA_COLA.
Make a very narrow edit: replace ONLY the two rows of heart icons on the right with horizontal pixel-art health bars.

Upper-right boss HUD: keep the exact label "보스", exact HP text "1000 / 3000", gold pixel token icon and count "12", current position, cream bitmap typography and shadow. Where the three hearts were, draw one compact horizontal rectangular pixel HP bar with TWELVE equal cells: exactly FOUR cells filled muted red and EIGHT dark empty cells, so the bar reads one-third full.
Lower-right player HUD: keep the exact label "플레이어", exact HP text "2000 / 3000", gold pixel token icon and count "15", current position, cream bitmap typography and shadow. Where the three hearts were, draw an identical horizontal pixel HP bar with TWELVE equal cells: exactly EIGHT cells filled soft green and FOUR dark empty cells, so the bar reads two-thirds full.

Bar design: simple squared geometry, crisp low-resolution pixel edges, small clear segment gaps, thin cream pixel outline and dark one-pixel shadow. Approximately 210–230 screen pixels wide at the original image size, about 20–24 pixels tall; maintain compact balanced proportions. Flat colors with very restrained pixel highlights. No rounded ends, smooth gradients, glow, hearts, portraits, icons inside the bar or 3D housing.
Keep the rest of each status group borderless: no encompassing panel, card, metal frame, plastic plaque or background behind the name/numbers. Preserve the clean light pixel-HUD style of version A.

Everything else must remain unchanged: the same framing, room, boss, glasses, pose, lighting, central bottle, player hands, loose tabletop inventory props and their positions. Keep the physical left pressure gauge, its reading "15", question mark "?", and physical buttons "1", "2", "3" unchanged. The removed left labels "현재 압력" and "흔들기 강도" must stay absent. Do not add inventory UI or item slots.
Output one edited image with the original wide framing, no comparison layout or captions.
