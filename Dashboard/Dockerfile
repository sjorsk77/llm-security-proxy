FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Dashboard/Dashboard.csproj", "Dashboard/"]
COPY ["Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "Dashboard/Dashboard.csproj"
COPY . .
WORKDIR "/src/Dashboard"
RUN dotnet build "./Dashboard.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Dashboard.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

USER root
RUN mkdir -p /app/data && chown -R &APP_UID:$APP_UID /app/data
USER $APP_UID

ENTRYPOINT ["dotnet", "Dashboard.dll"]
