# Portfolio Optimization — Projeto 2 (Programação Funcional, F#)

Otimização por força bruta da alocação de uma carteira long-only do Dow Jones,
buscando maximizar o **Sharpe Ratio**. Escrito em **F# / .NET 8** usando
funções puras e paralelismo via `Async.Parallel`.

---

## Problema

Dado o universo de 30 ações do Dow Jones Industrial Average (composição de 2025):

- Escolher um subconjunto de **20 ativos** — C(30, 20) = 30.045.015 combinações possíveis.
- Para cada combinação, sortear vetores de pesos aleatórios e guardar o de maior Sharpe.
- Restrições: `wᵢ ≥ 0`, `wᵢ ≤ 0.20`, `Σ wᵢ = 1`.
- Objetivo: maximizar **SR = μ / σ**.

Fórmulas (implementadas como funções puras em `src/Portfolio.fs`):

```
Retorno anualizado  :  μ = (meanReturn · w) × 252
Volatilidade anual  :  σ = √(wᵀ C w) × √252
Sharpe Ratio        :  SR = μ / σ
```

---

## Por que F# (rubrica B+)

F# é uma linguagem **funcional** nativa da CLR — o projeto encaixa na faixa B+ da rubrica.
Pilares funcionais presentes:

- **Funções puras** em `Portfolio.fs` (`annualizedReturn`, `annualizedVolatility`,
  `sharpeRatio`, `evaluate`, `sliceColumns`, `meanReturns`, `covarianceMatrix`, `combinations`).
  Mesma entrada ⇒ mesma saída, sem estado compartilhado.
- **Pipeline funcional** em `Simulate.findBestPortfolio`:
  `Array.map → Async.Parallel → Array.choose → Array.filter → Array.maxBy`,
  seguindo o padrão do exemplo `collatzWithSteps` fornecido em aula.
- **Paralelismo via `Async.Parallel`**: cada combinação é uma tarefa pura,
  sem mutação compartilhada — trivialmente seguro para paralelizar.
- **Imutabilidade** como default; mutação restrita a laços internos de funções puras,
  sem escapar de escopo.

---

## Estrutura do projeto

```
projetoPortfolio/
├── Dockerfile              # build multi-stage + runtime .NET 8
├── docker-compose.yml      # atalhos: sample / benchmark / big
├── .dockerignore
├── .gitignore
├── README.md
├── data/
│   └── raw/
│       └── *.csv           # 30 CSVs do Yahoo Finance (commitados)
├── docs/
│   └── benchmark.md        # saída completa do benchmark
├── scripts/
│   └── download_data.py    # script auxiliar de coleta (uso único, não é parte do F#)
└── src/
    ├── PortfolioOptimization.fsproj
    ├── Types.fs             # tipos do domínio (Weights, ReturnsMatrix, SimConfig…)
    ├── DataLoader.fs        # tickers do Dow + leitura dos CSVs reais
    ├── Portfolio.fs         # FUNÇÕES PURAS: retorno, vol, Sharpe, combinações
    ├── Simulate.fs          # pipeline paralelo (Async.Parallel)
    └── Program.fs           # CLI (modos sample / benchmark)
```

---

## Dados

Os retornos são calculados a partir de **30 CSVs reais do Yahoo Finance**,
baixados uma única vez e commitados em `data/raw/`.

| Item | Detalhe |
|------|---------|
| Fonte | Yahoo Finance — preço de fechamento ajustado (Adj Close) |
| Período | 01/07/2025 – 31/12/2025 (~128 pregões → 127 retornos diários) |
| Composição | Dow Jones Industrial Average 2025: **AMZN substituiu WBA** em fev/2024 |
| Formato | `Date,Open,High,Low,Close,Adj Close,Volume` (cabeçalho padrão Yahoo) |
| Localização | `data/raw/{TICKER}.csv` — 30 arquivos, um por ativo |

**Não há chamada de API dentro do F#.** Os 30 CSVs foram baixados uma única vez
usando o script Python `scripts/download_data.py` (que usa a biblioteca
`yfinance`). O script é um utilitário externo de coleta — não faz parte do
pipeline F#. O F# apenas lê os CSVs já presentes em `data/raw/`.

Para reproduzir a coleta (caso queira atualizar os dados):

```bash
cd scripts && python3 -m pip install yfinance && python3 download_data.py
```

Os retornos diários são calculados como `Adj Close_t / Adj Close_{t-1} − 1`
sobre a **interseção** de datas presentes nos 30 arquivos simultaneamente.

---

## Como rodar com Docker

### Pré-requisito único

**Docker** instalado. Não é necessário instalar F# nem .NET — o build acontece
dentro do container.

### Opção A — docker compose (recomendado)

```bash
# Sample rápido (termina em < 1 s)
docker compose run --rm sample

# Benchmark: paralelo vs sequencial, 5 rodadas cada
docker compose run --rm benchmark

# Run maior (~alguns minutos)
docker compose run --rm big
```

### Opção B — docker run direto

