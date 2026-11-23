# Advisory benchmark scenario preflight

The **City Highways → 3D Objects → Performance Scenario Suite** window now checks manually typed scenario labels before recording new frame captures.

## Recommended label

`magnate;seed=42;camera=central;viewport=1920x1080`

The new advisory parser checks that:

- `seed` exists once and is a signed 32-bit integer;
- `camera` exists once and is nonempty;
- `viewport` exists once, uses positive integer `WIDTHxHEIGHT`, and matches the observed Unity screen resolution when runtime conditions are available.

Keys are case-insensitive and surrounding whitespace is allowed. Arbitrary scenario text is retained for compatibility: a free-form label produces a warning, not an automatic rejection. If a warning is present while capturing, the editor requests **Capture anyway** or **Cancel**. No automatic text rewriting or seed generation takes place.

## What this does not verify

The tool **cannot establish the actual procedural random Magnate seed**, Unity camera position, scene workload, or the true viewport from the manually entered label alone. Matching label syntax only reduces accidental errors, it is not a proof of reproducibility. Previously recorded suite JSON, existing scenario identities, save games, and archive report data remain unchanged.

## Acceptance

- [ ] Unity Edit Mode CI passes `City3DPerformanceScenarioPreflightTests`.
- [ ] Structured seed/camera/viewport label matching the current runtime resolution has no warnings.
- [ ] Invalid/duplicate seed, empty camera and malformed viewport are reported.
- [ ] Labelled 1280x720 viewport during a 1920x1080 capture warns about resolution.
- [ ] Capture with warnings requires manual confirmation, without silently changing the label.
- [ ] Free-form historical labels remain loadable and can still be used after acknowledgement.
