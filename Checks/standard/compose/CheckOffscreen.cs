﻿using MapsetParser.objects;
using MapsetParser.objects.hitobjects;
using MapsetParser.statics;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.attributes;
using MapsetVerifierFramework.objects.metadata;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MapsetChecks.Checks.Standard.Compose
{
    [Check]
    public class CheckOffscreen : BeatmapCheck
    {
        public override CheckMetadata GetMetadata() => new BeatmapCheckMetadata()
        {
            Modes = new Beatmap.Mode[]
            {
                Beatmap.Mode.Standard
            },
            Category = "Compose",
            Message = "Offscreen hit objects.",
            Author = "Naxess",

            Documentation = new Dictionary<string, string>
            {
                {
                    "Purpose",
                    @"
                    Preventing the border of hit objects from even partially becoming offscreen in 4:3 aspect ratios.
                    <note>
                        4:3 is included in 16:9 and 16:10, the only difference is the width, so you can check for 
                        offscreens along the top and bottom in any of these aspect ratios and it will look the same.
                    </note>
                    <image-right>
                        https://i.imgur.com/zXT4Zwr.png
                        A slider end which is partially offscreen along the bottom of the screen.
                    </image>"
                },
                {
                    "Reasoning",
                    @"
                    Although everything is technically readable and playable if an object is only partially offscreen, 
                    it trips up players using relative movement input (for example mouse) when their cursor hits the 
                    side of the screen, since the game will offset the cursor back into the screen which is difficult 
                    to correct while in the middle of gameplay.
                    <br \><br \>
                    Since objects partially offscreen also have a smaller area to hit, if not hitting the screen 
                    causing the problems above, it makes those objects need more precision to play which isn't 
                    consistent with how the rest of the game works, especially considering that the punishment for 
                    overshooting is getting your cursor offset slightly but still hitting the object and not missing 
                    like you probably would otherwise."
                }
            }
        };
        
        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>
            {
                { "Offscreen",
                    new IssueTemplate(Issue.Level.Problem,
                        "{0} {1} is offscreen.",
                        "timestamp - ", "object")
                    .WithCause(
                        "The border of a hit object is partially off the screen in 4:3 aspect ratios.") },

                { "Prevented",
                    new IssueTemplate(Issue.Level.Warning,
                        "{0} {1} would be offscreen, but the game prevents it.",
                        "timestamp - ", "object")
                    .WithCause(
                        "The .osu code implies the hit object is in a place where it would be off the 512x512 playfield area, but the game has " +
                        "moved it back inside the screen automatically.") },

                { "Bezier Margin",
                    new IssueTemplate(Issue.Level.Warning,
                        "{0} Slider body is possibly offscreen, ensure the entire white border is visible on a 4:3 aspect ratio.",
                        "timestamp - ")
                    .WithCause(
                        "The slider body of a bezier slider is approximated to be 1 osu!pixel away from being offscreen at some point on its curve.") }
            };
        }

        // Old measurements: -60, 430, -66, 578
        // New measurements: -60, 428, -67, 579 (tested with slider tails)
        private const int UPPER_LIMIT = -60;
        private const int LOWER_LIMIT = 428;
        private const int LEFT_LIMIT  = -67;
        private const int RIGHT_LIMIT = 579;

        public override IEnumerable<Issue> GetIssues(Beatmap beatmap)
        {
            foreach (HitObject hitObject in beatmap.hitObjects)
            {
                string type = hitObject is Circle ? "Circle" : "Slider head";
                if (!(hitObject is Circle) && !(hitObject is Slider))
                    continue;

                float circleRadius = beatmap.difficultySettings.GetCircleRadius();
                Vector2 stackedOffset = new Vector2(0, 0);
                if (hitObject is Stackable stackable)
                    stackedOffset = stackable.Position - stackable.UnstackedPosition;

                if (hitObject.Position.Y + circleRadius > LOWER_LIMIT)
                    yield return new Issue(GetTemplate("Offscreen"), beatmap,
                        Timestamp.Get(hitObject), type);

                // The game prevents the head of objects from going offscreen inside a 512 by 512 px square,
                // meaning heads can still go offscreen at the bottom due to how aspect ratios work.
                else if (GetOffscreenBy(hitObject.Position, beatmap) > 0)
                {
                    // It does not prevent stacked objects from going offscreen, though.

                    // for each stackindex it goes 3px up and left, so for it to be prevented it'd be
                    // top, left : stackindex <= 0
                    // right     : stackindex >= 0

                    Stackable stackableObject = hitObject as Stackable;

                    bool goesOffscreenTopOrLeft =
                        (stackableObject.Position.Y - circleRadius < UPPER_LIMIT ||
                        stackableObject.Position.X - circleRadius < LEFT_LIMIT) &&
                        stackableObject.stackIndex > 0;

                    bool goesOffscreenRight =
                        stackableObject.Position.X + circleRadius > RIGHT_LIMIT &&
                        stackableObject.stackIndex < 0;

                    if (goesOffscreenTopOrLeft || goesOffscreenRight)
                        yield return new Issue(GetTemplate("Offscreen"), beatmap,
                            Timestamp.Get(hitObject), type);
                    else
                        yield return new Issue(GetTemplate("Prevented"), beatmap,
                            Timestamp.Get(hitObject), type);
                }

                if (!(hitObject is Slider slider))
                    continue;

                if (GetOffscreenBy(slider.EndPosition, beatmap) > 0)
                    yield return new Issue(GetTemplate("Offscreen"), beatmap,
                        Timestamp.Get(hitObject.GetEndTime()), "Slider tail");
                else
                {
                    bool offscreenBodyFound = false;
                    foreach(Vector2 pathPosition in slider.pathPxPositions)
                    {
                        if (GetOffscreenBy(pathPosition + stackedOffset, beatmap) <= 0)
                            continue;

                        yield return new Issue(GetTemplate("Offscreen"), beatmap,
                            Timestamp.Get(hitObject), "Slider body");

                        offscreenBodyFound = true;
                        break;
                    }

                    // Since we sample parts of slider bodies, and these aren't math formulas (although they could be),
                    // we'd need to sample an infinite amount of points on the path, which is too intensive, so instead
                    // we approximate and apply leniency to ensure false-positive over false-negative.
                    if (offscreenBodyFound)
                        continue;

                    foreach (Vector2 pathPosition in slider.pathPxPositions)
                    {
                        Vector2 exactPathPosition = pathPosition + stackedOffset;
                        if (GetOffscreenBy(exactPathPosition, beatmap, 2) <= 0 || slider.curveType == Slider.CurveType.Linear)
                            continue;

                        bool isOffscreen = false;
                        for (int j = 0; j < slider.GetCurveDuration() * 50; ++j)
                        {
                            exactPathPosition = slider.GetPathPosition(slider.time + j / 50d);

                            double offscreenBy = GetOffscreenBy(exactPathPosition, beatmap);
                            if (offscreenBy > 0)
                                isOffscreen = true;
                        }

                        if (isOffscreen)
                            yield return new Issue(GetTemplate("Offscreen"), beatmap,
                                Timestamp.Get(hitObject), "Slider body");
                        else
                            yield return new Issue(GetTemplate("Bezier Margin"), beatmap,
                                Timestamp.Get(hitObject));

                        break;
                    }
                }
            }
        }

        /// <summary> Returns how far offscreen an object is in pixels (in-game pixels, not resolution). </summary>
        private float GetOffscreenBy(Vector2 point, Beatmap beatmap, float leniency = 0)
        {
            float circleRadius = beatmap.difficultySettings.GetCircleRadius();

            float offscreenBy = 0;

            float offscreenRight = point.X + circleRadius - RIGHT_LIMIT + leniency;
            float offscreenLeft  = circleRadius - point.X + LEFT_LIMIT  + leniency;
            float offscreenLower = point.Y + circleRadius - LOWER_LIMIT + leniency;
            float offscreenUpper = circleRadius - point.Y + UPPER_LIMIT + leniency;

            if (offscreenRight > offscreenBy) offscreenBy = offscreenRight;
            if (offscreenLeft  > offscreenBy) offscreenBy = offscreenLeft;
            if (offscreenLower > offscreenBy) offscreenBy = offscreenLower;
            if (offscreenUpper > offscreenBy) offscreenBy = offscreenUpper;

            return (float)Math.Ceiling(offscreenBy * 100) / 100f;
        }
    }
}
