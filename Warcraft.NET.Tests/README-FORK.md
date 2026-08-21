# Fork test notes

Read this before judging a suite run in this fork (`Trichome707/Warcraft.NET`).

## Running the suite

The test project targets `net9.0`, but only the .NET 10 runtime is installed here, so a plain
`dotnet test` aborts with "You must install or update .NET to run this application". Roll forward
rather than retargeting the csproj:

    DOTNET_ROLL_FORWARD=LatestMajor dotnet test Warcraft.NET.sln

## Known-failing: MVERTests (4), environmental

`MVERTests` fails all four of its tests -- `GetSignature`, `GetSize`, `LoadBinaryData`,
`Serialize` -- with `WebException: The remote server returned an error: (400) Bad Request`. The
class downloads a fixture in its constructor, so every test fails before its body runs. This is a
dead remote, not a code regression, and touches no chunk parsing.

## Expected counts

As of 2026-08-20: **21 passed, 4 failed, 25 total**, the 4 being those MVERTests. Anything else
that fails is real.
