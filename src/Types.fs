module PortfolioOptimization.Types

/// Matriz de retornos diários: linhas = dias, colunas = ações
type ReturnsMatrix = float[][]

/// Vetor de pesos de uma carteira (soma = 1)
type Weights = float[]

/// Matriz de covariância dos retornos
type CovMatrix = float[][]

/// Resultado avaliado de uma carteira
type EvaluatedPortfolio = {
    Tickers: string[]
    Weights: Weights
    AnnualReturn: float
    AnnualVolatility: float
    Sharpe: float
}

/// Configuração da simulação
type SimConfig = {
    NumAssetsInPortfolio: int      // quantos ativos escolher (20)
    TotalAssets: int               // total disponível (30)
    SimulationsPerCombination: int // sorteios de pesos por combinação (1.000.000)
    MaxWeight: float               // restrição de concentração (0.20)
    MaxCombinations: int option    // limite de combinações (para sample)
    TradingDays: float             // 252
    UseParallel: bool              // liga/desliga paralelismo
    Seed: int                      // semente para reprodutibilidade
}
