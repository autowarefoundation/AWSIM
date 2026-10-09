# Curvas e parada diante de obstáculo

Resultado confirmado em 09/10/2026: **os três casos foram aprovados** no servidor do apartamento, usando o CORE existente.

| Resultado | Esquerda | Direita | Obstáculo |
|---|---|---|---|
| Estado final de teste | ARRIVED | ARRIVED | AVOIDING_OBSTACLE, parado |
| Mudança de direção | +43,28° | −43,35° | ~0° |
| Erro final ao destino | 0,385 m | 0,365 m | Destino bloqueado; não houve ARRIVED |
| Desvio radial máximo | 0,0143 m | 0,0138 m | Não se aplica |
| Velocidade máxima | 0,535 m/s | 0,534 m/s | 0,505 m/s |
| Velocidade final | Zero | Zero | Zero |
| Rodas apoiadas | 4 durante a manobra | 4 durante a manobra | 4 durante a manobra |
| Contato com barreira | Nenhum | Nenhum | Nenhum; folga corporal mínima 4,342 m |

A parada diante da barreira foi preventiva, pela decisão do planejador ao perceber o caminho bloqueado. A distância de emergência de 3 m não foi alcançada. Este resultado não comprova frenagem de emergência nesse limite. No instante da detecção, a velocidade era aproximadamente 0,499 m/s; o shuttle se deslocou 1,211 m desde a partida antes de parar.

Os hashes originais dos dois executáveis do CORE foram preservados, seus arquivos não tiveram alterações e os serviços de simulador, ponte, cluster e painel ficaram desligados após os testes. Repetir curvas e bloqueios nas ruas de Shinjuku continua sendo uma etapa posterior.

Os testes usam `JidoShuttleValidationHDRP.unity`, uma pista plana de 100 × 100 m, com o mesmo prefab físico do shuttle, LiDAR VLP16, IMU, ponte UDP/Kafka e os binários existentes do CORE e do cluster. O cenário operacional de Shinjuku fica no build separado `Builds/JidoShuttle`. Esta etapa não valida curvas urbanas, cruzamentos ou tráfego.

O teste `tools/jido/shuttle_maneuvers_check.py` reinicia a simulação entre os casos. Usa a pose assentada como origem da rota e o inverso elipsoidal da ponte para gerar GeoJSON compatível com o parser do CORE. Registra localização, estado do CORE, nuvem processada pelo cluster e diagnóstico independente de física.

| Caso | Critério |
|---|---|
| Curva à esquerda | Arco de 8 m e raio de 10 m; NAVIGATING e ARRIVED; yaw positivo superior a 0,4 rad; erro final < 0,7 m e desvio radial < 0,5 m |
| Curva à direita | Mesmo arco espelhado; yaw negativo inferior a −0,4 rad; demais critérios iguais |
| Obstáculo | Barreira de 12 × 1,2 × 1 m, centrada em Unity z=8; ativa após o corpo ultrapassar z=1; detecção frontal real pelo LiDAR; movimento prévio; parada com folga corporal > 0,3 m e sem contato físico |

Todos os casos exigem quatro rodas em contato com o chão, velocidade máxima < 0,8 m/s e velocidade final < 0,1 m/s. O limite configurado é 0,6 m/s, com acelerador limitado a 20%. O CORE usa distância de emergência de 3 m, sem alterações de código. A compensação de partida de 20% vale até 0,3 m/s. O limite de área desta pista é 50 m.

A folga diante da barreira é calculada a partir da posição e orientação física do corpo, conservando o envelope externo de 3,8489 × 2,2609 × 2,135 m. O diagnóstico também registra colisão com o colisor real da barreira. A pista não mede o desempenho contra um obstáculo que surge a distância crítica nem a resposta a pedestres.

Os resultados individuais ficam em `Logs/jido-shuttle-left-check.json`, `Logs/jido-shuttle-right-check.json` e `Logs/jido-shuttle-obstacle-check.json`. O resumo fica em `Logs/jido-shuttle-maneuvers-check.json`; conferir `passed`, sem inferir aprovação apenas pela existência do arquivo. O script encerra seu CORE e desliga simulador, ponte e cluster no fim de cada caso.
