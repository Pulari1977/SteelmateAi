# SteelMate AI

A chat window for Tekla Structures. You type what you want, and Claude does it in Tekla using about 58 tools: create and modify parts, check numbering, find clashes, make drawings, and export NC, IFC and reports. It asks before it changes your model.

## Latest version: 1.9.0 (2026-11-07)

Works with **Tekla Structures 2025 and 2026**. It uses whichever Tekla is running.

**Download:** [releases/1.9.0/SteelMateAI-Setup-1.9.0.exe](releases/1.9.0/SteelMateAI-Setup-1.9.0.exe). Click the file, then **Download raw file**.

What changed in each version: [CHANGELOG.txt](CHANGELOG.txt)

## Install
1. Run the Setup file. If Windows says "Windows protected your PC", click **More info → Run anyway**. The file is not signed.
2. No admin rights are needed. It installs to `%LOCALAPPDATA%\Programs\SteelMateAI` and adds Start menu and desktop shortcuts.
3. Start SteelMate AI. The first time, open **Settings (gear) → Claude**, paste your API key and press **Save and reconnect**. After that it connects by itself every time. (If the key is already in the Windows variable `ANTHROPIC_API_KEY`, no setup is needed.)

## No API key? Use your Claude Pro/Max plan
A Claude subscription is not an API key, so the SteelMate chat window cannot use it. Instead:
1. Install the [Claude desktop app](https://claude.ai/download) and sign in.
2. In SteelMate AI: **Settings → Connect to Claude Desktop**.
3. Fully quit Claude Desktop (right-click its icon near the clock → Quit) and open it again.
4. Open Tekla with a model and chat in Claude Desktop. It now has the Tekla tools.

## Uninstall
Use Windows **Settings → Apps → SteelMate AI → Uninstall**. It asks whether to also delete your settings, keys and chat history.

## Update
Run the newer Setup file. It keeps your settings and the tools you built.

## Build from source
See [HOW-TO-RELEASE.txt](HOW-TO-RELEASE.txt). The code is compiled against Tekla 2025 (`TeklaVersion` in `SteelMateAI.csproj`). At run time `TeklaMcp/TeklaLocator.cs` loads the Tekla API files from the running or newest installed Tekla, so one build serves 2025 and 2026.

Older installers are kept in `releases/<version>/`.
