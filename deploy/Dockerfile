# Prohori.Api — container image for Render (or any Docker host).
#   docker build -f deploy/Dockerfile -t prohori-api .
#   docker run -p 8080:8080 -e Fhir__BaseUrl=https://sandbox.fhir.dghs.gov.bd/fhir prohori-api

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/Prohori.Api/Prohori.Api.csproj src/Prohori.Api/
RUN dotnet restore src/Prohori.Api/Prohori.Api.csproj

COPY src/ src/
RUN dotnet publish src/Prohori.Api/Prohori.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1
EXPOSE 8080

# the aspnet:8.0 image already ships a non-root "app" user
USER app

ENTRYPOINT ["dotnet", "Prohori.Api.dll"]
