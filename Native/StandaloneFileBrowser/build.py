"""Build the native file browser for both supported macOS CPU architectures."""
from pathlib import Path
import os
import subprocess
import tempfile

source = Path(__file__).resolve().parent
project = source.parents[1]
bundle = project / "Assets/SFB/Plugins/StandaloneFileBrowser.bundle"
target = bundle / "Contents/MacOS/StandaloneFileBrowser"
with tempfile.TemporaryDirectory(prefix="affdataedit-sfb-") as directory:
    output = Path(directory) / "StandaloneFileBrowser"
    subprocess.run([
        "xcrun", "clang++", "-bundle", "-fno-objc-arc", "-O2",
        "-arch", "arm64", "-arch", "x86_64", "-mmacosx-version-min=11.0",
        "-framework", "AppKit", "-framework", "Foundation",
        str(source / "Plugin.mm"), "-o", str(output),
    ], check=True)
    architectures = subprocess.check_output(["xcrun", "lipo", "-archs", str(output)], text=True).split()
    if set(architectures) != {"arm64", "x86_64"}:
        raise RuntimeError(f"Unexpected plugin architectures: {architectures}")
    subprocess.run(["codesign", "--force", "--sign", "-", str(output)], check=True)
    subprocess.run(["codesign", "--verify", "--strict", str(output)], check=True)
    # Unity signs the enclosing bundle when building the player.
    # Copy before replacing so the update also works across filesystem volumes.
    staged = target.with_suffix(".tmp")
    staged.write_bytes(output.read_bytes())
    staged.chmod(0o755)
    os.replace(staged, target)
print(f"Updated universal plugin: {target}")
