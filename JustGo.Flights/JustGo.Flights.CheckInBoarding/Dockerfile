# builds the app with the full .NET SDK
FROM  mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# contracts needs to be coppead in as check in references contracts
COPY JustGo.Flights.Contracts/ JustGo.Flights.Contracts/
COPY JustGo.Flights.CheckInBoarding/ JustGo.Flights.CheckInBoarding/

RUN dotnet publish JustGo.Flights.CheckInBoarding/JustGo.Flights.CheckInBoarding.csproj -c Release -o /app

# build output into a small runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "JustGo.Flights.CheckInBoarding.dll"]