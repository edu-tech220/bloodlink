FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json BloodLink.sln ./
COPY src/BloodLink.Api/BloodLink.Api.csproj src/BloodLink.Api/
COPY src/BloodLink.Web/BloodLink.Web.csproj src/BloodLink.Web/
COPY tests/BloodLink.Api.Tests/BloodLink.Api.Tests.csproj tests/BloodLink.Api.Tests/
RUN dotnet restore src/BloodLink.Api/BloodLink.Api.csproj

COPY src/BloodLink.Api/ src/BloodLink.Api/
RUN dotnet publish src/BloodLink.Api/BloodLink.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "BloodLink.Api.dll"]
