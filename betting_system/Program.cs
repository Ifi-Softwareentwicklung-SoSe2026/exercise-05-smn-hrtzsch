using System.Text.Json;
using System.Text.Json.Serialization;

const string DataDirectory = "tournament_data";
const string DataFileName = "turnier.json";

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "print";
var dataPath = Path.Combine(DataDirectory, DataFileName);

switch (command)
{
    case "new":
        Directory.CreateDirectory(DataDirectory);
        var tournament = Tournament.CreateWorldCupGroupStage();
        TournamentStore.Save(dataPath, tournament);
        Console.WriteLine($"Turniertabelle mit {tournament.Groups.Sum(group => group.Matches.Count)} Spielen gespeichert: {dataPath}");
        break;

    case "print":
        var loaded = TournamentStore.LoadOrCreate(dataPath);
        PrintTournament(loaded);
        break;

    case "set":
        if (args.Length != 4 || !decimal.TryParse(args[3], out var quote))
        {
            Console.WriteLine("Verwendung: set <spielid> <Wetttyp> <Wettquote>");
            Environment.ExitCode = 1;
            break;
        }
        var tournamentWithQuote = TournamentStore.LoadOrCreate(dataPath);
        var matchForQuote = tournamentWithQuote.FindMatch(args[1]);
        if (matchForQuote is null)
        {
            Console.WriteLine($"Spiel-ID nicht gefunden: {args[1]}");
            Environment.ExitCode = 1;
            break;
        }
        matchForQuote.Odds[args[2]] = quote;
        TournamentStore.Save(dataPath, tournamentWithQuote);
        Console.WriteLine($"Quote gespeichert: {matchForQuote.MatchId} {args[2]} = {quote}");
        break;

    case "get":
        if (args.Length != 3)
        {
            Console.WriteLine("Verwendung: get <spielid> <Wetttyp>");
            Environment.ExitCode = 1;
            break;
        }
        var tournamentForLookup = TournamentStore.LoadOrCreate(dataPath);
        var matchForLookup = tournamentForLookup.FindMatch(args[1]);
        if (matchForLookup is null || !matchForLookup.Odds.TryGetValue(args[2], out var storedQuote))
        {
            Console.WriteLine($"Keine Quote gefunden: {args[1]} {args[2]}");
            Environment.ExitCode = 1;
            break;
        }
        Console.WriteLine($"{matchForLookup.MatchId} {args[2]} = {storedQuote}");
        break;

    default:
        Console.WriteLine("Unbekannter Befehl. Verfügbar: new, print, set, get");
        Environment.ExitCode = 1;
        break;
}

static void PrintTournament(Tournament tournament)
{
    Console.WriteLine(tournament.Name);
    foreach (var group in tournament.Groups)
    {
        Console.WriteLine($"Gruppe {group.Name}");
        foreach (var match in group.Matches)
        {
            Console.WriteLine($"{match.MatchId}: {match.HomeTeam.Name} vs {match.AwayTeam.Name} am {match.Kickoff:yyyy-MM-dd HH:mm} Ergebnis: {match.Result ?? "offen"}");
        }
    }
}

public sealed record Team(string Name);

public sealed record Match(
    string MatchId,
    Team HomeTeam,
    Team AwayTeam,
    DateTime Kickoff,
    string? Result = null,
    Dictionary<string, decimal>? Odds = null)
{
    public Dictionary<string, decimal> Odds { get; init; } = Odds ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
}

public sealed record Group(string Name, List<Team> Teams, List<Match> Matches);

public sealed record Tournament(string Name, List<Group> Groups)
{
    public Match? FindMatch(string matchId) => Groups
        .SelectMany(group => group.Matches)
        .FirstOrDefault(match => string.Equals(match.MatchId, matchId, StringComparison.OrdinalIgnoreCase));

    public static Tournament CreateWorldCupGroupStage()
    {
        var groupA = CreateGroup("A", "Deutschland", "Schottland", "Ungarn", "Schweiz", new DateTime(2026, 6, 11, 20, 0, 0));
        var groupB = CreateGroup("B", "Spanien", "Kroatien", "Italien", "Albanien", new DateTime(2026, 6, 12, 18, 0, 0));
        return new Tournament("Fussball-WM Gruppenphase", [groupA, groupB]);
    }

    private static Group CreateGroup(string name, string team1, string team2, string team3, string team4, DateTime firstKickoff)
    {
        var teams = new List<Team> { new(team1), new(team2), new(team3), new(team4) };
        var pairings = new[] { (0, 1), (2, 3), (0, 2), (1, 3), (0, 3), (1, 2) };
        var matches = pairings
            .Select((pairing, index) => new Match(
                $"{name}{index + 1}",
                teams[pairing.Item1],
                teams[pairing.Item2],
                firstKickoff.AddDays(index)))
            .ToList();
        return new Group(name, teams, matches);
    }
}

public static class TournamentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Save(string path, Tournament tournament)
    {
        var json = JsonSerializer.Serialize(tournament, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static Tournament LoadOrCreate(string path)
    {
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var tournament = Tournament.CreateWorldCupGroupStage();
            Save(path, tournament);
            return tournament;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Tournament>(json, JsonOptions) ?? Tournament.CreateWorldCupGroupStage();
    }
}
