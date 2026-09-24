FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Financeiro/Financeiro.csproj", "src/Financeiro/"]
COPY ["src/Financeiro.Domain/Financeiro.Domain.csproj", "src/Financeiro.Domain/"]
RUN dotnet restore "src/Financeiro/Financeiro.csproj"
COPY . .
WORKDIR "/src/src/Financeiro"
RUN dotnet build "Financeiro.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Financeiro.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Financeiro.dll"]