# syntax=docker/dockerfile:1
# Build context must be the repository root: docker build -f Appointment.Server/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# The Angular client is built and served by its own container
ENV SkipSpa=true
COPY Appointment.Server/Appointment.Server.csproj Appointment.Server/
RUN dotnet restore Appointment.Server/Appointment.Server.csproj
COPY Appointment.Server/ Appointment.Server/
RUN dotnet publish Appointment.Server/Appointment.Server.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DisableHttpsRedirection=true \
    Swagger__Enabled=true

COPY --from=build /app/publish ./

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "Appointment.Server.dll"]
