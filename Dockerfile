# syntax=docker/dockerfile:1

# ---- 1. Angular client ----
FROM node:20-alpine AS client-build
WORKDIR /src/appointment.client
COPY appointment.client/package*.json ./
RUN npm ci
COPY appointment.client/ ./
RUN npx ng build --configuration production

# ---- 2. .NET restore/build (also usable as a test target: docker build --target test .) ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS server-build
WORKDIR /src
ENV SkipSpa=true
COPY Appointment.Server/Appointment.Server.csproj Appointment.Server/
COPY Appointment.Tests/Appointment.Tests.csproj Appointment.Tests/
RUN dotnet restore Appointment.Server/Appointment.Server.csproj && dotnet restore Appointment.Tests/Appointment.Tests.csproj
COPY Appointment.Server/ Appointment.Server/
COPY Appointment.Tests/ Appointment.Tests/

FROM server-build AS test
RUN dotnet test Appointment.Tests/Appointment.Tests.csproj --no-restore

FROM server-build AS publish
RUN dotnet publish Appointment.Server/Appointment.Server.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---- 3. Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DisableHttpsRedirection=true \
    Swagger__Enabled=true

COPY --from=publish /app/publish ./
COPY --from=client-build /src/appointment.client/dist/appointment.client/browser ./wwwroot

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "Appointment.Server.dll"]
