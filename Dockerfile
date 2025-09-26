# Use the official .NET 8.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

# Install Node.js for TailwindCSS build
RUN curl -fsSL https://deb.nodesource.com/setup_18.x | bash - \
    && apt-get install -y nodejs

WORKDIR /app

# Copy package.json and package-lock.json for npm dependencies
COPY package*.json ./
RUN npm install

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN npm run build:css
RUN ls -la wwwroot/css/ && echo "CSS files:" && ls -la wwwroot/css/

RUN dotnet publish -c Release -o out

# Use the official .NET 8.0 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Install Chrome dependencies for PuppeteerSharp
RUN apt-get update && apt-get install -y \
    curl \
    wget \
    gnupg \
    ca-certificates \
    fonts-liberation \
    libasound2 \
    libatk-bridge2.0-0 \
    libdrm2 \
    libxcomposite1 \
    libxdamage1 \
    libxrandr2 \
    libgbm1 \
    libxss1 \
    libnss3 \
    libxshmfence1 \
    && wget -q -O - https://dl.google.com/linux/linux_signing_key.pub | apt-key add - \
    && echo "deb [arch=amd64] http://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list \
    && apt-get update \
    && apt-get install -y google-chrome-stable \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /https

# Create app directory
WORKDIR /app

# Create logs directory
RUN mkdir -p /app/Logs

# Copy the build output
COPY --from=build-env /app/out .

# Create non-root user for security (after installing packages)
RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN chown -R appuser:appuser /app
USER appuser

# Expose port
EXPOSE 8080 8081

# Set environment variables
ENV ASPNETCORE_URLS=https://+:8080;http://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -k -f http://localhost:8081/health || exit 1

# Start the application
ENTRYPOINT ["dotnet", "Freelancing.dll"]