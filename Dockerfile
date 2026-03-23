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
ENTRYPOINT ["dotnet", "TD.dll"]
