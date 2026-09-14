# Built-in Arcade Alpha skin

Copied from `/Volumes/T7/ReferenceProject/Arcade-Alpha/Assets`.
Existing texture GUIDs are retained; JPG textures are decoded with macOS sips and stored as PNG without further lossy compression.
Arc colors use the Normal palette from `Scripts/Gameplay/Utility/ColorLibrary.cs`.
Long-particle colors use the start color multiplied by the lifetime gradient from `Prefabs/Player/LongNoteEffectPrefab.prefab`.
Light/Conflict hit atlases use 4x4 frames; the SFX atlas uses the existing 6x5 setup.
The existing VFX Graph playback remains in use.

These imported resources retain their original owners and licenses; the project MIT license does not relicense them.

| Built-in asset | Alpha source asset |
|---|---|
| `Note/TapNote/TapNoteLight.png` | `LowiroLimited/Note/Tap/TapNote.png` |
| `Note/TapNote/TapNoteDark.png` | `LowiroLimited/Note/Tap/TapNoteDark.png` |
| `Note/HoldNote/HoldNoteLight.png` | `LowiroLimited/Note/Hold/HoldNote.png` |
| `Note/HoldNote/HoldNoteDark.png` | `LowiroLimited/Note/Hold/HoldNoteDark.png` |
| `Note/HoldNote/HoldNoteLightHighlight.png` | `LowiroLimited/Note/Hold/HoldNoteHighlight.png` |
| `Note/HoldNote/HoldNoteDarkHighlight.png` | `LowiroLimited/Note/Hold/HoldNoteDarkHighlight.png` |
| `Note/ArcTap/ArcTapLight.png` | `Textures/Gameplay/Note/tap_l.png` |
| `Note/ArcTap/ArcTapDark.png` | `Textures/Gameplay/Note/tap_d.png` |
| `Note/ArcBody/ArcBody.png` | `LowiroLimited/Note/Arc/arc_body.png` |
| `Note/ArcBody/ArcBodyHighlight.png` | `LowiroLimited/Note/Arc/arc_body_hi.png` |
| `Note/ArcCap.png` | `LowiroLimited/Note/Arc/arc_cap.png` |
| `Note/HeightIndicator.png` | `LowiroLimited/Note/Arc/height_indicator.png` |
| `Particle/ParticleArc.png` | `Textures/Gameplay/Effect/particle_arc.png` |
| `Particle/ParticleSfxTap.png` | `Scripts/Gameplay/EffectParticle/note_sfx.png` |
| `Particle/ParticleNote/NoteParticles.png` | `LowiroLimited/Particle/note_light.png` |
| `Particle/ParticleNote/NoteParticlesConflict.png` | `LowiroLimited/Particle/note_conflict.png` |
| `Background/BaseLight.png` | `Textures/Gameplay/Backgrounds/BaseLight.jpg` |
| `Background/BaseConflict.png` | `Textures/Gameplay/Backgrounds/BaseConflict.jpg` |
| `BackgroundDarken.png` | `Textures/Gameplay/Backgrounds/bg_darken.png` |
| `Note/SfxArcTap/SfxArcTapNoteLight.png` | `Textures/Gameplay/Note/SFXArcTap/sfx_l_note.jpg` |
| `Note/SfxArcTap/SfxArcTapCoreLight.png` | `Textures/Gameplay/Note/SFXArcTap/sfx_l_core.jpg` |
| `Note/SfxArcTap/SfxArcTapNoteDark.png` | `Textures/Gameplay/Note/SFXArcTap/sfx_d_note.jpg` |
| `Note/SfxArcTap/SfxArcTapCoreDark.png` | `Textures/Gameplay/Note/SFXArcTap/sfx_d_core.jpg` |

## Validation

Unity 6000.6.0f1 imported the assets and compiled the scripts with no console errors.
Play mode startup succeeded; the Game view background was checked in `Captures/alpha-skin.png`.
The loaded scene contains both particle texture references and the Alpha Arc palette.
All imported texture dimensions match their source; the 17 PNG source files match decoded pixels exactly.
The six JPEG conversions differ from Pillow decoding by at most 4/255 per channel due to decoder differences.
Full chart playback and a standalone rebuild were not performed for this asset change.
