# SmartMouth

## Build

```bash
dotnet build SmartMouth.slnx -c Release
```

## Publish executable (.exe)

Run from repository root:

```bash
bash publish.sh
```

On Windows:

```bat
publish.bat
```

This will execute `dotnet publish` for `SmartMouth.App` and output a runnable exe to:

`publish/win-x64/SmartMouth.App.exe`
