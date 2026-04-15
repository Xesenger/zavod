# Projects Web Baseline — 2026-04-15

This checkpoint is the safe return point before full Projects Web UI migration.

Proven live at this point:

- atomic composer submission is active
- artifact content reaches the real model path
- Cyrillic survives the live Chats artifact path end-to-end
- Projects can continue on the current typed backend and snapshot-driven UI path

Frozen foundation for the next stage:

- atomic submission
- artifact -> execution
- UTF-8 path
- typed work-cycle truth
- snapshot-store-first semantics
- keyed incremental rendering

Live proof artifacts:

- `artifacts/live-proof/chats-live-proof-external.json`
- `artifacts/live-proof/chats-live-proof-external.png`

Planned next step:

- migrate Projects UI to web in narrow vertical slices without changing foundation unless a separate audit proves it is necessary
