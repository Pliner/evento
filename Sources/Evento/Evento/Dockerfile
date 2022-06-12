FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS="http://+:8000"
EXPOSE 8000

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS publish
WORKDIR /src
COPY ["./Evento.csproj", "."]
RUN dotnet restore "Evento.csproj"
COPY . .
RUN dotnet publish "Evento.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Evento.dll"]
