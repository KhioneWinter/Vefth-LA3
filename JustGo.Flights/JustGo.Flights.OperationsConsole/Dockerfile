FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY JustGo.Flights.Contracts/ JustGo.Flights.Contracts/
COPY JustGo.Flights.OperationsConsole/ JustGo.Flights.OperationsConsole/

RUN dotnet publish JustGo.Flights.OperationsConsole/JustGo.Flights.OperationsConsole.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "JustGo.Flights.OperationsConsole.dll"]