```bash
# 1. Build
docker build -t portfolio-opt .

# 2. Sample padrão
docker run --rm portfolio-opt

# 3. Parâmetros customizados
docker run --rm portfolio-opt --mode=sample --max-combos=1000 --sims=5000

# 4. Benchmark
docker run --rm portfolio-opt --mode=benchmark --max-combos=200 --sims=1000 --runs=5

# 5. Sequencial (sem paralelismo)
docker run --rm portfolio-opt --mode=sample --parallel=false --max-combos=100
```

---

## Parâmetros da CLI

| Flag           | Default  | Significado                                               |
|----------------|----------|-----------------------------------------------------------|
| `--mode`       | `sample` | `sample` ou `benchmark`                                   |
| `--max-combos` | `500`    | quantas combinações C(30,20) avaliar                      |
| `--sims`       | `2000`   | sorteios de pesos por combinação                          |
| `--seed`       | `42`     | semente do RNG — garante reprodutibilidade                |
| `--parallel`   | `true`   | liga/desliga `Async.Parallel`                             |
| `--runs`       | `5`      | (benchmark) número de rodadas por modo                    |

---

## Sample vs run completo

O espaço de busca completo são **30 milhões de combinações × 1 milhão de sorteios
≈ 30 trilhões de simulações**. Mesmo com paralelismo, isso levaria dias.

O default (`--max-combos=500 --sims=2000` → 1 milhão de simulações totais) termina
em menos de 1 segundo e demonstra que o pipeline está correto e que escala com
paralelismo. Para exploração mais ampla, aumente os parâmetros conforme o tempo
disponível.

---

## Saída esperada

```
╔══════════════════════════════════════════════════════════════════╗
║       PORTFOLIO OPTIMIZATION — Sample Run (F# / .NET 8)         ║
╚══════════════════════════════════════════════════════════════════╝

Configuração:
  Modo               : sample
  Paralelo           : true
  Max combinações    : 500  (C(30,20) = 30.045.015 no total)
  Simulações/combo   : 2,000
  Seed               : 42
  Cores disponíveis  : 10

Carregando retornos reais do Dow Jones de data/raw/...
  Matriz carregada: 127 dias × 30 ativos (retornos diários)
  Total de simulações que serão executadas: 1,000,000

Rodando...
  Avaliando 500 combinações × 2000 simulações de pesos = 1,000,000 simulações...

Tempo total: 0.29 s

  ════════════════════════════════════════════════════════════════
  MELHOR CARTEIRA ENCONTRADA
  ════════════════════════════════════════════════════════════════
  Retorno anualizado    :   43.24%
  Volatilidade anualiz. :   11.75%
  Sharpe Ratio          :   3.6811
  Ativos (20) e pesos:
    AAPL     15.35%  ███████████████
    JNJ      14.77%  ██████████████
    GS       12.03%  ████████████
    CAT      10.91%  ██████████
    ...
  Soma dos pesos        :  1.0000
  ════════════════════════════════════════════════════════════════
```

---

## Paralelismo — como foi feito

Cada combinação de ativos é avaliada por uma **função pura** — sem I/O, sem
mutação compartilhada. O `Async.Parallel` distribui o trabalho entre os cores:

```fsharp
// Simulate.fs — pipeline principal
combos
|> Array.map (bestPortfolioForCombinationAsync allTickers allReturns config config.Seed)
|> Async.Parallel
|> Async.RunSynchronously
|> Array.choose id
|> Array.filter (fun p -> p.Sharpe > 0.0)
|> Array.maxBy (fun p -> p.Sharpe)
```

A seed de cada combinação é **derivada deterministicamente** dos índices escolhidos:

```fsharp
let seed =
    let mutable h = seedBase
    for i = 0 to combo.Length - 1 do
        h <- h * 31 + combo.[i]
    h
```

Mesmos parâmetros ⇒ mesmo resultado, independentemente da ordem de execução paralela.

---

## Resultados de benchmark

Configuração: 5 rodadas × 5.000 combinações × 10.000 sorteios = **50 milhões de simulações/rodada**.
Máquina: 10 cores (Apple M-series), container Docker.

| Modo        | Média    | Mín      | Máx      |
|-------------|----------|----------|----------|
| Sequencial  | 17.79 s  | 17.59 s  | 18.29 s  |
| Paralelo    |  3.13 s  |  3.02 s  |  3.26 s  |
| **Speedup** | **5.68×** | —       | —        |

Saída completa das 10 rodadas em [`docs/benchmark.md`](docs/benchmark.md).

---

## Extensões da rubrica

| Item | Status |
|------|--------|
| Linguagem funcional (F#) — faixa B+ | ✅ implementado |
| Benchmark paralelo vs sequencial (5+ rodadas, `--mode=benchmark`) | ✅ implementado |
| Obter dados via API (on-demand, dentro do F#) | ❌ não implementado — os CSVs foram baixados manualmente via `scripts/download_data.py` e estão commitados em `data/raw/`. O programa F# apenas lê os arquivos locais. |
| Teste out-of-sample (ex.: Q1/2026) | ❌ não implementado nesta versão |
