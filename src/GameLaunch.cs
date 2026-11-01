using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

public static class GameLaunch {
    public static List<string> Executables(LibraryGame game) {
        return game.Rules.Select(r => r.Exe).Where(p => File.Exists(p) && !Catalog.Helper(p))
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Build a launch request separately, so validation never has to start a real game.
    public static ProcessStartInfo Request(LibraryGame game, string executable = null) {
        var exes = Executables(game);
        if (exes.Count == 0) throw new Exception("O executável deste jogo não foi encontrado. Atualize a biblioteca ou confira a instalação.");
        if (game.Launcher == Launchers.Steam) {
            uint id;
            if (game.Key == null || !game.Key.StartsWith("steam:", StringComparison.OrdinalIgnoreCase) || !UInt32.TryParse(game.Key.Substring(6), out id) || id == 0)
                throw new Exception("O identificador Steam deste jogo é inválido. Atualize a biblioteca.");
            return new ProcessStartInfo("steam://rungameid/" + id) { UseShellExecute = true };
        }
        if (executable == null) {
            if (exes.Count != 1) throw new Exception("Escolha o executável no submenu Jogar.");
            executable = exes[0];
        }
        executable = Path.GetFullPath(executable);
        if (!exes.Contains(executable, StringComparer.OrdinalIgnoreCase)) throw new Exception("Este executável não pertence ao jogo selecionado.");
        return new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = true };
    }

    public static void Start(LibraryGame game, string executable = null) { Process.Start(Request(game, executable)); }
}
