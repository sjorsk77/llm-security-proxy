FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Proxy/Proxy.csproj", "Proxy/"]
COPY ["Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "Proxy/Proxy.csproj"
COPY . .
WORKDIR "/src/Proxy"
RUN dotnet build "./Proxy.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Proxy.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

USER root
RUN mkdir -p /app/data && chown -R &APP_UID:$APP_UID /app/data
USER $APP_UID

ENTRYPOINT ["dotnet", "Proxy.dll"]
