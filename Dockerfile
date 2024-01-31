#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["ArashiDNS.C.csproj", "."]
RUN dotnet restore "./ArashiDNS.C.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "ArashiDNS.C.csproj" -c Release -o /app/build /p:UseAppHost=true /p:PublishAot=false

FROM build AS publish
RUN dotnet publish "ArashiDNS.C.csproj" -c Release -o /app/publish /p:UseAppHost=true /p:PublishAot=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ARASHI_ANY=1
ENV ARASHI_RUNNING_IN_CONTAINER=1
EXPOSE 53
ENTRYPOINT ["dotnet", "ArashiDNS.C.dll"]
