FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/PayDesk.Web/PayDesk.Web.csproj", "src/PayDesk.Web/"]
RUN dotnet restore "src/PayDesk.Web/PayDesk.Web.csproj"

COPY . .
RUN dotnet publish "src/PayDesk.Web/PayDesk.Web.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PayDesk.Web.dll"]
