FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY Producer/Producer.csproj Producer/

RUN dotnet restore Producer/Producer.csproj

COPY Producer/ Producer/

RUN dotnet publish Producer/Producer.csproj \
    -c Release \
    -o /app/publish


FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Producer.dll"]