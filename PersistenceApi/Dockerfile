FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY PersistenceApi/PersistenceApi.csproj PersistenceApi/
COPY Shared/Shared.csproj Shared/

RUN dotnet restore PersistenceApi/PersistenceApi.csproj

COPY PersistenceApi/ PersistenceApi/
COPY Shared/ Shared/

RUN dotnet publish PersistenceApi/PersistenceApi.csproj \
    -c Release \
    -o /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PersistenceApi.dll"]