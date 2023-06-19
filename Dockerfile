#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build
WORKDIR /src
COPY ["/ArashiDNS.C.csproj", "/"]
RUN dotnet restore "/ArashiDNS.C.csproj"
COPY . .
WORKDIR /src
RUN dotnet build -c "/ArashiDNS.C.csproj" Release -o /app/build /p:PublishSingleFile=false /p:PublishTrimmed=false

FROM build AS publish
RUN dotnet publish -c "/ArashiDNS.C.csproj" Release -o /app/publish /p:PublishSingleFile=false /p:PublishTrimmed=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 53
ENTRYPOINT ["dotnet", "ArashiDNS.C.dll"]
