# Stage 2 — Replay localization and compact layout

- Replay header, facts, state, score/hand/contract/co-op labels and known command commentary are rendered in RU/EN. User names and recorded metadata remain intact.
- Command commentary captures pre-command road/face-up values and renders in the currently selected language. The shared Classic journal formatter translates recognized Core journal events; unknown legacy text is preserved. Error prefixes render in the current language; technical details may remain in their source language.
- Layout uses a bounded sidebar (220–360 px) and a two-row transport/timeline below 1400 px; scores and journal share a scroll view so showing hands cannot push the journal offscreen. Header facts and titles can truncate, with full text available via tooltip.
- Eight Edit Mode cases check locale switching, metadata preservation, journal translation, error context and layout thresholds. No game state, replay schema, network, or Steam SDK changes.

Manual QA: open a replay with long author/city names, show all hands, scroll scores/journal, use playback slider/buttons at 1280×800 and 1920×1080, switch language during playback or paused, review unsupported legacy entries and error messages. Native UI sizing and actual Steam Deck navigation are not established by unit tests.
