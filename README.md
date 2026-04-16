# Portfolio Optimization — Projeto 2 (Programação Funcional, F#)

Otimização por força bruta da alocação de uma carteira long-only do Dow Jones
buscando maximizar o **Sharpe Ratio**. Escrito em **F# / .NET 8** usando
**funções puras** e paralelismo via `Async.Parallel`.

---

## Problema

- 30 ações do índice Dow Jones, escolher um subconjunto de **20** (C(30,20) ≈ 30 milhões de combinações).
- Para cada combinação, sortear **1.000.000** de vetores de pesos e guardar o melhor.
- Restrições: `w_i ≥ 0`, `w_i ≤ 0.20`, `Σ w_i = 1`.
- Objetivo: maximizar **SR = μ / σ** (risk-free desconsiderado para comparação).

Fórmulas (todas implementadas como funções puras em `Portfolio.fs`):

- Retorno anualizado da carteira: `μ = (mean(r) · w) · 252`
- Volatilidade anualizada: `σ = √(wᵀ C w) · √252`
- Sharpe: `SR = μ / σ`

---

## Como eu escolhi usar F# aqui (rubrica B+)

F# é uma linguagem **funcional** (dialeto de ML na CLR) — portanto o projeto
se encaixa na faixa **B+** da rubrica. Os pilares funcionais estão presentes:

- **Funções puras** em `Portfolio.fs` (`annualizedReturn`, `annualizedVolatility`,
  `sharpeRatio`, `evaluate`, `sliceColumns`, `meanReturns`, `covarianceMatrix`,
  `combinations`). Mesma entrada ⇒ mesma saída, sem estado compartilhado.
- **Pipeline funcional** em `Simulate.findBestPortfolio`
  (`Array.map → Async.Parallel → Array.choose → Array.filter → Array.maxBy`),
  seguindo o padrão sugerido no enunciado.
- **Paralelismo via `Async.Parallel`** (mesma receita do exemplo `collatzWithSteps`
  fornecido nos exemplos de aula).
- **Imutabilidade** como default; mutação restrita a laços internos das funções
  puras, sem escapar de seu escopo (arrays construídos e retornados).

---

## Estrutura do projeto

```
portfolio-optimization/
├── Dockerfile              # build multi-stage + runtime .NET 8
├── docker-compose.yml      # atalhos para os modos (sample/benchmark/big)
├── .dockerignore
├── .gitignore
├── README.md               # este arquivo
└── src/
    ├── PortfolioOptimization.fsproj
    ├── Types.fs            # tipos do domínio (Weights, ReturnsMatrix, ...)
    ├── DataLoader.fs       # tickers do Dow Jones + geração de retornos sintéticos
    ├── Portfolio.fs        # FUNÇÕES PURAS: retorno, volatilidade, Sharpe, combinações
    ├── Simulate.fs         # pipeline paralelo (Async.Parallel)
    └── Program.fs          # CLI (modo sample / benchmark)
```

---

## Rodando com Docker (não precisa instalar F#/.NET)

### Pré-requisito único

Ter **Docker** instalado. Nada mais é necessário — o build compila o F# dentro
do container.

### Opção A — `docker-compose` (mais simples)

Da pasta do projeto:

```bash
# Sample rápido (~10-30 s dependendo da máquina)
docker compose run --rm sample

# Benchmark: paralelo vs sequencial, 5 runs cada
docker compose run --rm benchmark

# Run maior (alguns minutos)
docker compose run --rm big
```

### Opção B — `docker run` direto

```bash
# 1) build
docker build -t portfolio-opt .

# 2) sample padrão
docker run --rm portfolio-opt

# 3) customizando parâmetros
docker run --rm portfolio-opt --mode=sample --max-combos=1000 --sims=5000

# 4) benchmark
docker run --rm portfolio-opt --mode=benchmark --max-combos=200 --sims=1000 --runs=5

# 5) forçar sequencial (sem paralelismo)
docker run --rm portfolio-opt --mode=sample --parallel=false --max-combos=100
```

---

## Parâmetros da CLI

