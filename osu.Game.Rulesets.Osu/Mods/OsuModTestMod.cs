// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModTestMod : Mod, IApplicableToDrawableHitObject, IApplicableToDrawableRuleset<OsuHitObject>
    {
        public override string Name => "Test Mod";
        public override string Acronym => "TM";
        public override LocalisableString Description => @"Shows Late/Early on 100s and 50s.";
        public override double ScoreMultiplier => 1;
        public override IconUsage? Icon => OsuIcon.ModHidden;
        public override ModType Type => ModType.Fun;

        private OsuPlayfield playfield = null!;
        private Container overlay = null!;
        private readonly OsuColour colours = new();

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            playfield = ((DrawableOsuRuleset)drawableRuleset).Playfield;
            overlay = new Container { RelativeSizeAxes = Axes.Both };
            drawableRuleset.Overlays.Add(overlay);
        }

        public void ApplyToDrawableHitObject(DrawableHitObject d)
        {
            d.OnNewResult += (drawable, result) =>
            {
                if (result.Type is not (HitResult.Ok or HitResult.Meh))
                    return;

                bool late = result.TimeOffset > 0;

                var text = new OsuSpriteText
                {
                    Text = late ? "Late" : "Early",
                    Colour = colours.ForHitResult(result.Type),
                    Origin = Anchor.Centre,
                };

                Vector2 gamefieldPos = ((OsuHitObject)drawable.HitObject).StackedPosition;
                Vector2 screenPos = playfield.GamefieldToScreenSpace(gamefieldPos);
                text.Position = overlay.ToLocalSpace(screenPos) + new Vector2(0, -40);

                overlay.Add(text);
                text.FadeOut(1500).Expire();
            };
        }
    }
}
