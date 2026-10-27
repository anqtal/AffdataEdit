# AFF Slide extension

AffdataEdit extends AFF with an independent region note inspired by SpcDataPlay's SkyArea:

```text
slide(startTime,endTime,startCenter,startWidth,endCenter,endWidth,leftCurve,rightCurve[,height]);
```

Example:

```text
AudioOffset:0
-
timing(0,120,4);
slide(1500,2300,0.35,0.3,0.6,0.5,1,2);
```

Times are AFF milliseconds. Centres and full widths use normalized sky coordinates. The optional ninth argument is a height enum: `0` = ground (Arc Y = -2/9), `1` = normal sky (Arc Y = 1), `2` = high sky (Arc Y = 1.61, the widened sky height). Omitting it defaults to `1`. Like Arc coordinates, these heights remain fixed during an enwiden transition; choose `2` to author a high Slide. Curves independently control the left and right boundaries: 0 = linear, 1 = sin ease out, 2 = cos ease in, 3 or b = smoothstep. Each endpoint must stay inside -0.5–1.5, widths must be positive, boundaries must not cross anywhere during interpolation, and duration must be 2–2147483647 ms. Validation checks width extrema, including when the edges use different curves.

### Automatic migration from isfloor

Old charts still open: the former boolean ninth argument is translated during parsing (`true` → `0`, `false` → `1`), and eight-argument Slides remain normal sky. Saving the chart automatically writes every Slide with the numeric height, including Slides inside timing groups. Times, ranges, curves and timing groups are preserved; no manual text replacement is needed. Save and re-upload migrated charts before the planned retirement of the boolean syntax on 2026-12-18. The compatibility reader has no automatic date cutoff.

```text
Old: slide(1500,2300,0.35,0.3,0.6,0.5,1,2,true);
New: slide(1500,2300,0.35,0.3,0.6,0.5,1,2,0);
Old: slide(1500,2300,0.35,0.3,0.6,0.5,1,2,false);
New: slide(1500,2300,0.35,0.3,0.6,0.5,1,2,1);
```

`slide` may occur at the chart root or inside an existing `timinggroup`. It follows that group's BPM, noinput and hidegroup behaviour. Slides in the same timing group and plane are ordered by start time (stable for ties). Consecutive segments share a logical group when their end/start times differ by at most 2 ms, regardless of horizontal overlap. Exact-time seams use the intersection of boundary ranges to suppress internal edges; a non-overlapping seam keeps its edges. Each segment retains its own mesh and selection. Only the group head gets entry brackets. This adapts SPC continuity to AFF time-sorted saving; SPC itself assigns groups in source order. Branches are not merged into one logical group. Players without this AFF extension cannot play these notes.

## Editing

Use the existing 点立得 marking menu and select **Slide**. Click a start time, choose a start centre, choose the end time, then choose the end centre. New notes have width 1/3. Escape cancels the unfinished operation.

Select a Slide to edit it in the existing left-hand **ValueEdit** window:

- Time, end time, timing group and time reposition buttons are the original controls.
- Start/end range use the original input style: `centre,width`.
- Left/right curves use the existing dropdown style.
- **高度** uses a dropdown for **地面**, **普通天空**, and **高空（enwiden）**. Batch edits show **混合** for mixed heights and support undo/redo.
- Drag the four handles in the embedded preview to edit each boundary; bottom = start, top = end. Dragging snaps to 1/48. One completed drag is one undo step.
- Multiple Slides support batch range/curve editing. A mixed note selection retains the original common fields.

Selection, range selection, deletion, copy/cut/paste, mirroring, time snapping, undo/redo, timing-group deletion/undo and AFF save/reload include Slides. Invalid range edits leave the note unchanged. Time edits/snapping cannot collapse a Slide to zero length.

## Rendering and preview

