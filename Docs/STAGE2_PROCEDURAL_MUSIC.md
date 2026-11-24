# Stage 2 — Original procedural background music

## Scope

This iteration adds a **quiet, original, synthesized music loop** without
introducing licensed music, sample downloads, third-party audio packages,
changes to gameplay rules, replay protocols or save-game data.

- `MusicSoundscape` is a deterministic 16-second four-phrase harmonic
  composition at 22,050 Hz, synthesized into a mono PCM `AudioClip`.
- Each phrase fades in/out; the final edge is silent, preventing a hard
  click at the loop boundary. A simple tonal accent keeps the soundscape
  from being a flat constant tone.
- `MusicBank` owns a **separate `AudioSource`**, so regular `SoundBank`
  `PlayOneShot` effects are never interrupted by the background track.
- The clip is generated **lazily on first enable**, so existing users who
  leave music disabled do not pay its startup allocation/computation.
- No streaming callbacks or per-frame sample generation. Once generated,
  the normal Unity audio engine plays the loop.

## Configuration

The Sound settings page now includes localized **Background music** and
**Music volume** controls (RU / EN). The saved `DisplaySettings` uses
`musicEnabled` (default false) and `musicVolume` (default 35%). All
existing players retain music **off** by default.

The master volume (`DisplaySettings.volume`) still controls both music
and sound effects. The existing game `sound` toggle also silences music.
`uiSounds` continues to apply **only to UI effects**.

A Display.v1 save that lacks the new music-volume field is migrated to
35% safely. Explicitly saved zero remains zero, so volume-based muting
survives a restart. The new fields do not modify or migrate saved games.

## Tests

`MusicSoundscapeTests` includes 9 Edit Mode checks for phrase and loop
boundaries, periodicity, deterministic output, bounded samples, invalid
times, mute semantics, safe volume, legacy settings and round-trip
persistence. Existing gameplay and SoundBank effects should remain
unchanged.

## Acceptance on desktop and Steam Deck

1. On a fresh or existing installation, music starts **off**.
2. Open Settings → Sound and turn background music on; check that a soft
   original theme plays in the menu and continues in both game modes.
3. Play SFX over the music (road placement, toll, character, UI clicks).
   The sounds must remain audible; the music must not restart on SFX.
4. Move the **Music volume** slider from 0% to 100%, and independently
   move the master volume. A music level of 0% should stay silent.
5. Disable the existing main sound toggle: both music and one-shot SFX
   should become silent. Re-enable it and check normal playback.
6. Restart the game. Music toggle and its volume should persist, and
   disabling/re-enabling should not create duplicate AudioSources.
7. Listen over a complete 16-second loop for clicks or bursts. Verify
   Unity Editor, desktop build and Steam Deck separately.
8. Switch RU↔EN and use keyboard/mouse or gamepad navigation in the
   settings UI at 1280×800.

**Limitations:** Edit Mode tests validate math, settings migration and
build compatibility but do not objectively measure how pleasant the music
sounds or verify hardware audio playback. These need manual listening.
