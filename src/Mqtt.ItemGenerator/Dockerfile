#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Mqtt.ItemGenerator/Mqtt.ItemGenerator.csproj", "Mqtt.ItemGenerator/"]
COPY ["Mqtt.Shared/Mqtt.Shared.csproj", "Mqtt.Shared/"]
RUN dotnet restore "Mqtt.ItemGenerator/Mqtt.ItemGenerator.csproj"
COPY . .
WORKDIR "/src/Mqtt.ItemGenerator"
RUN dotnet build "Mqtt.ItemGenerator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Mqtt.ItemGenerator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
ENV DOTNET_gcServer=1
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mqtt.ItemGenerator.dll"]