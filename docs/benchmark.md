# Benchmark — Paralelo vs Sequencial

Comando executado:
```bash
docker compose run --rm benchmark
```

Configuração do benchmark (valores no `docker-compose.yml`):
- `--max-combos=5000 --sims=10000` → **50 milhões de simulações por rodada**
- 5 rodadas em modo sequencial + 5 rodadas em modo paralelo
- Máquina: macOS Darwin 24.6.0, 10 cores disponíveis (Apple M-series)
- Dados: 30 CSVs reais do Yahoo Finance, 2º semestre de 2025 (127 dias × 30 ativos)

## Saída completa

```
╔══════════════════════════════════════════════════════════════════╗
║       PORTFOLIO OPTIMIZATION — Benchmark (paralelo vs seq)      ║
╚══════════════════════════════════════════════════════════════════╝

Executando 5 rodadas em cada modo...

→ Modo SEQUENCIAL:
  Avaliando 5000 combinações × 10000 simulações de pesos = 50,000,000 simulações...
  [SEQ] run 1: 18.29 s
  Avaliando 5000 combinações × 10000 simulações de pesos = 50,000,000 simulações...
  [SEQ] run 2: 17.71 s
  Avaliando 5000 combinações × 10000 simulações de pesos = 50,000,000 simulações...
  [SEQ] run 3: 17.63 s
  Avaliando 5000 combinações × 10000 simulações de pesos = 50,000,000 simulações...
  [SEQ] run 4: 17.72 s
  Avaliando 5000 combinações × 10000 simulações de pesos = 50,000,000 simulações...
  [SEQ] run 5: 17.59 s

→ Modo PARALELO:
  Avaliando 5000 combinações × 10000 simulações de pesos = 50,000,000 simulações...
  [PAR] run 1: 3.14 s
  Avaliando 5000 combinações × 10000 simulações de petos = 50,000,000 simulações...
  [PAR] run 2: 3.26 s
  Avaliando 5000 combinações × 10000 simulações de pesos = 50,000,000 simulações...
  [PAR] run 3: 3.10 s
  Avaliando 5000 combinações × 10000 simulações de pesos = 50,000,000 simulações...
  [PAR] run 4: 3.13 s
  Avaliando 5000 combinações × 10000 simulações de pesos = 50,000,000 simulações...
  [PAR] run 5: 3.02 s

  ════════════════════════════════════════════════════════════════
  RESULTADOS (5 runs cada, 5000 combos × 10000 sims)
  ════════════════════════════════════════════════════════════════
  Sequencial : média = 17.79 s  | min = 17.59 s  | max = 18.29 s
  Paralelo   : média =  3.13 s  | min =  3.02 s  | max =  3.26 s
  Speedup    : 5.68x
  ════════════════════════════════════════════════════════════════
```

## Interpretação

Com 50 milhões de simulações por rodada, os tempos sequenciais (≈17.7 s) e
paralelos (≈3.1 s) estão dominados pelo trabalho real — sem ruído de arranque.
O speedup de **5.68×** em 10 cores reflete a eficiência do `Async.Parallel`:
cada combinação é uma tarefa pura (sem mutação compartilhada), mas há overhead
de agendamento do threadpool do .NET para 5.000 tarefas pequenas, o que limita
o speedup abaixo do ideal teórico de 10×.
