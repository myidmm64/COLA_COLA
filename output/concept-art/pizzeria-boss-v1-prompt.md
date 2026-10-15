# COLA_COLA — 피자리아 보스전 컨셉

생성 방식: built-in image_gen. 프로젝트 에셋과 사용자 스케치를 참조하는 AI 컨셉아트이며 Unity 실제 플레이 캡처가 아닙니다.

최종 이미지: pizzeria-boss-v1.png

## 생성 프롬프트

Use case: stylized-concept, sketch-to-render with asset references.
Asset type: one finished 16:9 landscape gameplay concept image for the Unity game COLA_COLA. A believable PSX / early-3D game screenshot with a carefully designed readable HUD, not an illustration poster.

Primary request: Turn the user's game-screen sketch into a compelling first-person single-player boss duel inside a pizzeria, faithfully using the visual identity of the provided local project assets. One human boss opposite the player. No multiplayer lobby, no other opponents.

Input images and roles:
1. User sketch: composition and screen layout reference. Preserve its arrangement of a first-person table, boss opposite, bottle in the middle, pressure gauge on the left, three strength controls below it, player inventory near the lower-left tabletop, boss inventory on the far-right tabletop, boss status upper-right, player status lower-right. Do not reproduce handwriting or sketch arrows.
2. Actual project character mesh render: strict boss appearance reference. Preserve the simple low-poly young adult male with short dark-brown hair, large square pale glasses, light off-white short-sleeved T-shirt with a small chest motif, denim shorts, dark shoes and wristwatch. Pose him seated behind the table, leaning slightly forward, arms naturally on the far edge, looking toward the player; do not copy T-pose. No chef costume, no hat, no invented monster, no facial hair, no suit.
3. Actual cola asset preview sheet: use the SINGLE original low-poly semi-transparent PET cola bottle, red ribbed cap, dark brown liquid, red and cream COLA CLASSIC 500 ML pixel label. Slightly swollen lower body hints at danger. Preserve its asset silhouette, do not turn it into a glass bottle or can. Do not copy the reference sheet's layout or text.
4. Actual Chair_Table texture atlas: pizzeria furniture material guide, ivory speckled laminate tabletop, red vinyl chairs/booths with dark supports. Read this UV atlas as a material guide, never display its flat layout.
5. Actual Soda_machine texture atlas: the pizzeria asset's red-and-black soda fountain, as an appearance and material reference. Never display its flat UV layout.
Additional inspected project assets: PlayerArms has bare natural hands with dark teal-blue sleeves/cuffs; pizzeria includes reddish-brown small-brick walls, pale square tiles, a dark chalkboard Menu, stainless Pizza_oven, preparation counter and Pizza_box props. Match these modest asset materials rather than inventing ornate restaurant decoration.

Scene/backdrop: recognizable modest pizzeria interior made from those low-poly assets: reddish brick walls, pale tiled floor, ivory laminate tables and red vinyl seats, service counter, menu board, stainless oven, red soda fountain, restrained pizza boxes and condiment bottles from the pizzeria prop vocabulary. Keep the arena uncluttered and the boss and cola immediately readable. No guns, no weapons, no extra patrons. The restaurant continues behind the boss, giving natural depth.

Composition: first-person seated eye-level view looking slightly downward across a large rectangular table; the tabletop occupies the lower half to lower two-thirds. The boss occupies the upper-middle slightly right of center, head fully visible with clear silhouette. The single cola bottle stands upright centrally on the table, isolated against darker surroundings and large enough to read its form. Player's two hands frame the bottom corners. Player and boss inventory spaces are distinct areas of the shared table, do not create a second table.

Style: faithful low-poly PSX game art with simple geometry, low-resolution material textures, subtle ordered dithering and slightly crunchy shadows. Consistent assets and proportions. This must look achievable from this project's actual models. Avoid photorealism, painterly brushwork, anime, clay style, premium cinematic character redesign, huge hands, excessive noise or extreme fisheye.

Lighting/mood: tense after-hours pizzeria duel. Warm overhead restaurant light pools over the bottle and table, dimmer red and cool green reflections from the existing soda/restaurant equipment in the background. Shadows give suspense while the scene and face remain readable. Keep the asset's cream/red/brown/teal palette.

HUD layout, integrated restrained worn-metal and cream pixel UI, readable Korean text:
- Upper-left / left-middle: compact circular analog pressure gauge. Label exactly "현재 압력", current reading "15" below the dial, a prominent "?" on the unknown upper limit. The explosion threshold MUST remain unknown: no numerical maximum, no explicit safe threshold. Keep the gauge out of the boss's face and don't make it the dominant object.
- Directly below the left gauge: three rectangular buttons, exactly "1", "2", "3", with small heading "흔들기 강도". Three buttons only.
- Upper-right: boss name "보스", red horizontal HP bar, exactly "1000 / 3000", gold token icon and "12".
- Lower-right: name "플레이어", green horizontal HP bar, exactly "2000 / 3000", gold token icon and "15".
- Three subtle player inventory slots on the near-left table area and three boss slots on far-right; keep small, not a bottom full-screen toolbar. Mostly empty with modest small consumable silhouettes, no cards filling the scene.
- These are the only required overlay labels. No additional slogans, title screen, explanatory captions, annotation arrows, watermark or art frame.

Output: one polished, wide 16:9 full-screen in-game concept frame, no split panels, no reference sheet. Preserve the sketch's gameplay hierarchy and use the supplied actual assets as appearance anchors.
