## Easy to use save converter for Hogwarts Legacy (Gamepass <-> Steam)

<img width="674" height="383" alt="hogwarts-legacy-save-converter_X78jXIpxIL" src="https://github.com/user-attachments/assets/4a69829e-8b2b-412d-a450-85f9b89d6da3" />

Based on [hogwarts-legacy-save-convertor](https://github.com/NativeSmell/hogwarts-legacy-save-convertor) by NativeSmell, this simple Command Line utility takes the guess work out of locating folders by automatically locating the GamePass/Steam Hogwarts Legacy save folders, converting the save files to a desired format, and then automatically (after confirmation by the user) migrates the saves either from GamePass to Steam, or from Steam to GamePass.

## How to use:
  - Play the game at least once on both GamePass and on Steam. Make sure there is at least one auto and manual save on each.
  - Download [the latest release](https://github.com/Axolittles/hogwarts-legacy-save-converter/releases/download/release/hogwarts-legacy-save-converter.exe), then run it.
    - If Windows SmartScreen blocks you from starting it, Right Click the EXE, go to Properties, then near the bottom check the box labeled "Unblock" and click Apply, then try to run it again.
    - If you run into an error about missing frameworks, make sure to download the .NET 9 Runtime linked above.
  - Follow the prompts to migrate your save to the correct user. If you cancel the migration the target and working folders will be opened automatically instead.
  - (GamePass -> Steam only) After the migration, the working folder is only cleaned up if you have performed an auto-migration. If you have performed a manual migration, delete the working folder.
  - (Steam -> GamePass) No working folder is used, nothing to clean up.
  - NOTE: This is based on the Save Converter by NativeSmell. **I'm unable to test further as I no longer have GamePass**, so I'm not sure what happens in certain cases such as if there are multiple GamePass/Xbox users with saves. This has only been tested with a single Xbox GamePass user. **I cannot test if Steam -> GamePass migration works!**
      
## Requirements:
  - Windows (Tested on Windows 10 LTSC)
  - [.NET 9.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) ([Windows x64 Installer](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-9.0.10-windows-x64-installer))
  - Save at least once in both HL in GamePass and on Steam

## How to build your own:
  - Download the [source code](https://github.com/Axolittles/hogwarts-legacy-save-converter/archive/refs/heads/main.zip) and open it up in VS2022.
  - Build in VS2022.
  - Head to the root project folder "hogwarts-legacy-save-converter", open Windows PowerShell, and execute the command

```
  dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

  - Navigate to the listed output folder and run the generated exe.

## Extra

Automatic migration when single Steam user:

<img width="674" height="406" alt="hogwarts-legacy-save-converter_nQ3ALFZ1yh" src="https://github.com/user-attachments/assets/b8f1d203-da79-4dd1-b8d8-e23cfd540b40" />

Automatic migration when multiple Steam users:

<img width="674" height="491" alt="hogwarts-legacy-save-converter_qsGvoSZ2mD" src="https://github.com/user-attachments/assets/ccf757cf-ce2f-42fd-801a-d83b8cec828b" />
