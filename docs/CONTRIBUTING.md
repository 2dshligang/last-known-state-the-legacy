# Contributing

## Branches

Create short-lived branches from the default branch:

- `feature/<area>-<summary>`
- `fix/<area>-<summary>`
- `docs/<summary>`

## Pull requests

Keep pull requests focused and identify the ownership area. Include:

- purpose and player impact;
- files and systems changed;
- tests actually executed and their results;
- Unity Editor verification performed;
- screenshots or video for visual changes;
- manual verification steps;
- dependencies, risks and known limitations.

Do not merge changes that silently cross ownership boundaries. Main scene changes require Developer 1. Authoritative gameplay contract changes require Developer 2.

## Unity practices

- Commit `.meta` files with their assets.
- Never commit `Library`, `Temp`, `Logs`, generated IDE files or local settings.
- Use text serialization and visible meta files.
- Avoid editing the same scene or prefab concurrently.
- Prefer additive scenes and isolated prefabs for parallel work.
- Ensure editor generators are repeatable and do not duplicate generated objects.

## Content review

Before merging, verify that player-facing content is English, no spoken dialogue dependency exists and scientific statements follow `SCIENTIFIC_INTEGRITY.md`.

