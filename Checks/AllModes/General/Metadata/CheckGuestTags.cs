﻿using MapsetParser.objects;
using MapsetVerifierFramework;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.attributes;
using MapsetVerifierFramework.objects.metadata;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MapsetChecks.Checks.AllModes.General.Metadata
{
    [Check]
    public class CheckGuestTags : GeneralCheck
    {
        public override CheckMetadata GetMetadata() => new CheckMetadata
        {
            Category = "Metadata",
            Message = "Missing GDers in tags.",
            Author = "Naxess",

            Documentation = new Dictionary<string, string>
            {
                {
                    "Purpose",
                    @"
                    Ensuring that the set can be found by searching for any of its guest difficulty creators."
                },
                {
                    "Reasoning",
                    @"
                    If you're looking for beatmaps of a specific user, it'd make sense if sets containing their
                    guest difficulties would appear, and not only sets they hosted."
                }
            }
        };

        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>
            {
                { "Warning",
                    new IssueTemplate(Issue.Level.Warning,
                        "\"{0}\" is possessive but \"{1}\" isn't in the tags, ignore if not a user.",
                        "guest's diff", "guest")
                    .WithCause(
                        "A difficulty name is prefixed by text containing an apostrophe (') before or after " +
                        "the character \"s\", which is not in the tags.") }
            };
        }

        private readonly char[] collabChars = new char[] { ',', '&', '|', '/' };
        private readonly Regex possessorRegex = new Regex(@"(.+)(?:'s|(s)')", RegexOptions.IgnoreCase);

        public override IEnumerable<Issue> GetIssues(BeatmapSet beatmapSet)
        {
            foreach (Beatmap beatmap in beatmapSet.beatmaps)
            {
                foreach (string possessor in GetAllPossessors(beatmap.metadataSettings.version))
                {
                    if (beatmap.metadataSettings.IsCoveredByTags(possessor) ||
                        beatmap.metadataSettings.creator.ToLower() == possessor.ToLower())
                        continue;

                    yield return new Issue(GetTemplate("Warning"), null,
                        beatmap.metadataSettings.version, possessor.ToLower().Replace(" ", "_"));
                }
            }
        }

        /// <summary> Returns any text before the last apostrophe with a neighbouring "s".
        /// E.g. "Naxess' & Greaper" when inputting "Naxess' & Greaper's Nor'mal". </summary>
        private string GetPossessor(string text)
        {
            Match match = possessorRegex.Match(text);
            if (match == null)
                return null;

            // e.g. "Alphabet" in "Alphabet's Normal"
            string possessor = match.Groups[1].Value;
            if (match.Groups.Count > 2)
                // If e.g. "Naxess' Insane", group 1 is "Naxes" and group 2 is the remaining "s".
                possessor += match.Groups[2].Value;

            return possessor.Trim();
        }

        /// <summary> Returns all possessors in the given text. E.g. "Naxess', Greaper's &
        /// Someone else's Normal" returns "Naxess", "Greaper", and "Someone else". </summary>
        private IEnumerable<string> GetAllPossessors(string text)
        {
            foreach (string possessor in text.Split(collabChars))
                yield return GetPossessor(possessor);
        }
    }
}
