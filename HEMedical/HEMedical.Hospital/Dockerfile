FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /src
COPY ["HEMedical.Hospital/HEMedical.Hospital.csproj", "HEMedical.Hospital/"]
COPY ["HEMedical.Shared/HEMedical.Shared.csproj", "HEMedical.Shared/"]
RUN dotnet restore "HEMedical.Hospital/HEMedical.Hospital.csproj"
COPY . .
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle --project HEMedical.Hospital/HEMedical.Hospital.csproj --output /app/efbundle --self-contained
RUN dotnet publish "HEMedical.Hospital/HEMedical.Hospital.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=publish /app/efbundle ./efbundle
ENTRYPOINT ["sh", "-c", "./efbundle --connection \"${Database__DefaultConnection}\" && dotnet HEMedical.Hospital.dll"]
