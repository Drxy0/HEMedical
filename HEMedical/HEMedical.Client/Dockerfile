FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /src
COPY ["HEMedical.Client/HEMedical.Client.csproj", "HEMedical.Client/"]
COPY ["HEMedical.Shared/HEMedical.Shared.csproj", "HEMedical.Shared/"]
RUN dotnet restore "HEMedical.Client/HEMedical.Client.csproj"
COPY . .
RUN dotnet publish "HEMedical.Client/HEMedical.Client.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=publish /src/HEMedical.Client/public.key .
COPY --from=publish /src/HEMedical.Client/secret.key .
ENTRYPOINT ["dotnet", "HEMedical.Client.dll"]
