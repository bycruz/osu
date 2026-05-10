# osu! (lazer) Architecture Notes

## Project Structure

```
osu.Game/                    # Core game: screens, graphics, config, scoring, mods base classes
osu.Game.Rulesets.Osu/       # osu! standard ruleset: mods, objects, judgements, UI, skinning
osu.Game.Rulesets.Catch/     # Catch the Beat ruleset
osu.Game.Rulesets.Mania/     # osu!mania ruleset
osu.Game.Rulesets.Taiko/     # osu!taiko ruleset
osu.Desktop/                 # Desktop launcher
```

## Key Namespaces

- `osu.Game.Rulesets.Mods` — Base `Mod` class, `IApplicableTo*` interfaces, shared mods (`ModHidden`, `ModHardRock`, etc.)
- `osu.Game.Rulesets.Osu.Mods` — osu!-specific mods (`OsuModHidden`, `OsuModSynesthesia`, etc.)
- `osu.Game.Rulesets.Objects.Drawables` — `DrawableHitObject`, `DrawableHitObject<T>`
- `osu.Game.Rulesets.Osu.Objects.Drawables` — `DrawableOsuHitObject`, `DrawableHitCircle`, `DrawableSlider`, etc.
- `osu.Game.Rulesets.Osu.UI` — `OsuPlayfield`, `DrawableOsuRuleset`
- `osu.Game.Rulesets.Judgements` — `JudgementResult`, `Judgement`
- `osu.Game.Rulesets.Scoring` — `HitResult` enum, `HitWindows`
- `osu.Game.Graphics` — `OsuColour`, `OsuIcon`, `OsuSpriteText`

## Mod System

### Creating a Mod

Mods extend `Mod` (from `osu.Game.Rulesets.Mods`). Key members to override:

```csharp
public override string Name => "My Mod";
public override string Acronym => "MM";
public override LocalisableString Description => @"Does something.";
public override double ScoreMultiplier => 1;
public override IconUsage? Icon => OsuIcon.ModHidden;   // reuse existing icon
public override ModType Type => ModType.Fun;             // DifficultyReduction, DifficultyIncrease, Conversion, Automation, Fun, System
public override Type[] IncompatibleMods => new[] { typeof(SomeOtherMod) };
```

### Applicable Interfaces

Mods implement one or more of these to affect gameplay:

| Interface | When Called | Use For |
|-----------|------------|---------|
| `IApplicableToDrawableHitObject` | Per top-level drawable hit object | Modifying visuals of hit objects |
| `IApplicableToDrawableRuleset<TObject>` | Once for the ruleset | Accessing playfield, overlays |
| `IApplicableToBeatmap` | Once per beatmap | Modifying beatmap data |
| `IApplicableToHitObject` | Per hit object | Modifying hit object properties |
| `IApplicableToDrawableRuleset` | Once | Adding HUD elements, changing playfield behavior |
| `IApplicableToHealthProcessor` | Once | Custom health drain |
| `IApplicableToScoreProcessor` | Once | Custom scoring |

### Mod Registration

Mods are registered in `OsuRuleset.GetModsFor(ModType type)`. Add to the appropriate `ModType` array.

### Reference Mods

- **`OsuModSynesthesia`** — `IApplicableToDrawableHitObject`, sets `AccentColour` every frame via `OnUpdate`
- **`OsuModHidden`** — `ModHidden` subclass, fades circles/sliders, uses `ApplyToBeatmap` + `ApplyIncreasedVisibilityState`/`ApplyNormalVisibilityState`
- **`OsuModClassic`** — `IApplicableToDrawableRuleset`, `IApplicableToDrawableHitObject`, `IApplicableToHitObject` — accesses `DrawableOsuRuleset.Playfield`
- **`OsuModApproachDifferent`** — `IApplicableToDrawableHitObject`, modifies approach circle transforms via `ApplyCustomUpdateState`

## Hit Result System

### HitResult Enum (`osu.Game.Rulesets.Scoring`)

For osu! standard, the mapping is:
| HitResult | Meaning | Color (`OsuColour.ForHitResult`) |
|-----------|---------|----------------------------------|
| `Great` | 300 | Blue (`#66ccff`) |
| `Good` | — | GreenLight (`#b3d944`) |
| `Ok` | 100 | Green (`#88b300`) |
| `Meh` | 50 | Yellow (`#ffcc22`) |
| `Miss` | Miss | Red (`#ed1121`) |

### JudgementResult

Stored on `DrawableHitObject.Result` (via the lifetime entry). Key fields:

- `Type` — `HitResult` enum value
- `TimeOffset` — Positive = late, Negative = early (ms from object end time)
- `TimeAbsolute` — Absolute time of the judgement
- `HitObject` — The judged hit object

### Events on DrawableHitObject

