# ──────────────────────────────────────────────────────────────────────
# Stage 1: build — compila o projeto F# em Release
# ──────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia apenas o csproj/fsproj primeiro para cachear `dotnet restore`
COPY src/PortfolioOptimization.fsproj ./
RUN dotnet restore PortfolioOptimization.fsproj

# Agora copia o resto do código-fonte e publica
COPY src/ ./
RUN dotnet publish PortfolioOptimization.fsproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ──────────────────────────────────────────────────────────────────────
# Stage 2: runtime — imagem enxuta só com o runtime .NET
# ──────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
COPY data/ ./data/

# GC em modo servidor + concurrent (melhor para cargas paralelas)
ENV DOTNET_gcServer=1
ENV DOTNET_gcConcurrent=1

ENTRYPOINT ["dotnet", "PortfolioOptimization.dll"]

# Por padrão, roda o modo "sample" com valores leves que terminam em segundos.
# Você pode sobrescrever passando flags ao `docker run`:
#   docker run --rm portfolio-opt --mode=sample --max-combos=200 --sims=1000
#   docker run --rm portfolio-opt --mode=benchmark --runs=5
CMD ["--mode=sample", "--max-combos=500", "--sims=2000"]
