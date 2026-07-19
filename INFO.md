Для Vs Code установить следующие расширения:

- C#
- С# Tools for Godot


Установка VS Code как редактора по умолчанию:

- Редактор -> Настройки редактора
- Включить ползунок "Расширенные настройки"
- В списке настроек выбрать "Dotnet"
- В External Editor выбрать VS Code

В godot проекте для Vs Code необходимо создать директорию `.vscode`.
В эту директорию добавить два файла:

```JSON
//.vscode/launch.json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Play",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "C:\\Users\\moroz\\Desktop\\Godot_v4.7-stable_mono_win64\\Godot_v4.7-stable_mono_win64.exe", // путь к исполняему файлу godot
            "args": [],
            "cwd": "${workspaceFolder}",
            "stopAtEntry": false,
        }
    ]
}
```



```JSON
//.vscode/tasks.json
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "build",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build"
            ],
            "problemMatcher": "$msCompile"
        }
    ]
}
```

```json
//.vscode/settings.json
// Файлы формата .uid и .godot будут не видны в vscode
{   
    "godotTools.editorPath.godot4": "ur_godot_path",
     "files.exclude": {
        "**/*.uid": true,
        "**/.godot": true
    }
}
```