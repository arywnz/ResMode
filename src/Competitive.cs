using System;
using System.Text.RegularExpressions;

// Competitive games where frame rate matters more than sharpness. They are left at the desktop
// resolution by default (Settings.SkipCompetitive); the user can turn any of them back on.
public static class Competitive {
    static readonly Regex Names = new Regex(
        @"(?i)^(league of legends|valorant|teamfight tactics|counter-strike.*|dota 2|fortnite|apex legends|pubg.*|playerunknown.*|" +
        @"tom clancy's rainbow six.*siege.*|rainbow six siege.*|overwatch.*|rocket league|call of duty.*|.*warzone.*|marvel rivals|deadlock|the finals|" +
        @"halo infinite|team fortress 2|naraka.*|splitgate.*|xdefiant|paladins.*|smite.*|escape from tarkov.*|hunt: showdown.*|" +
        @"osu!|brawlhalla|delta force.*|spectre divide|strinova|predecessor|battlebit.*|krunker.*|the finals.*)$",
        RegexOptions.Compiled);

    public static bool Is(string name) { return Names.IsMatch((name ?? "").Trim()); }
}
