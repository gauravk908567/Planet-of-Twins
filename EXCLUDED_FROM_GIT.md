# Excluded from git (kept on local disk only)

These large / re-importable items are intentionally **not** in git history, so the repo
stays pushable (GitHub rejects any file >100 MB and chokes on multi-GB pushes). They are
**not** gitignored — the on-disk ones remain on your machine, untracked, so you can re-add
any of them to git later if you decide to.

Removed from the `vfxsounds` history on 2026-08-11 via `git filter-repo`:

| Item | Why removed | On-disk status |
|---|---|---|
| `Assets/TerrainDemoScene_URP/` | Unity terrain demo scene; not in Build Settings | deleted (re-import from Package Manager if ever needed) |
| `Assets/Skybox/AllSkyFree/` | AllSky asset-store pack; game uses its own Coexistence skybox | deleted (re-import if needed) |
| `Assets/Art/Terrain/Textures_Demo/Cliff_Mossy_E/` | 3 textures over 100 MB; **UNUSED** by the game terrain (only the demo scene's palette referenced them) | **kept locally** — re-add to git only if the game starts using them AND they're resized under 100 MB |
| `PitchDeck/` | Pitch materials (docs / media / references) | **kept locally** |
| `.continue/` | continue.dev config | **kept locally** |
| root `*.pdf` (6 Unity guide ebooks) | re-downloadable reference PDFs | **kept locally** |

**Kept in git (confirmed used by the game):** `SkyboxMountains_Demo` (Persistent scene +
Temple Vista), `Assets/Samples/Shader Graph` (holds `Cloud04_8x8.png` used by the skybox),
`Details_Demo` vegetation, `VidSrc`, the lighting bake, and all scripts/scenes/materials/prefabs.

> Note: since these are not gitignored, a future `git add -A` will re-stage the on-disk ones.
> Add selectively if you want to keep them out of a commit.
>
> Full pre-cleanup history is preserved locally at tag `backup-pre-rewrite-2026-08-11`
> (recover with `git reset --hard backup-pre-rewrite-2026-08-11`). Delete that tag and run
> `git gc` to reclaim the disk once you're satisfied.
