FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /src
COPY ["HEMedical.HospitalProxy/HEMedical.HospitalProxy.csproj", "HEMedical.HospitalProxy/"]
COPY ["HEMedical.Shared/HEMedical.Shared.csproj", "HEMedical.Shared/"]
RUN dotnet restore "HEMedical.HospitalProxy/HEMedical.HospitalProxy.csproj"
COPY . .
RUN dotnet publish "HEMedical.HospitalProxy/HEMedical.HospitalProxy.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=publish /src/HEMedical.HospitalProxy/public.key .
ENTRYPOINT ["dotnet", "HEMedical.HospitalProxy.dll"]
