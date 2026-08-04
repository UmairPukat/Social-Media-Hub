FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copy solution and project files
COPY ["SocialMedia.sln", "./"]
COPY ["SocialMedia.Api/SocialMedia.Api.csproj", "SocialMedia.Api/"]
COPY ["SocialMedia.Application/SocialMedia.Application.csproj", "SocialMedia.Application/"]
COPY ["SocialMedia.Domain/SocialMedia.Domain.csproj", "SocialMedia.Domain/"]
COPY ["SocialMedia.Infrastructure/SocialMedia.Infrastructure.csproj", "SocialMedia.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "SocialMedia.sln"

# Copy all source code
COPY . .

# Build the application
RUN dotnet build "SocialMedia.sln" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "SocialMedia.Api/SocialMedia.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

# Set ASP.NET Core to listen on all interfaces
ENV ASPNETCORE_URLS=http://+:5000

# Run the application
ENTRYPOINT ["dotnet", "SocialMedia.Api.dll"]

