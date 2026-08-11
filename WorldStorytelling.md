# WorldStorytelling.md — Environmental Storytelling Reference (JP + CN studios)

> **Provenance.** Web research synthesis (multi-source), gathered broadly per request and
> **clubbed by technique** — studios using the same method are grouped; distinctive outliers are
> called out. Sources linked at the bottom.

---

## PART A — Japanese studios

The unifying Japanese instinct: **withhold, and let the player infer.** Story lives in space and
objects, not exposition. Below, grouped by the actual technique.

### A1. Story through geometry, props & enemy placement (no cutscene tells you)
**Studios: FromSoftware, Team Ico/genDESIGN, Capcom (Resident Evil), Kojima Productions.**
- **FromSoftware** distributes narrative across **level geometry, enemy placement, item names &
  descriptions, gestures, and fragments of NPC speech** — players build theories from evidence
  while the mystery is *deliberately preserved*. Each locale carries a distinct atmosphere and
  hints of its own history; a ruined garrison with rusted weapons around a dead Guardian *is* the
  battle report. (BotW does the identical trick — see A4.)
- **Capcom RE** turned the mansion/environment itself into the narrator — survival-horror as
  "experiments in environment design and storytelling"; the space teaches you what happened.
- **Kojima (P.T., Death Stranding)** builds dread and lore through environment + eerie isolation;
  P.T. is called "the most influential demo in gaming history" precisely for environmental
  storytelling with almost no text.

### A2. Design by subtraction / minimalism
**Studios: Team Ico (Fumito Ueda), Nintendo (restraint).**
- **Ico** ships with **no HUD, no health bar, no map, no inventory, very little explicit story.**
  Ueda's method literally *removes* elements (human enemies, etc.) to concentrate emotion on the
  core. Miyazaki cites Ueda as a direct influence — FromSoft's lore method is Ico's philosophy
  scaled up.
- Lesson: **what you take away** focuses the read as much as what you add (mirrors the VFX
  "restraint" pillar in ArtStyle.md §9).

### A3. Lore carried on examinable items + naming
**Studio: FromSoftware (the canonical example).**
- The **item description** is the primary lore vector — names, flavor text, who carried it. The
  world's history is reconstructed from inventory, not narrated.

### A4. Landmark-pull & instinctive navigation
**Studio: Nintendo EPD (Breath of the Wild / Tears of the Kingdom).**
- **Triangle Rule (terrain):** terrain shaped into triangular forms creates rhythm, hides/reveals
  landmarks, and **subtly suggests decision points** — you always glimpse the next landmark from a
  vantage, forming a natural exploration path. Towers/shrines = landmark attractors.
- **Real-world grounding:** Hyrule was laid out using a **map of Kyoto** (director Fujibayashi's
  hometown) to gauge believable distances/geography; Dueling Peaks ≈ Sado Mine.
- Lesson: **compose terrain and sightlines to pull the player**, and base layout on real places
  for believable scale.

### A5. Atmosphere & dread per locale
**Studios: Silent Hill team / Kojima (P.T.), Capcom (RE), FromSoftware.**
- Each area has a **bespoke mood** doing narrative work — fog, light, sound, decay. Tension is
  built by pacing and absence, not jump-cut storytelling.

---

## PART B — Chinese studios

The unifying Chinese instinct (current generation): **ground the world in real, specific culture,
then either go full-authentic or blend to lower the barrier.** Grouped by technique.

### B1. Real-world cultural grounding via photogrammetry / scanning
**Studio: Game Science (Black Myth: Wukong).**
- **Scanned real Chinese temples, statues, architecture** — four years traveling across China with
  local cultural institutions, building scans of real sites. The authenticity is *captured*, not
  invented. UE5 for realism; score built on **Chinese instruments + Shaanbei storytelling, Hua'er
  folk, Buddhist chanting**; pinyin retained (jingubang, yaoguai) to assert identity.

