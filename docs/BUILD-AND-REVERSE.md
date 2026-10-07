# Build and reverse engineering workflow

## Build

    dotnet restore HexServer.Modern.sln
    dotnet build HexServer.Modern.sln -c Release
    dotnet test tests/HexServer.Protocol.Tests/HexServer.Protocol.Tests.csproj -c Release

## Catalog the original client

Use the original DLL as an input to the extractor; do not copy the full client runtime into the server.

    dotnet run --project tools/HexExtractor -- ^
      <Assembly-CSharp-firstpass.dll> ^
      artifacts/firstpass.catalog.json ^
      Game.Shared

The catalog records type names, fields, methods and assembly references.

## Probe legacy shared types

    dotnet run --project tools/LegacyLoadProbe -- ^
      <Assembly-CSharp-firstpass.dll>

Default targets:

    Game.Shared.Mechanics.Card
    Game.Shared.Session
    Game.Shared.AuthoritativeSessionBase
    Game.Shared.Mechanics.Transactions.Transaction
    Game.Shared.Network.DataWrapper
    Game.Shared.Network.HConnect.Session

A successful load does not prove that a type is safe to execute headlessly. It only proves the loader can resolve the requested Type object with the supplied assembly directory.

## Classification

DIRECT_REUSE
    pure shared rule code with no Unity/client dependency

ADAPTER_REUSE
    rule code with a small external data/provider dependency

ORACLE_ONLY
    useful for behavioral comparison, not for production runtime

CLIENT_ONLY
    UI/rendering/input/network presentation

## Differential testing

Run the same logical transaction through the original compatible runtime and the new server, then compare:

    state
    events
    resources
    priority
    stack
    card locations

Record the first divergence.

## Protocol fixture policy

Always keep raw bytes in fixtures.

A parsed header/JSON representation is not sufficient to prove compatibility.

Recommended capture fields:

    direction
    timestamp
    raw frame
    HCP header
    DataWrapper DataType
    RequestId
    compression
    decoded object

## Evidence priority

    IL / metadata
        >
    runtime behavior
        >
    golden bytes
        >
    decompiler presentation

Decompiler-generated C# can change compiler-generated structures and should not be treated as the authoritative ABI.
