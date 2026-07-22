#!/bin/bash
# Headless-тест лобби-потока для The Escape Game.
# Возвращает exit code Godot: 0 — успех, 1+ — ошибка.

set -e

GODOT="/mnt/c/Programs/Godot_v4.7-stable_mono_win64/Godot_v4.7-stable_mono_win64.exe"
PROJECT="/mnt/c/Users/Admin/Desktop/probe/Games/the-escape-game"

cd "$PROJECT"
timeout 60 "$GODOT" --headless tests/lobby_flow.tscn
