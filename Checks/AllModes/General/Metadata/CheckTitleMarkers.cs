﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MapsetParser.objects;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.attributes;
using MapsetVerifierFramework.objects.metadata;

namespace MapsetChecks.Checks.AllModes.General.Metadata
{
    [Check]
    public class CheckTitleMarkers : GeneralCheck
    {
        public override CheckMetadata GetMetadata() => new CheckMetadata
        {
            Category = "Metadata",
            Message = "Incorrect format of (TV Size) / (Game Ver.) / (Short Ver.) / (Cut Ver.) / (Sped Up Ver.) / etc in title.",
            Author = "Naxess",

            Documentation = new Dictionary<string, string>
            {
                {
                    "Purpose",
                    @"
                    Standardizing the way metadata is written for ranked content.
                    <image>
                        https://i.imgur.com/1ozV71n.png
                        A song using ""-TV version-"" as its official metadata, which becomes ""(TV Size)"" when standardized.
                    </image>"
                },
                {
                    "Reasoning",
                    @"
                    Small deviations in metadata or obvious mistakes in its formatting or capitalization are for the 
                    most part eliminated through standardization. Standardization also reduces confusion in case of 
                    multiple correct ways to write certain fields and contributes to making metadata more consistent 
                    across official content."
                }
            }
        };

        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>
            {
                { "Problem",
                    new IssueTemplate(Issue.Level.Problem,
                        "{0} title field; \"{1}\" incorrect format of \"{2}\".",
                        "Romanized/unicode", "field", "title marker")
                    .WithCause(
                        @"The format of a title marker, in either the romanized or unicode title, is incorrect.
                        The following are detected formats:
                        <ul>
                            <li>(TV Size)</li>
                            <li>(Game Ver.)</li>
                            <li>(Short Ver.)</li>
                            <li>(Cut Ver.)</li>
                            <li>(Sped Up Ver.)</li>
                            <li>(Nightcore Mix)</li>
                            <li>(Sped Up & Cut Ver.)</li>
                            <li>(Nightcore & Cut Ver.)</li>
                        </ul>
                        ") },

                { "Warning Nightcore",
                    new IssueTemplate(Issue.Level.Warning,
                        "\"{0}\" in tags, consider \"{1}\" instead of \"{2}\" in {3} title.",
                        "nightcore", "(Nightcore Mix)", "(Sped Up Ver.)", "romanized/unicode")
                    .WithCause(
                        "The romanized/unicode title contains \"(Sped Up Ver.)\" or equivalent, " +
                        "when the tags contain \"nightcore\".") }
            };
        }

        public override IEnumerable<Issue> GetIssues(BeatmapSet beatmapSet)
        {
            Beatmap beatmap = beatmapSet.beatmaps[0];

            foreach (Issue issue in GetMarkerFormatIssues(beatmap))
                yield return issue;

            foreach (Issue issue in GetNightcoreIssues(beatmap))
                yield return issue;
        }

        private class Marker
        {
            private Marker(string value) { Value = value; }
            public string Value { get; }

            public static Marker TV_SIZE => new Marker("(TV Size)");
            public static Marker GAME_VER => new Marker("(Game Ver.)");
            public static Marker SHORT_VER => new Marker("(Short Ver.)");
            public static Marker CUT_VER => new Marker("(Cut Ver.)");
            public static Marker SPED_UP_VER => new Marker("(Sped Up Ver.)");
            public static Marker NIGHTCORE_MIX => new Marker("(Nightcore Mix)");
            public static Marker SPED_UP_CUT_VER => new Marker("(Sped Up & Cut Ver.)");
            public static Marker NIGHTCORE_CUT_VER => new Marker("(Nightcore & Cut Ver.)");
        }

        private readonly struct MarkerFormat
        {
            public readonly Marker marker;
            public readonly Regex incorrectFormatRegex;

            public MarkerFormat(Marker marker, Regex incorrectFormatRegex)
            {
                this.marker = marker;
                this.incorrectFormatRegex = incorrectFormatRegex;
            }
        }

        private static readonly List<MarkerFormat> MarkerFormats = new List<MarkerFormat>
        {
            new MarkerFormat(Marker.TV_SIZE,           new Regex(@"(?i)(tv (size|ver))")),
            new MarkerFormat(Marker.GAME_VER,          new Regex(@"(?i)(game (size|ver))")),
            new MarkerFormat(Marker.SHORT_VER,         new Regex(@"(?i)(short (size|ver))")),
            new MarkerFormat(Marker.CUT_VER,           new Regex(@"(?i)(?<!& )(cut (size|ver))")),
            new MarkerFormat(Marker.SPED_UP_VER,       new Regex(@"(?i)(?<!& )(sped|speed) ?up ver")),
            new MarkerFormat(Marker.NIGHTCORE_MIX,     new Regex(@"(?i)(?<!& )(nightcore|night core) (ver|mix)")),
            new MarkerFormat(Marker.SPED_UP_CUT_VER,   new Regex(@"(?i)(sped|speed) ?up (ver)? ?& cut (size|ver)")),
            new MarkerFormat(Marker.NIGHTCORE_CUT_VER, new Regex(@"(?i)(nightcore|night core) (ver|mix)? ?& cut (size|ver)"))
        };

        private IEnumerable<Issue> GetMarkerFormatIssues(Beatmap beatmap)
        {
            foreach (var markerFormat in MarkerFormats)
            {
                // Matches any string containing some form of the marker but not exactly it.
                foreach (var issue in GetIssuesFromRegex(beatmap, markerFormat))
                    yield return issue;
            }
        }

        private readonly struct TitleType
        {
            public readonly string type;
            public readonly Func<Beatmap, string> Get;

            public TitleType(string type, Func<Beatmap, string> Get)
            {
                this.type = type;
                this.Get = Get;
            }
        }

        private static readonly IEnumerable<TitleType> TitleTypes = new[]
        {
            new TitleType("romanized", beatmap => beatmap.metadataSettings.title),
            new TitleType("unicode",   beatmap => beatmap.metadataSettings.titleUnicode)
        };

        private static string Capitalize(string str) =>
            str.First().ToString().ToUpper() + str[1..];

        /// <summary> Returns issues wherever the romanized or unicode title matches the regex but not the exact format. </summary>
        private IEnumerable<Issue> GetIssuesFromRegex(Beatmap beatmap, MarkerFormat markerFormat)
        {
            foreach (var titleType in TitleTypes)
            {
                string title = titleType.Get(beatmap);
                string correctFormat = markerFormat.marker.Value;
                Regex approxRegex = markerFormat.incorrectFormatRegex;
                Regex exactRegex  = new Regex(Regex.Escape(correctFormat));

                // Unicode fields do not exist in file version 9, hence null check.
                if (title != null && approxRegex.IsMatch(title) && !exactRegex.IsMatch(title))
                    yield return new Issue(GetTemplate("Problem"), null,
                        Capitalize(titleType.type), title, correctFormat);
            }
        }

        private readonly struct SubstitutionPair
        {
            public readonly Marker original;
            public readonly Marker substitution;

            public SubstitutionPair(Marker original, Marker substitution)
            {
                this.original = original;
                this.substitution = substitution;
            }
        }

        private IEnumerable<Issue> GetNightcoreIssues(Beatmap beatmap)
        {
            string nightcoreTag = beatmap.metadataSettings.GetCoveringTag("nightcore");
            if (nightcoreTag == null)
                yield break;

            var substitutionPairs = new List<SubstitutionPair>
            {
                new SubstitutionPair(Marker.SPED_UP_VER,     Marker.NIGHTCORE_MIX),
                new SubstitutionPair(Marker.SPED_UP_CUT_VER, Marker.NIGHTCORE_CUT_VER)
            };

            foreach (var pair in substitutionPairs)
                foreach (var titleType in TitleTypes)
                    if (titleType.Get(beatmap).Contains(pair.original.Value))
                        yield return new Issue(GetTemplate("Warning Nightcore"), null,
                            nightcoreTag, pair.original.Value, pair.substitution.Value, titleType.type);
        }
    }
}
