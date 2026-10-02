# AFF Slide extension

AffdataEdit extends AFF with an independent region note inspired by SpcDataPlay's SkyArea:

```text
slide(startTime,endTime,startCenter,startWidth,endCenter,endWidth,leftCurve,rightCurve,isfloor);
```

Example:

```text
AudioOffset:0
-
timing(0,120,4);
slide(1500,2300,0.35,0.3,0.6,0.5,1,2);
```

Times are AFF milliseconds. Centres and full widths use normalized sky coordinates 0–1; `isfloor=false` fixes height at SkyInput; `true` places the Slide on the lane-note plane across the four main lanes. The optional ninth argument defaults to false for old charts. Curves independently control the left and right boundaries: 0 = linear, 1 = sin ease out, 2 = cos ease in. Each endpoint must stay inside 0–1, widths must be positive, boundaries must not cross anywhere during interpolation, and duration must be 2–2147483647 ms. Validation checks analytic width extrema, including when the edges use different curves.

`slide` may occur at the chart root or inside an existing `timinggroup`. It follows that group's BPM, noinput and hidegroup behaviour. Slides in the same timing group and plane are ordered by start time (stable for ties). Consecutive segments share a logical group when their end/start times differ by at most 2 ms, regardless of horizontal overlap. Exact-time seams use the intersection of boundary ranges to suppress internal edges; a non-overlapping seam keeps its edges. Each segment retains its own mesh and selection. Only the group head gets entry brackets. This adapts SPC continuity to AFF time-sorted saving; SPC itself assigns groups in source order. Branches are not merged into one logical group. Players without this AFF extension cannot play these notes.

## Editing

Use the existing 点立得 marking menu and select **Slide 区域**. Click a start time, choose a start centre, choose the end time, then choose the end centre. New notes have width 1/3. Escape cancels the unfinished operation.

Select a Slide to edit it in the existing left-hand **ValueEdit** window:

- Time, end time, timing group and time reposition buttons are the original controls.
- Start/end range use the original input style: `centre,width`.
- Left/right curves use the existing dropdown style.
- **地面 Slide** uses the original toggle style to switch between the sky and lane-note planes.
- Drag the four handles in the embedded preview to edit each boundary; bottom = start, top = end. Dragging snaps to 1/48. One completed drag is one undo step.
- Multiple Slides support batch range/curve editing. A mixed note selection retains the original common fields.

Selection, range selection, deletion, copy/cut/paste, mirroring, time snapping, undo/redo, timing-group deletion/undo and AFF save/reload include Slides. Invalid range edits leave the note unchanged. Time edits/snapping cannot collapse a Slide to zero length.

## Rendering and preview

The body uses the source SkyArea mesh/UV contract and body shader, original dot/architect textures, entry brackets, and a ground shadow. Per user preference, the shadow uses Arcade Alpha Arc tints (black 50/255 on Light, white 20/255 on Conflict) instead of the SPC grey shadow. Geometry integrates AFF BPM and scroll speed. Unconsumed geometry moves with its head; active auto-preview trims the elapsed portion. Sampling is 5 ms for ordinary notes, bounded to about 8192 quads for very long notes. Mesh colliders update while paused for selection, rather than cooking every playback frame. Body, shadow, brackets and selection share the existing AFF ±100 world-Z cutoff, with a fade over the future-side 90–100 interval. Ray selection rejects clipped portions of crossing meshes; whole-mesh bounds culling remains safe for reversed BPM.

With **AUTO** enabled, the interpolated active range drives the source sky-line glow and animated triangle grid. Segments in one logical group share a glow object and transition without overlapping release effects. Independent simultaneous groups retain separate glow ranges. Sky and floor feedback use separate grids. Release fades over 0.3 seconds; seeks/restarts clear feedback. No-input groups render without hit consumption. Slides now contribute to the AFF preview combo and score denominator. SkyArea-style ticks use each segment's start BPM (half beats below 249.99 BPM, full beats above), multiplied by AFF TimingPointDensityFactor; a remainder of at least 2 ms gets an end tick. Connected segments do not add an extra head tick. Zero BPM gets one end tick, negative BPM uses its magnitude, and noinput groups contribute nothing. Counts are recalculated from chart time, so seeking and edits remain consistent. This remains editor preview scoring, not SPC manual cursor acquisition or danger/miss judgement.

Adapters account for AFF's reversed X/Z axes and sky width, map source world Z to its judgement-line origin, and use AffdataEdit's Arc/Shadow/Effect sorting layers. Body and glow are two-sided for editor camera views. Source shaders are retained under Resources so standalone builds include them.

## Asset provenance

The local reference repository `/Volumes/T7/SpcDataPlay` was read only. Files copied into `Assets/Resources/SlideOriginalReference`:

- `SpcSkyArea.shader`, `SpcSkyLineGlow.shader`, `SpcJudgementGrid.shader`, `SpcBlur.shader`, and the two `Reference/*.hlsl` includes from `Assets/SpcDataPlay/Shaders`.
- `architect_lines.png`, `dot_grid.png` from `Assets/SpcDataPlay/Original/Gameplay/Sky`.
- `square_bracket.png` and `bracket.obj` from the reference's `Original/Gameplay/FX` and `Meshes` directories.
- `ArcSlideJudgementGrid.cs` adapts `Runtime/View/SpcJudgementGrid.cs`, removing reference-only profiler dependencies and allowing multiple independent ranges.

`SlideShadow.shader` and `SlideBracket.shader` are small local unlit adapters. Reference-specific original-asset provenance remains applicable; these copies do not imply a new licence grant. The source's SkyArea hit effect uses a grid and range glow, not an Arc particle cloud.

## Verification

EditMode tests in `ArcSlideTests` cover invalid format/geometry, narrow fractional widths in non-English locales, parse/write/parse in a timing group, independent curves, clone/assign and continuity. Play-mode verification covers the reused editor, rendering layers, selection collision and auto-consumption. Development captures and the saved temporary preview chart are under the ignored `Captures/` directory.

### Hit audio

During auto playback, a Slide group plays once when crossing its head: floor uses the Tap sound, sky uses the Arc sound, through the existing effect AudioSource and skin/volume settings. Connected segments do not retrigger. Paused scrubbing, backwards jumps, and forward seeks exceeding 250 ms are silent. Playback starting exactly at a head also triggers once; starting midway does not.
