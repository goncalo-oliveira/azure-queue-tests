FROM microsoft/dotnet:2.1-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.1-sdk AS build
ARG PRIVATE_REPOSITORY
WORKDIR /build
COPY *.sln ./
COPY src/queue-auth-app/queue-auth-app.csproj src/queue-auth-app/
RUN dotnet restore --source https://api.nuget.org/v3/index.json
COPY . .
WORKDIR /build/src/queue-auth-app
RUN dotnet build -c Release -o /app

FROM build AS publish
RUN dotnet publish -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "QueueAuthTests.dll"]
