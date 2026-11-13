FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/PayDesk.Api/PayDesk.Api.csproj", "src/PayDesk.Api/"]
RUN dotnet restore "src/PayDesk.Api/PayDesk.Api.csproj"

COPY . .
RUN dotnet publish "src/PayDesk.Api/PayDesk.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PayDesk.Api.dll"]
