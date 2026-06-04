# 1. Use the official .NET 10 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Copy everything and restore local dependencies
COPY *.csproj ./
RUN dotnet restore
COPY . ./

# Build and publish a release version
RUN dotnet publish -c Release -o out

# 2. Build runtime image using ASP.NET Core 10 runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .

# Tell ASP.NET to listen on the port Render expects
ENV ASPNETCORE_URLS=http://+:10000

ENTRYPOINT ["dotnet", "kuv-api.dll"]