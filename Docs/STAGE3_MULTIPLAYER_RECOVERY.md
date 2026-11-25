# Stage 3 — bounded multiplayer recovery

Verified gaps in NetSession: a client timed out by clearing its wait flag before a full snapshot arrived; gaps could request sync repeatedly and duplicate command batches could unblock a pending move. Malformed Start was not caught. Closed sessions could still process queued transport events in the same poll, and host/client Perform did not consistently reject closed sessions.

- ClientRecovery uses monotonic time (injectable in tests). Submit waits 6 seconds; timeout or command gap enters full-sync wait. At most two sync requests, 6 seconds apart; no automatic resubmission of a move. If still unanswered, close with a message and the existing return-to-menu flow. Only actual new commands or a valid snapshot clear wait; during full sync ignore incremental batches. A stale shorter snapshot cannot roll back accepted history.
- Malformed Start/Sync closes gracefully. Duplicate Start/lobby packets after start ignored; invalid seat updates rejected; map publication follows successful reconstruction. Closed sessions ignore received/peer events and reject actions and lobby mutations.
- Eight new Edit Mode cases (five LoopbackTransport session scenarios and three deterministic wait-policy cases). Existing protocol version and packet shape unchanged.
- Not an explicit request/ack protocol: progress from host remains the acknowledgement mechanism; no change to TCP/Steam/UGS identity, nickname-based reconnect, host migration, authentication or transport-level retries.

Manual QA: actual LAN/Steam/UGS, drop/delay messages, host disconnect, rejoin and bot takeover, timeout and return to menu. Loopback tests do not prove external network behavior. No new server or SDK deployment.
