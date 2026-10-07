namespace HexServer.Contracts;

public static class ServiceIds
{
    public const int Tournaments = 242;
    public const int Monitor = 243;
    public const int Profile = 245;
    public const int GameSession = 246;
    public const int Matchmaking = 247;
    public const int Ai = 248;
    public const int Escrow = 249;
    public const int Gm = 251;
    public const int Mail = 252;
    public const int Campaign = 253;
    public const int LoadBalancer = 254;

    public static bool TryFromTarget(string target, out int serviceId)
    {
        serviceId = target switch
        {
            "ServiceTournaments" => Tournaments,
            "ServiceMonitor" => Monitor,
            "ServiceProfile" => Profile,
            "ServiceGameSession" => GameSession,
            "ServiceMatchmaking" => Matchmaking,
            "ServiceAI" => Ai,
            "ServiceEscrow" => Escrow,
            "ServiceGM" => Gm,
            "ServiceMail" => Mail,
            "ServiceCampaign" => Campaign,
            "ServiceLoadBalancer" => LoadBalancer,
            _ => 0
        };

        return serviceId != 0;
    }

    public static string ToTarget(int serviceId) => serviceId switch
    {
        Tournaments => "ServiceTournaments",
        Monitor => "ServiceMonitor",
        Profile => "ServiceProfile",
        GameSession => "ServiceGameSession",
        Matchmaking => "ServiceMatchmaking",
        Ai => "ServiceAI",
        Escrow => "ServiceEscrow",
        Gm => "ServiceGM",
        Mail => "ServiceMail",
        Campaign => "ServiceCampaign",
        LoadBalancer => "ServiceLoadBalancer",
        _ => throw new KeyNotFoundException($"Unknown HEX service id: {serviceId}")
    };
}

public static class LoadBalancerDataTypes
{
    public const int StartSession = 22011;
    public const int StartEncounter = 22013;
    public const int FindSession = 22015;
    public const int JoinSession = 22017;
    public const int ReadyForGameSetup = 22019;
    public const int ReadyForGameEvents = 22021;
    public const int JoinDisconnectedGame = 22023;
    public const int ReadyToContinueGame = 22025;
    public const int LeaveSession = 22027;
    public const int EndSession = 22029;
    public const int GetSessionList = 22031;
}

public static class GameSessionDataTypes
{
    public const int PlayerTransaction = 3029;
    public const int PlayerAdded = 3050;
    public const int PlayerRemoved = 3051;
    public const int GameContinue = 3052;
    public const int GameStarted = 3053;
    public const int GameEnded = 3054;
    public const int SessionSyncEvent = 3055;
    public const int ChampionStatsUpdated = 3056;
}
