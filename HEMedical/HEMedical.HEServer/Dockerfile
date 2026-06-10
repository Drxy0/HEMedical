FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /src
COPY ["HEMedical.HEServer/HEMedical.HEServer.csproj", "HEMedical.HEServer/"]
COPY ["HEMedical.Shared/HEMedical.Shared.csproj", "HEMedical.Shared/"]
RUN dotnet restore "HEMedical.HEServer/HEMedical.HEServer.csproj"
COPY . .
RUN dotnet publish "HEMedical.HEServer/HEMedical.HEServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HEMedical.HEServer.dll"]
