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

public static class GameSessionMethodIds
{
    public const int TryReconnectionToDisconnectedGame = 3003;
    public const int StartSession = 3005;
    public const int StartEncounter = 3007;
    public const int FindReconnectionInformation = 3009;
    public const int FindSession = 3011;
    public const int JoinDisconnectedGame = 3013;
    public const int JoinSession = 3015;
    public const int ReadyForGameSetup = 3019;
    public const int LeaveSession = 3025;
    public const int EndSession = 3027;
    public const int GetSessionList = 3031;
    public const int FindSessionById = 3047;
    public const int SessionResync = 3049;
    public const int PlayerAdded = 3050;
    public const int PlayerRemoved = 3051;
    public const int GameContinue = 3052;
    public const int GameStarted = 3053;
    public const int GameEnded = 3054;
    public const int SessionSyncEvent = 3055;
    public const int ChampionStatsUpdated = 3056;
}