| Flag             | Default | Significado                                                        |
|------------------|---------|--------------------------------------------------------------------|
| `--mode`         | `sample`| `sample` ou `benchmark`                                            |
| `--max-combos`   | `500`   | quantas combinações C(30,20) avaliar (sample do espaço completo)  |
| `--sims`         | `2000`  | sorteios de pesos por combinação                                   |
| `--days`         | `126`   | dias de retorno sintético a gerar (~2º semestre de 2025)           |
| `--seed`         | `42`    | semente do RNG — garante reprodutibilidade                         |
| `--parallel`     | `true`  | liga/desliga `Async.Parallel`                                      |
| `--runs`         | `5`     | (benchmark) rodadas por modo                                       |

---

## Sample de hoje — por que não rodar os 30 trilhões?

O problema completo são **30 milhões de combinações × 1 milhão de sorteios
≈ 30 trilhões de simulações** (ver slide 10 do enunciado). Mesmo com
paralelismo, rodar tudo leva dias — então esta entrega de hoje faz um
**sample**: por padrão `--max-combos=500 --sims=2000` (1 milhão de simulações
totais), que termina em **segundos** e já mostra que o pipeline está correto
e que ele escala com paralelismo.

Para o entregável final basta rodar com valores maiores (ex.
`--max-combos=30000000 --sims=1000000`) e paciência.

---

## O que você vai ver na saída

```
╔══════════════════════════════════════════════════════════════════╗
║       PORTFOLIO OPTIMIZATION — Sample Run (F# / .NET 8)         ║
╚══════════════════════════════════════════════════════════════════╝

Configuração:
  Modo               : sample
  Paralelo           : true
  Max combinações    : 500  (C(30,20) = 30.045.015 no total)
  Simulações/combo   : 2,000
  ...

  Avaliando 500 combinações × 2000 simulações de pesos = 1,000,000 simulações...

Tempo total: 3.42 s

  ════════════════════════════════════════════════════════════════
  MELHOR CARTEIRA ENCONTRADA
  ════════════════════════════════════════════════════════════════
  Retorno anualizado    :   24.18%
  Volatilidade anualiz. :   13.42%
  Sharpe Ratio          :   1.8016
  Ativos (20) e pesos:
    MSFT     19.87%  ███████████████████
    AAPL     17.52%  █████████████████
    ...
  Soma dos pesos        :  1.0000
  ════════════════════════════════════════════════════════════════
```

> Observação: os **retornos são sintéticos** (movimento browniano geométrico
> com fator de mercado comum, seed reprodutível). Para a entrega final da
> disciplina basta trocar `DataLoader.generateSyntheticReturns` por uma função
> que lê um CSV real do Yahoo Finance (a função `loadReturnsFromCsv` já está
> lá, é só plugar).

---

## Paralelismo — como foi feito

No `Simulate.fs`:

```fsharp
let bestPortfolioForCombinationAsync allTickers allReturns config seedBase combo =
    async {
        return bestPortfolioForCombination allTickers allReturns config seedBase combo
    }

// ... e no pipeline:
combos
|> Array.map (bestPortfolioForCombinationAsync ...)
|> Async.Parallel
|> Async.RunSynchronously
|> Array.choose id
|> Array.filter (fun p -> p.Sharpe > 0.0)
|> Array.maxBy (fun p -> p.Sharpe)
```

Cada combinação é avaliada por uma função **pura** — sem mutação compartilhada,
sem I/O — então o `Async.Parallel` é trivialmente seguro. A seed de cada
combinação é **derivada** dos índices escolhidos, garantindo reprodutibilidade
mesmo com paralelismo.

---

## Extensões implementadas (rubrica)

- [x] Comparação paralelo vs sequencial com múltiplas rodadas (`--mode=benchmark --runs=5`)
- [ ] Obter dados via API (estrutura pronta em `DataLoader.loadReturnsFromCsv`; ainda usa dados sintéticos)
- [ ] Testar melhor carteira no 1º trimestre de 2025 (pendente — depende de dados reais)

---

## Roadmap para a entrega final

1. Substituir dados sintéticos por CSV real do Dow Jones (Yahoo Finance /
   Alpha Vantage) carregado via `loadReturnsFromCsv`.
2. Rodar com `--max-combos=30000000 --sims=1000000` numa máquina com muitos
   núcleos (GCP/AWS, ou local se tiver paciência).
3. Adicionar teste out-of-sample no 1º trimestre de 2025.