### B2. Soulslike environmental lore, adopted & localized
**Studios: Game Science, S-Game (Phantom Blade Zero).**
- Black Myth uses **Dark-Souls-style environmental lore** (story uncovered by traversing/observing)
  applied to Chinese mythology. S-Game (backed by NetEase + Tencent, hands-off) pushes a stylized
  "kungfu-punk" identity — distinctive art as the storytelling hook.

### B3. Region-as-culture worldbuilding + "lower the barrier" blend
**Studio: miHoYo / HoYoverse (Genshin Impact).**
- Each nation = a **distinct real-culture analog**; lore is **exploration-led** (you learn a region
  by moving through it). Genshin deliberately **blends Chinese myth/philosophy/aesthetics with
  Western fantasy tropes + open-world mechanics** to lower the cultural barrier for global players.

### B4. Cinematic / cross-media worldbuilding
**Studio: HoYoverse.**
- Experimenting with cinematic storytelling and world-building that **blurs anime / game / film** —
  the world extends beyond the game into shorts, music, lore media.

### B5. Distinctive stylized art identity as the differentiator
**Studios: Lilith (AFK Arena, Rise of Kingdoms), S-Game.**
- Lilith leans on **distinctive, universally-appealing stylized art** to stand out against
  standardized competitors — art identity *is* the brand and the world's first impression.

---

## PART C — How we apply this to Planet of Twins

Our world is a **fallen/ruined setting traversed by two bonded twins across streamed areas** — a
perfect fit for environmental storytelling.

- **Tell the twins' backstory & the world's fall through the areas themselves** (A1) — what L1_Park
  / L2_Streets *look like* and what's left in them, not exposition dumps. A ruined checkpoint, a
  Severed pair's remains, a Witness ritual site each narrate without text.
- **Design by subtraction** (A2) — our HUD is already minimal; keep lore on *examinable world
  objects* and atmosphere, not pop-ups.
- **Landmark-pull for our streaming** (A4) — place each area's `LocationEntrance`/POIs on
  triangle-rule sightlines so the next zone is *glimpsed* before it streams in; this also masks
  load boundaries naturally.
- **Atmosphere per locale** (A5) — lean on `MusicManager` (per-`WorldLocationSO` track/ambience)
  + lighting to give each streamed area a bespoke mood doing narrative work.
- **Ground our look in real reference** (B1) — even stylized, base area silhouettes on real places
  for believable scale (the Kyoto/BotW lesson).
- **Region-as-culture** (B3) — give Vethara (dark-energy/Kai) and Luminari (soul-light/Lyra) two
  legible cultural/visual vocabularies so areas read as belonging to one side or the other.

---

**Sources:** [World Design Lessons from FromSoftware](https://medium.com/@Jamesroha/world-design-lessons-from-fromsoftware-78cadc8982df) ·
[Ico level design — Film Stories](https://filmstories.co.uk/features/ico-level-design-when-less-is-more/) ·
[Environmental Storytelling in BotW — The Confusing Middle](https://confusingmiddle.com/2025/02/14/analyzing-environmental-storytelling-in-the-legend-of-zelda-breath-of-the-wild/) ·
[How BotW Makes Exploration Instinctive](https://medium.com/@jibeite45/the-call-of-the-wild-how-the-legend-of-zelda-breath-of-the-wild-makes-exploration-instinctive-506d03f4a732) ·
[P.T. — Wikipedia](https://en.wikipedia.org/wiki/P.T._(video_game)) ·
[Black Myth: Wukong — Wikipedia](https://en.wikipedia.org/wiki/Black_Myth:_Wukong) ·
[Rise of Chinese Gaming / Wukong](https://www.chinausfocus.com/society-culture/game-on-the-rise-of-chinese-gaming-and-the-global-impact-of-black-myth-wukong) ·
[From Rainblood to Phantom Blade Zero: S-Game](https://kr-asia.com/from-rainblood-to-phantom-blade-zero-the-origins-and-rise-of-s-game) ·
[Top Chinese Video Game Companies](https://aaagameartstudio.com/blog/chinese-video-game-companies) ·
[Inside Lilith Games — PocketGamer.biz](https://www.pocketgamer.biz/inside-mobile-hitmaker-lilith-games/)
