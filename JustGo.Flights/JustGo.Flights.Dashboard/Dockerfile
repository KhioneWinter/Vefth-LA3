FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build 
WORKDIR /src

COPY JustGo.Flights.Contracts/ JustGo.Flights.Contracts/
COPY JustGo.Flights.Dashboard/ JustGo.Flights.Dashboard/

RUN dotnet publish JustGo.Flights.Dashboard/JustGo.Flights.Dashboard/JustGo.Flights.Dashboard.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "JustGo.Flights.Dashboard.dll"]