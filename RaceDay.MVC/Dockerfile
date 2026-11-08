
# ==========================================
# RACEDAY MVC - DOCKERFILE
# ASP.NET Core 8
# ==========================================

# STAGE 1: BUILD
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy the project file first
COPY RaceDay.MVC/RaceDay.MVC.csproj RaceDay.MVC/

# Restore NuGet packages
RUN dotnet restore "RaceDay.MVC/RaceDay.MVC.csproj"

# Copy the MVC application source
COPY RaceDay.MVC/ RaceDay.MVC/

# Publish the application
RUN dotnet publish "RaceDay.MVC/RaceDay.MVC.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# STAGE 2: RUNTIME
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

# ASP.NET Core listens on port 8080
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

# Copy the published application
COPY --from=build /app/publish .

# Run as a non-root user
USER $APP_UID

# Start RaceDay.MVC
ENTRYPOINT ["dotnet", "RaceDay.MVC.dll"]
