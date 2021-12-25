﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapsetParser.objects;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.attributes;
using MapsetVerifierFramework.objects.metadata;

namespace MapsetChecks.Checks.AllModes.General.Resources
{
    [Check]
    public class CheckBgResolution : GeneralCheck
    {
        public override CheckMetadata GetMetadata() => new CheckMetadata
        {
            Category = "Resources",
            Message = "Too high or low background resolution.",
            Author = "Naxess",

            Documentation = new Dictionary<string, string>
            {
                {
                    "Purpose",
                    @"
                    Preventing background quality from being noticably low or unnoticably high to save on file size.
                    <image-right>
                        https://i.imgur.com/VrKRzse.png
                        The left side is ~2.25x the resolution of the right side, which is the equivalent of comparing 
                        2560 x 1440 to 1024 x 640.
                    </image>"
                },
                {
                    "Reasoning",
                    @"
                    Anything less than 1024 x 640 is usually quite noticeable, whereas anything higher than 2560 x 1440 
                    is unlikely to be visible with the setup of the average player.
                    <note>
                        This uses 16:10 as base, since anything outside of 16:9 will be cut off on that aspect ratio 
                        rather than resized to fit the screen, preserving quality.
                    </note>"
                }
            }
        };

        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>
            {
                { "Too high",
                    new IssueTemplate(Issue.Level.Problem,
                        "\"{0}\" greater than 2560 x 1440 ({1} x {2})",
                        "file name", "width", "height")
                    .WithCause(
                        "A background file has a width exceeding 2560 pixels or a height exceeding 1440 pixels.") },

                { "Very low",
                    new IssueTemplate(Issue.Level.Warning,
                        "\"{0}\" lower than 1024 x 640 ({1} x {2})",
                        "file name", "width", "height")
                    .WithCause(
                        "A background file has a width lower than 1024 pixels or a height lower than 640 pixels.") },

                { "File size",
                    new IssueTemplate(Issue.Level.Problem,
                        "\"{0}\" has a file size exceeding 2.5 MB ({1} MB)",
                        "file name", "file size")
                    .WithCause(
                        "A background file has a file size greater than 2.5 MB.") },

                // parsing results
                { "Leaves Folder",
                    new IssueTemplate(Issue.Level.Problem,
                        "\"{0}\" leaves the current song folder, which shouldn't ever happen.",
                        "file name")
                    .WithCause(
                        "The file path of a background file starts with two dots.") },

                { "Missing",
                    new IssueTemplate(Issue.Level.Problem,
                        "\"{0}\" is missing" + Common.CHECK_MANUALLY_MESSAGE,
                        "file name")
                    .WithCause(
                        "A background file referenced is not present.") },

                { "Exception",
                    new IssueTemplate(Issue.Level.Error,
                        Common.FILE_EXCEPTION_MESSAGE,
                        "file name", "exception info")
                    .WithCause(
                        "An exception occurred trying to parse a background file.") }
            };
        }

        public override IEnumerable<Issue> GetIssues(BeatmapSet beatmapSet)
        {
            foreach (Issue issue in Common.GetTagOsuIssues(
                beatmapSet,
                beatmap => beatmap.backgrounds.Count > 0 ? beatmap.backgrounds.Select(bg => bg.path) : null,
                GetTemplate,
                tagFile =>
                {
                    // Executes for each non-faulty background file used in one of the beatmaps in the set.
                    var issues = new List<Issue>();
                    if (tagFile.file.Properties.PhotoWidth > 2560 ||
                        tagFile.file.Properties.PhotoHeight > 1440)
                    {
                        issues.Add(new Issue(GetTemplate("Too high"), null,
                            tagFile.templateArgs[0],
                            tagFile.file.Properties.PhotoWidth,
                            tagFile.file.Properties.PhotoHeight));
                    }

                    else if (
                        tagFile.file.Properties.PhotoWidth < 1024 ||
                        tagFile.file.Properties.PhotoHeight < 640)
                    {
                        issues.Add(new Issue(GetTemplate("Very low"), null,
                            tagFile.templateArgs[0],
                            tagFile.file.Properties.PhotoWidth,
                            tagFile.file.Properties.PhotoHeight));
                    }

                    // Most operating systems define 1 KB as 1024 B and 1 MB as 1024 KB,
                    // not 10^(3x) which the prefixes usually mean, but 2^(10x), since binary is more efficient for circuits,
                    // so since this is what your computer uses we'll use this too.
                    double megaBytes = new FileInfo(tagFile.file.Name).Length / Math.Pow(1024, 2);
                    if (megaBytes > 2.5)
                    {
                        issues.Add(new Issue(GetTemplate("File size"), null,
                            tagFile.templateArgs[0],
                            FormattableString.Invariant($"{megaBytes:0.##}")));
                    }

                    return issues;
                }))
            {
                // Returns issues from both non-faulty and faulty files.
                yield return issue;
            }
        }
    }
}