The body uses the source SkyArea mesh/UV contract and body shader, original dot/architect textures, entry brackets, and a ground shadow. Per user preference, the shadow uses Arcade Alpha Arc tints (black 50/255 on Light, white 20/255 on Conflict) instead of the SPC grey shadow. Geometry integrates AFF BPM and scroll speed. Unconsumed geometry moves with its head; active auto-preview trims the elapsed portion. Sampling is 5 ms for ordinary notes, bounded to about 8192 quads for very long notes. Mesh colliders update while paused for selection, rather than cooking every playback frame. Body, shadow, brackets and selection share the existing AFF ±100 world-Z cutoff, with a fade over the future-side 90–100 interval. Ray selection rejects clipped portions of crossing meshes; whole-mesh bounds culling remains safe for reversed BPM.

With **AUTO** enabled, the interpolated active range drives the source sky-line glow and animated triangle grid. Segments in one logical group share a glow object and transition without overlapping release effects. Independent simultaneous groups retain separate glow ranges. Ground, normal sky and high sky use separate chains and feedback grids; both sky heights cast their shadow on the ground. Release fades over 0.3 seconds; seeks/restarts clear feedback. Like AffdataPlay, `noinput` Slides trim passed geometry even with AUTO disabled, without hit feedback or scoring; `noclip` keeps the complete geometry, including during AUTO preview. Slides now contribute to the AFF preview combo and score denominator. SkyArea-style ticks use each segment's start BPM (half beats below 249.99 BPM, full beats above), multiplied by AFF TimingPointDensityFactor; a remainder of at least 2 ms gets an end tick. Connected segments do not add an extra head tick. Zero BPM gets one end tick, negative BPM uses its magnitude, and noinput groups contribute nothing. Counts are recalculated from chart time, so seeking and edits remain consistent. This remains editor preview scoring, not SPC manual cursor acquisition or danger/miss judgement.

Adapters account for AFF's reversed X/Z axes and sky width, map source world Z to its judgement-line origin, and use AffdataEdit's Arc/Shadow/Effect sorting layers. Body and glow are two-sided for editor camera views. Source shaders are retained under Resources so standalone builds include them.

## Asset provenance

The local reference repository `/Volumes/T7/SpcDataPlay` was read only. Files copied into `Assets/Resources/SlideOriginalReference`:

- `SpcSkyArea.shader`, `SpcSkyLineGlow.shader`, `SpcJudgementGrid.shader`, `SpcBlur.shader`, and the two `Reference/*.hlsl` includes from `Assets/SpcDataPlay/Shaders`.
- `architect_lines.png`, `dot_grid.png` from `Assets/SpcDataPlay/Original/Gameplay/Sky`.
- `square_bracket.png` from the reference's `Original/Gameplay/FX` directory. Bracket geometry is now generated by `Affdata.Core`; the unused `bracket.obj` copy has been removed.
- `ArcSlideJudgementGrid.cs` adapts `Runtime/View/SpcJudgementGrid.cs`, removing reference-only profiler dependencies and allowing multiple independent ranges.

`SlideShadow.shader` and `SlideBracket.shader` are small local unlit adapters. Reference-specific original-asset provenance remains applicable; these copies do not imply a new licence grant. The source's SkyArea hit effect uses a grid and range glow, not an Arc particle cloud.

## Verification

EditMode tests in `ArcSlideTests` cover invalid format/geometry, narrow fractional widths in non-English locales, parse/write/parse in a timing group, independent curves, clone/assign and continuity. Play-mode verification covers the reused editor, rendering layers, selection collision and auto-consumption. Development captures and the saved temporary preview chart are under the ignored `Captures/` directory.

### Hit audio

During auto playback, a Slide group plays once when crossing its head: floor uses the Tap sound, sky uses the Arc sound, through the existing effect AudioSource and skin/volume settings. Connected segments do not retrigger. Paused scrubbing, backwards jumps, and forward seeks exceeding 250 ms are silent. Playback starting exactly at a head also triggers once; starting midway does not.
