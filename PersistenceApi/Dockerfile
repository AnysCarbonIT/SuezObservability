FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY PersistenceApi/PersistenceApi.csproj PersistenceApi/

RUN dotnet restore PersistenceApi/PersistenceApi.csproj

COPY PersistenceApi/ PersistenceApi/

RUN dotnet publish PersistenceApi/PersistenceApi.csproj \
    -c Release \
    -o /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PersistenceApi.dll"]