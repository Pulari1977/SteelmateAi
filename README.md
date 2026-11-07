# SteelMate AI

A chat window for Tekla Structures. You type what you want, and Claude does it in Tekla using about 58 tools: create and modify parts, check numbering, find clashes, make drawings, and export NC, IFC and reports. It asks before it changes your model.

## Latest version: 1.6.0 (2026-11-07)

**Download:** [releases/1.6.0/SteelMateAI-Setup-1.6.0.exe](releases/1.6.0/SteelMateAI-Setup-1.6.0.exe). Click the file, then **Download raw file**.

What changed in each version: [CHANGELOG.txt](CHANGELOG.txt)

## Install
1. Run the Setup file. If Windows says "Windows protected your PC", click **More info → Run anyway**. The file is not signed.
2. No admin rights are needed. It installs to `%LOCALAPPDATA%\Programs\SteelMateAI` and adds Start menu and desktop shortcuts.
3. Open Tekla with a model, start SteelMate AI, go to **Settings (gear) → Claude**, paste your API key and press **Save and reconnect**.

## Uninstall
Use Windows **Settings → Apps → SteelMate AI → Uninstall**. It asks whether to also delete your settings, keys and chat history.

## Update
Run the newer Setup file. It keeps your settings and the tools you built.

## Build from source
See [HOW-TO-RELEASE.txt](HOW-TO-RELEASE.txt). The Tekla version is set in `SteelMateAI.csproj` (`TeklaVersion`).
