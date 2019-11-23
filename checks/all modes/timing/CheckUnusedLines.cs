﻿using MapsetParser.objects;
using MapsetParser.objects.hitobjects;
using MapsetParser.objects.timinglines;
using MapsetParser.statics;
using MapsetVerifierFramework;
using MapsetVerifierFramework.objects;
using MapsetVerifierFramework.objects.attributes;
using MapsetVerifierFramework.objects.metadata;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MapsetChecks.checks.timing
{
    [Check]
    public class CheckUnusedLines : BeatmapCheck
    {
        public override CheckMetadata GetMetadata() => new BeatmapCheckMetadata()
        {
            Category = "Timing",
            Message = "Unused timing lines.",
            Author = "Naxess",
            
            Documentation = new Dictionary<string, string>()
            {
                {
                    "Purpose",
                    @"
                    Ensuring there are no unused timing lines in the beatmap."
                },
                {
                    "Reasoning",
                    @"
                    When placing uninherited lines on-beat with the previous uninherited line, timing may shift 1 ms forwards 
                    due to rounding errors. This means afer 20 uninherited lines placed in this way, timing may shift up to 
                    20 ms at the end. They may also affect the nightcore mod and main menu pulsing depending on placement.
                    Unused inherited timing points aren't as problematic, but there's no reason to keep them around. The
                    mapper may have intended to use these timing points for something, but forgot to change their settings."
                }
            }
        };
        
        public override Dictionary<string, IssueTemplate> GetTemplates()
        {
            return new Dictionary<string, IssueTemplate>()
            {
                { "Problem Nothing",
                    new IssueTemplate(Issue.Level.Problem,
                        "{0} Uninherited line changes nothing.",
                        "timestamp - ")
                    .WithCause(
                        "An uninherited line is placed on a multiple of 4 downbeats away from the previous uninherited line, " +
                        "and changes no settings.") },

                { "Problem Inherited",
                    new IssueTemplate(Issue.Level.Problem,
                        "{0} Uninherited line changes nothing that can't be changed with an inherited line.",
                        "timestamp - ")
                    .WithCause(
                        "Same as the first check, but changes volume, sampleset, or another setting that an inherited line could change instead.") },

                { "Warning Nothing",
                    new IssueTemplate(Issue.Level.Warning,
                        "{0} Uninherited line changes nothing, other than the finish with the nightcore mod. Ensure it makes sense to have a finish here.",
                        "timestamp - ")
                    .WithCause(
                        "Same as the first check, but is not on a multiple of 4 downbeats away from the previous uninherited line.") },

                { "Warning Inherited",
                    new IssueTemplate(Issue.Level.Warning,
                        "{0} Uninherited line changes nothing that can't be changed with an inherited line, other than the finish with the nightcore mod. " +
                        "Ensure it makes sense to have a finish here.",
                        "timestamp - ")
                    .WithCause(
                        "An uninherited line is not placed on a multiple of 4 downbeats away from the previous uninherited line, " +
                        "and only changes settings which an inherited line could do instead.") },

                { "Problem Useless Inherited",
                    new IssueTemplate(Issue.Level.Minor,
                        "{0} Inherited line changes nothing.",
                        "timestamp - ")
                    .WithCause("An inherited line changes no settings.") }
            };
        }

        public override IEnumerable<Issue> GetIssues(Beatmap aBeatmap)
        {
            List<UninheritedLine> lines = aBeatmap.timingLines.OfType<UninheritedLine>().ToList();
            for (int i = 1; i < lines.Count; ++i)
            {
                bool negligibleDownbeatOffset        = GetBeatOffset(lines[i - 1], lines[i], lines[i - 1].meter) <= 1;
                bool negligibleNightcoreCymbalOffset = GetBeatOffset(lines[i - 1], lines[i], 4 * lines[i - 1].meter) <= 1;

                // If the previous line omits the first barline in taiko and is less than a beat apart from the new one,
                // then the new one does change things even if it's just a ms ahead (prevents the barline from being
                // thicker than normal).
                bool canOmitBarLine =
                    aBeatmap.generalSettings.mode == Beatmap.Mode.Taiko ||
                    aBeatmap.generalSettings.mode == Beatmap.Mode.Mania;

                if (canOmitBarLine && lines[i - 1].omitsBarLine)
                    negligibleDownbeatOffset = GetBeatOffset(lines[i - 1], lines[i], lines[i - 1].meter) == 0;

                // Uninherited lines 4 (or whatever the meter is) beats apart (varying up to 1 ms for rounding errors),
                // with the same bpm and meter, have the same downbeat structure. At which point the latter could be
                // replaced by an inherited line and function identically (other than the finish in the nightcore mod).
                if (lines[i - 1].bpm == lines[i].bpm &&
                    lines[i - 1].meter == lines[i].meter &&
                    negligibleDownbeatOffset)
                {
                    // Check the lines in effect both here and before to see if an inherited
                    // line is placed on top of the red line negating its changes.
                    TimingLine prevLine = aBeatmap.GetTimingLine(lines[i].offset - 1);
                    TimingLine curLine = aBeatmap.GetTimingLine<UninheritedLine>(lines[i].offset);

                    // If a line omits the first bar line we just treat it as used.
                    if (canOmitBarLine && curLine.omitsBarLine)
                        continue;

                    if (prevLine.kiai == curLine.kiai &&
                        prevLine.sampleset == curLine.sampleset &&
                        prevLine.volume == curLine.volume)
                    {
                        // In the nightcore mod, every 4th (or whatever the meter is) downbeat
                        // has an added cymbal, so that technically changes things.
                        if (negligibleNightcoreCymbalOffset)
                            yield return new Issue(GetTemplate("Problem Nothing"),
                            aBeatmap, Timestamp.Get(lines[i].offset));
                        else
                            yield return new Issue(GetTemplate("Warning Nothing"),
                                aBeatmap, Timestamp.Get(lines[i].offset));
                    }
                    else
                    {
                        if (negligibleNightcoreCymbalOffset)
                            yield return new Issue(GetTemplate("Problem Inherited"),
                                aBeatmap, Timestamp.Get(lines[i].offset));
                        else
                            yield return new Issue(GetTemplate("Warning Inherited"),
                               aBeatmap, Timestamp.Get(lines[i].offset));
                    }
                }
            }

            List<TimingLine> allLines = aBeatmap.timingLines.ToList();
            for (int i = 1; i < allLines.Count; ++i)
            {
                if (!(allLines[i] is InheritedLine currentLine))
                    continue;

                TimingLine previousLine = allLines[i - 1];

                // Check if all of the timing point's settings are the same, excluding
                // offset and meter (meter doesn't do anything for inherited points).
                //
                // The SV comparison works even when the previous line is an uninherited
                // line, because all uninherited lines' SVs are 1.
                if (currentLine.svMult == previousLine.svMult &&
                    currentLine.sampleset == previousLine.sampleset &&
                    currentLine.customIndex == previousLine.customIndex &&
                    currentLine.volume == previousLine.volume &&
                    currentLine.kiai == previousLine.kiai)
                {
                    yield return new Issue(GetTemplate("Problem Useless Inherited"),
                        aBeatmap, Timestamp.Get(currentLine.offset));
                }
            }
        }

        /// <summary> Returns the ms difference between two timing lines, where the timing lines reset offset every given number of beats. </summary>
        private double GetBeatOffset(UninheritedLine aLine, UninheritedLine aNextLine, double aBeatOffset)
        {
            double beatsIn = (aNextLine.offset - aLine.offset) / aLine.msPerBeat;
            double offset = beatsIn % aBeatOffset;

            return
                Math.Min(
                    Math.Abs(offset),
                    Math.Abs(offset - aBeatOffset)) *
                aLine.msPerBeat;
        }
    }
}
