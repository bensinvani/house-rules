# HouseRules

Unity 6.3 LTS (6000.3.22f1), URP 17.3.0, new Input System.
Mobile game: casino games (blackjack, roulette) + board games
(backgammon, checkers, chess).

Shipping target is Android (module installed, and the active build target since
2026-08-14). iOS is not installed. The playable scene is
`Assets/Scenes/Blackjack.unity`; APK builds land in `Builds/` (gitignored).

## Driving the Editor

The Unity Pipeline package (com.unity.pipeline 0.5.0-exp.1) is installed. The
Editor must be running. 142 commands are available via
`unity command <name> --flag value`. Run `unity command` with no arguments to
list them.

You are expected to inspect and verify the Editor yourself. Do not ask me to
press play, check the console, or describe what I see.

### Prerequisite: the `unity` CLI
Every command below goes through the `unity` CLI, which is installed separately
from the package. If `unity` is not on PATH, install it (PowerShell):

    $env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex

### Autotick
The package auto-enables autotick at startup, so the Editor keeps ticking while
unfocused. Only run `set_autotick --enable true` if commands start hanging.

### Core loop
- `get_scene_hierarchy` / `find_gameobjects` / `get_selection` — read scene state
- `eval --code "<C#>"` — run C# live via Roslyn, no domain reload
- `get_console_logs --severity error` — check for errors after any change
- `capture_game_view` — see the result; returns a base64 PNG inline

Two gotchas, both hit in practice:
- On PowerShell, pass `--code` in a *single-quoted* string. Backslash-escaped
  double quotes get mangled by the shell before the CLI sees them.
- `--save_path` is resolved against the authoring root (`Assets/`) and rejects
  anything outside the project root. A path under `Assets/` lands there and
  gets imported, so delete it (and its `.meta`) when it was only a check.

### Creating scripts (order matters)
`create_script` writes the file but the type does not exist yet.
Sequence: `create_script` -> `recompile` -> poll `recompile_status` until it
reports `completed` -> `attach_script`. Attaching before the recompile finishes
returns a recoverable error.

### Running tests
- EditMode: `unity command run_tests --mode editor --filter <TestClass>`
  (`--filter_type assembly` for a whole assembly; `--timeout 600` for long suites).
- PlayMode: a synchronous run returns `Total: 0` with `success: true` and runs
  **nothing** — PlayMode tests need play-mode entry, which cannot complete inside
  a blocking call. Always run `--async_tests true`, then poll
  `unity command test_status` until `completed`. Treat `Total: 0` as "did not
  run", never as "passed"; cross-check against `list_tests --mode playmode`.

### Destructive commands
These take `--confirm true`, and most also take `--dry_run true`:

    build, switch_build_target, delete_asset, copy_asset, import_asset,
    write_text_file, package_add, package_remove,
    bake_lighting, bake_navmesh, bake_occlusion_culling,
    clear_baked_lighting, clear_navmesh, clear_occlusion_culling,
    create_asset, create_animation_clip, create_animator_controller,
    create_timeline, remove_animation_curve,
    set_audio_settings, set_build_settings, set_graphics_settings,
    set_input_settings, set_material_properties, set_physics_settings,
    set_player_settings, set_quality_settings, set_tags_layers,
    set_time_settings

Always run `--dry_run true` first and show me the output before confirming.

### If the CLI is unavailable
The Editor's HTTP server can be driven directly. The port and bearer token live
in `Library/Pipeline/.unity-pipeline-port` (`port`, `evalToken`). POST to
`http://127.0.0.1:<port>/api/exec` with
`{"command":"<name>","args":{...}}` and header
`Authorization: Bearer <evalToken>`. `GET /api/commands` lists everything.
Use this as a fallback for diagnosis, not as the normal path.

## Rules
- Never edit files under Library/, Temp/, obj/, or Logs/
- .meta files are committed; never delete one without its asset
- Prefer pipeline commands over hand-editing .unity or .prefab YAML
