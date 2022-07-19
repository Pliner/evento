FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS="http://+:8000"
EXPOSE 8000

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS publish
WORKDIR /src

COPY *.sln .

COPY */*.csproj .
RUN for file in $(ls *.csproj); do mkdir -p ${file%.*} && mv $file ${file%.*}; done
RUN dotnet restore

COPY . .
RUN dotnet build -c Release -o /app
RUN dotnet publish "Evento/Evento.csproj" -c Release -o /app/publish

RUN dotnet tool install --global dotnet-ef
ENV PATH $PATH:/root/.dotnet/tools
RUN dotnet ef migrations bundle --project Evento --no-build -o /app/publish/efbundle

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Evento.dll"]
