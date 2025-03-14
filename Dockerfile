# 1️⃣ Base runtime image (lighter than SDK)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080  # Only expose one port

# Disable HTTPS redirect for Render
ENV ASPNETCORE_URLS=http://+:8080

# 2️⃣ Build image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["djbeb/djbeb.csproj", "djbeb/"]
RUN dotnet restore "djbeb/djbeb.csproj"

# Copy the rest of the app and build
COPY . .
WORKDIR "/src/djbeb"
RUN dotnet build "djbeb.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 3️⃣ Publish the app
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "djbeb.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 4️⃣ Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set the entry point
ENTRYPOINT ["dotnet", "djbeb.dll"]