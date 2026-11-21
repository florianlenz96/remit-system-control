# One container for UI and API (ADR 0008).
FROM node:22-alpine AS web
WORKDIR /src/src/Remit.Web
COPY src/Remit.Web/package.json src/Remit.Web/package-lock.json ./
RUN npm ci
COPY src/Remit.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/Remit.Domain/ src/Remit.Domain/
COPY src/Remit.App/ src/Remit.App/
COPY --from=web /src/src/Remit.App/wwwroot/ src/Remit.App/wwwroot/
RUN dotnet publish src/Remit.App/Remit.App.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Remit.App.dll"]