- **`OnNewResult`** — `event Action<DrawableHitObject, JudgementResult>` — Fires when a result is applied. The `DrawableHitObject` parameter is the actual drawable that received the result (may be a nested object). This event bubbles up from nested to parent.
- **`ApplyCustomUpdateState`** — `event Action<DrawableHitObject, ArmedState>` — Fires during state updates (Hit/Miss/Idle). Use for custom hit/miss animations.
- **`HitObjectApplied`** — Fires when a hit object is assigned to this drawable.

### OsuHitWindows (`osu.Game.Rulesets.Osu.Scoring`)

Defines timing windows:
- `Great`: 80–20ms (OD-dependent)
- `Ok`: 140–60ms (OD-dependent)
- `Meh`: 200–100ms (OD-dependent)
- `Miss`: Fixed 400ms

## Visual Architecture

### Drawable Hierarchy

```
DrawableRuleset
├── FrameStabilityContainer
│   ├── FrameStableComponents
│   └── AudioContainer
│       └── KeyBindingInputManager
│           ├── PlayfieldAdjustmentContainer
│           │   └── Playfield (OsuPlayfield)
│           │       └── HitObjectContainer
│           │           ├── DrawableHitCircle
│           │           ├── DrawableSlider
│           │           │   ├── DrawableSliderHead (nested)
│           │           │   ├── DrawableSliderTick (nested)
│           │           │   └── DrawableSliderTail (nested)
│           │           └── DrawableSpinner
│           └── Overlays  ← Public Container, good for adding custom overlays
```

### Circle Coloring

`DrawableHitObject` has `Bindable<Color4> AccentColour`. This is set by `UpdateComboColour()` based on combo colors from the skin. In `MainCirclePiece` (the visual for hit circles), the accent colour is bound to `circle.Colour`, `glow.Colour`, and `explode.Colour`.

To override circle colors in a mod, implement `IApplicableToDrawableHitObject` and set `d.AccentColour.Value` (either once or per-frame via `OnUpdate`).

### Important Types

- **`DrawableHitObject`** — Base class, extends `PoolableDrawableWithLifetime<HitObjectLifetimeEntry>` (which extends `CompositeDrawable`). Does NOT have a public `Add` method — use `AddInternal` only from subclasses.
- **`DrawableHitCircle`** — Circle rendering. Has `CirclePiece` (main circle), `ApproachCircle`, `HitArea`.
- **`MainCirclePiece`** — Composed of `CirclePiece`, `RingPiece`, `FlashPiece`, `ExplodePiece`, `NumberPiece`, `GlowPiece`. Binds to `AccentColour`.
- **`OsuSpriteText`** — Use this instead of `SpriteText` (project convention).
- **`OsuColour`** — Color constants and `ForHitResult()`, `ForRank()` helpers. Can be instantiated directly.
- **`OsuIcon`** — Icon constants for mods (e.g., `OsuIcon.ModHidden`).

### Coordinate Systems

- **Gamefield coordinates** — osu! playfield (512×384 base). `OsuHitObject.StackedPosition` is in gamefield space.
- **Screen space** — Actual screen pixels. Convert via `Playfield.GamefieldToScreenSpace()` / `ScreenSpaceToGamefield()`.
- **Overlay space** — `DrawableRuleset.Overlays` is a sibling of `PlayfieldAdjustmentContainer`. Use `overlay.ToLocalSpace(screenPos)` to convert screen positions to overlay positions.

## Container Gotchas

- `DrawableHitObject` is a `CompositeDrawable`, NOT a `Container<T>`. It has no public `Add()` method. To add children from outside, you must either:
  - Put an overlay `Container` on `DrawableRuleset.Overlays` (implement `IApplicableToDrawableRuleset` to access)
  - Or derive from `DrawableHitObject` and use the protected `AddInternal`
- `HitObjectContainer` is also NOT a `Container<T>` — it's a `PooledDrawableWithLifetimeContainer`.

## Finding Your Way Around

- Mod examples: `osu.Game.Rulesets.Osu/Mods/OsuModSynesthesia.cs` (color override), `OsuModHidden.cs` (fading), `OsuModClassic.cs` (multi-interface)
- Judgement/result: `osu.Game/Rulesets/Judgements/JudgementResult.cs`, `osu.Game/Rulesets/Scoring/HitResult.cs`
- Hit windows: `osu.Game.Rulesets.Osu/Scoring/OsuHitWindows.cs`
- Playfield: `osu.Game.Rulesets.Osu/UI/OsuPlayfield.cs`
- Circle rendering: `osu.Game.Rulesets.Osu/Skinning/Default/MainCirclePiece.cs`
- Ruleset: `osu.Game.Rulesets.Osu/OsuRuleset.cs`
- Drawable ruleset: `osu.Game.Rulesets.Osu/UI/DrawableOsuRuleset.cs`
