# MonsterSurvival asset layout

Production assets live under this single project root. Keep third-party packages outside it.

- `Art/`: sprites, animations, map art, boss art and UI art.
- `Audio/`: mixers, music and sound effects.
- `Code/Runtime/`: shared runtime code used across features.
- `Code/Editor/`: shared installers, audits and editor-only utilities.
- `Content/Data/`: shared ScriptableObject data.
- `Content/Materials/`: shared materials.
- `Content/Prefabs/`: shared prefabs.
- `Content/Scenes/`: production scenes included in Build Profiles.
- `Features/`: feature-owned code and content, grouped by gameplay domain.
- `Settings/`: render-pipeline settings, templates and Build Profiles.

## Rules

1. Move Unity assets together with their `.meta` files so GUID references remain stable.
2. Put editor-only C# below an `Editor` directory.
3. Store generated backups and QA exports outside `Assets`; these paths are ignored by Git.
4. Use full scene paths for runtime scene loading and Build Profile tooling.
5. Do not add historical scene copies, generated reports or package examples to `Assets`.
