#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["ArashiDNS.C.csproj", "."]
RUN dotnet restore "./ArashiDNS.C.csproj"
COPY . .
WORKDIR /src
RUN dotnet build "ArashiDNS.C.csproj" -c Release -o /app/build /p:PublishSingleFile=false /p:PublishTrimmed=false

FROM build AS publish
RUN dotnet publish "ArashiDNS.C.csproj" -c Release -o /app/publish /p:PublishSingleFile=false /p:PublishTrimmed=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ArashiDNS.C.dll"]