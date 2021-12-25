﻿using System.Collections.Generic;
using MapsetParser.objects;
using MapsetParser.objects.hitobjects;
using MapsetParser.statics;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.attributes;
using MapsetVerifierFramework.objects.metadata;

namespace MapsetChecks.Checks.Standard.Spread
{
    [Check]
    public class CheckCloseOverlap : BeatmapSetCheck
    {
        public override CheckMetadata GetMetadata() => new BeatmapCheckMetadata
        {
            Modes = new[]
            {
                Beatmap.Mode.Standard
            },
            Category = "Spread",
            Message = "Objects close in time not overlapping.",
            Author = "Naxess",

            Documentation = new Dictionary<string, string>
            {
                {
                    "Purpose",
                    @"
                    Ensuring that objects close in time are indiciated as such in easy and lowest normal difficulties.
                    <image>
                        https://i.imgur.com/rnIi6Pj.png
                        Right image is harder to distinguish time distance in, despite spacings still clearly being different.
                    </image>"
                },
                {
                    "Reasoning",
                    @"
                    Newer players often have trouble reading how far apart objects are in time, which is why enabling 
                    distance spacing for lower difficulties is often recommended. However, if two spacings for different 
                    snappings look similar, it's possible to confuse them. By forcing an overlap between objects close in 
                    time and discouraging it for objects further apart, the difference in snappings become more apparent."
                }
            }
        };
        
        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>
            {
                { "Problem",
                    new IssueTemplate(Issue.Level.Problem,
                        "{0} {1} ms apart, should either be overlapped or at least {2} ms apart.",
                        "timestamp - ", "gap", "threshold")
                    .WithCause(
                        "Two objects with a time gap less than 125 ms (240 bpm 1/2) are not overlapping.") },

                { "Warning",
                    new IssueTemplate(Issue.Level.Warning,
                        "{0} {1} ms apart.",
                        "timestamp - ", "gap")
                    .WithCause(
                        "Two objects with a time gap less than 167 ms (180 bpm 1/2) are not overlapping.") }
            };
        }

        private const double ProblemThreshold = 125; // Shortest acceptable gap is 1/2 in 240 BPM, 125 ms.
        private const double WarningThreshold = 188; // Shortest gap before warning is 1/2 in 160 BPM, 188 ms.

        public override IEnumerable<Issue> GetIssues(BeatmapSet beatmapSet)
        {
            Beatmap.Difficulty skipAfterDifficulty = Beatmap.Difficulty.Normal;
            foreach (Beatmap beatmap in beatmapSet.beatmaps)
            {
                // Beatmaps are sorted by interpreted difficulty.
                if (beatmap.GetDifficulty(considerName: true) > skipAfterDifficulty)
                    break;

                // This check only applies to easy/lowest diff normals, so if we find an easy, normals cannot not be lowest diff.
                if (beatmap.GetDifficulty(considerName: true) == Beatmap.Difficulty.Easy)
                    skipAfterDifficulty = Beatmap.Difficulty.Easy;

                foreach (HitObject hitObject in beatmap.hitObjects)
                {
                    if (!(hitObject.Next() is HitObject nextObject))
                        continue;

                    // Slider ends do not need to overlap, same with spinners, spinners should be ignored overall.
                    if (!(hitObject is Circle) || nextObject is Spinner)
                        continue;

                    if (nextObject.time - hitObject.time >= WarningThreshold)
                        continue;

                    double distance = (nextObject.Position - hitObject.Position).Length();

                    // If the distance is larger or equal to the diameter of a circle, then they're not overlapping.
                    float radius = beatmap.difficultySettings.GetCircleRadius();
                    if (distance < radius * 2)
                        continue;

                    if (nextObject.time - hitObject.time < ProblemThreshold)
                        yield return new Issue(GetTemplate("Problem"), beatmap,
                                Timestamp.Get(hitObject, nextObject),
                                $"{nextObject.time - hitObject.time:0.##}",
                                ProblemThreshold)
                            .ForDifficulties(Beatmap.Difficulty.Easy, Beatmap.Difficulty.Normal);

                    else
                        yield return new Issue(GetTemplate("Warning"), beatmap,
                                Timestamp.Get(hitObject, nextObject),
                                $"{nextObject.time - hitObject.time:0.##}")
                            .ForDifficulties(Beatmap.Difficulty.Easy, Beatmap.Difficulty.Normal);
                }
            }
        }
    }
}
