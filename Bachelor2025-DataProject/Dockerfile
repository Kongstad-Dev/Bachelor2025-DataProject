FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Bachelor2025-DataProject/Bachelor2025-DataProject.csproj", "Bachelor2025-DataProject/"]
RUN dotnet restore "Bachelor2025-DataProject/Bachelor2025-DataProject.csproj"
COPY . .
WORKDIR "/src/Bachelor2025-DataProject"
RUN dotnet build "Bachelor2025-DataProject.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Bachelor2025-DataProject.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Bachelor2025-DataProject.dll"]
