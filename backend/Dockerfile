FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/UbbEvents.Api/UbbEvents.Api.csproj backend/UbbEvents.Api/
RUN dotnet restore backend/UbbEvents.Api/UbbEvents.Api.csproj
COPY backend/UbbEvents.Api/ backend/UbbEvents.Api/
RUN dotnet publish backend/UbbEvents.Api/UbbEvents.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "UbbEvents.Api.dll"]
