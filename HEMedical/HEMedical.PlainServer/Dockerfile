FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /src
COPY ["HEMedical.PlainServer/HEMedical.PlainServer.csproj", "HEMedical.PlainServer/"]
COPY ["HEMedical.Shared/HEMedical.Shared.csproj", "HEMedical.Shared/"]
RUN dotnet restore "HEMedical.PlainServer/HEMedical.PlainServer.csproj"
COPY . .
RUN dotnet publish "HEMedical.PlainServer/HEMedical.PlainServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HEMedical.PlainServer.dll"]
