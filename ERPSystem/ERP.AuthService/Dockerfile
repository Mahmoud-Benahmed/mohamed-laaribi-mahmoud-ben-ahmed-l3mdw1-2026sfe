# ---------- Build Stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy csproj first for Docker layer caching
COPY ERP.AuthService.csproj .

RUN dotnet restore

# Copy remaining files
COPY . .

RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false


# ---------- Runtime Stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

# Install curl for Docker healthchecks
RUN apt-get update \
    && apt-get install -y curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ERP.AuthService.dll"]