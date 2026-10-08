FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY CacheApi/CacheApi.csproj CacheApi/
COPY Shared/Shared.csproj Shared/

RUN dotnet restore CacheApi/CacheApi.csproj

COPY CacheApi/ CacheApi/
COPY Shared/ Shared/

RUN dotnet publish CacheApi/CacheApi.csproj \
    -c Release \
    -o /app/publish


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CacheApi.dll"]