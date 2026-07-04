FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/FCG.Payments.Api/FCG.Payments.Api.csproj", "src/FCG.Payments.Api/"]
RUN dotnet restore "src/FCG.Payments.Api/FCG.Payments.Api.csproj"

COPY . .
RUN dotnet publish "src/FCG.Payments.Api/FCG.Payments.Api.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "FCG.Payments.Api.dll"]
