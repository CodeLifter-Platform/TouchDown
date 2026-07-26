FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["TouchDown/TouchDown.csproj", "TouchDown/"]
COPY ["Data/Data.csproj", "Data/"]
RUN dotnet restore "TouchDown/TouchDown.csproj"
COPY . .
WORKDIR "/src/TouchDown"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Keep the SQLite file on the mounted volume — the working directory is not persisted,
# so a default relative path would put the DB inside the container layer and lose it on
# every recreate.
RUN mkdir -p /app/data
ENV ConnectionStrings__TouchDown="Data Source=/app/data/touchdown.db"
ENV ConnectionStrings__Hangfire="/app/data/hangfire.db"
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "TD.dll"]
