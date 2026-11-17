# Handoff LUNET-511 — SAF document picker failures (2026-11-16)

Scope: only apps/lunet2d in AbnerCruz/Ecosystem. Main, root/local AGENTS, ROADMAP, PRs #959/#960/#961/#962 and product UX audit consulted. Reference: Android Storage Access Framework (ACTION_CREATE_DOCUMENT); local-first design and device-specific document providers. PR #961 is merged; #962 remains draft.

## Implemented
- MainActivity.AssetLibrary.cs: catch ActivityNotFoundException and Java.Lang.SecurityException when launching ACTION_CREATE_DOCUMENT.
- Clear pending asset ID/version and present an actionable message without destroying Library contents.
- CHANGELOG and ROADMAP updated; no version stage changes or forced merges.

## Evidence and remaining gates
- Commits: 39cb0a8, 230ea69, 639e0ac. The previous head 2afe0d9 had four successful CI workflows; **the new head must be checked independently**.
- No Android device attached; test missing document provider and security-blocked provider on Android 13. Test cancel/retry, process recreation, successful export ZIP and ZIP hash verification.
- This is a narrow crash-safety fix, not evidence of independent Studio editing. No new automated Android test added for provider launch failure; keep LUNET-511 open.
- Next independent increment: decouple one Studio session from project persistence, with a core test covering Library save and project copy.

No release APK is claimed without published release assets.
