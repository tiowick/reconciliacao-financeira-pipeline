FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Financeiro.Worker/Financeiro.Worker.csproj", "src/Financeiro.Worker/"]
COPY ["src/Financeiro.Domain/Financeiro.Domain.csproj", "src/Financeiro.Domain/"]
RUN dotnet restore "src/Financeiro.Worker/Financeiro.Worker.csproj"
COPY . .
WORKDIR "/src/src/Financeiro.Worker"
RUN dotnet build "Financeiro.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Financeiro.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Financeiro.Worker.dll"]