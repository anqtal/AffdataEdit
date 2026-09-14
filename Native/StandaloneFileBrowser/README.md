# macOS file browser plugin

Source: https://github.com/gkngkc/UnityStandaloneFileBrowser/tree/04a5d49ed2545556da8a7192e86c69bd47641f10/Plugins/MacOS/StandaloneFileBrowser

`Plugin.mm` and `Plugin.pch` are unmodified upstream sources (MIT license).
The bundled plugin was Intel-only and could not load in Apple Silicon players.
Rebuild the universal arm64/x86_64 binary with Xcode command-line tools:

```sh
python3 Native/StandaloneFileBrowser/build.py
```

The script updates the plugin in Assets; rebuild the Unity player afterwards.
Minimum macOS version: 11.0. Manual reference counting matches the upstream build.
