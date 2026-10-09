# AWSIM no servidor do apartamento

Inspeção em 08/10/2026. Destino confirmado por SSH: `jido-carla`.

Atualização em 09/10/2026: shuttle com física própria e sensores calibrados, mapa Shinjuku com correção local de norte/escala e teste da rota de 8 m aprovado pelo CORE existente. O shuttle percorreu 7,55 m, terminou a 0,47 m do destino e parou, com velocidade máxima de 0,40 m/s e sem EMERGENCY_STOP. O CORE não foi alterado. Todos os serviços de teste ficaram desligados. Detalhes e parâmetros em [JIDO_SHINJUKU_ROTA.md](JIDO_SHINJUKU_ROTA.md); as seções abaixo preservam o histórico das etapas anteriores.

Validação adicional em pista controlada: curvas à esquerda e à direita de raio 10 m aprovadas, com desvio radial máximo de 1,44 cm; parada preventiva diante de barreira detectada pelo LiDAR aprovada, sem colisão e com 4,34 m de folga corporal. Ver [JIDO_SHUTTLE_MANOBRAS.md](JIDO_SHUTTLE_MANOBRAS.md). Esses testes não validam curvas nas ruas de Shinjuku nem frenagem de emergência a distância crítica.

## Estado confirmado antes da implantação

| Item | Resultado |
|---|---|
| Sistema | Linux x86_64, Ubuntu 24.04.4 LTS |
| GPU | NVIDIA RTX A4500, 20 GB; driver 565.77 |
| Unity | 6000.0.61f1 em `/home/ladybug/Unity/Hub/Editor/6000.0.61f1/Editor/Unity` |
| Build Linux | `LinuxStandaloneSupport` instalado |
| Projeto remoto | `/media/ladybug/Dataset1/AWSIM`, com os scripts JIDO |
| Cena JIDO | Diretório `Assets/Awsim/Scenes/Jido` ainda ausente |
| Mapa externo | `Assets/Awsim/Externals` contém apenas `.gitkeep` |
| Cache e build | Não encontrados nos caminhos `Library` e `Build*` do projeto |
| Kafka | Contêiner `jido-kafka` saudável, porta 9092 escutando |
| Docker | Usar `DOCKER_CONTEXT=default`; o contexto Desktop não conecta |
| Gráficos sem monitor | `xvfb-run`, `vulkaninfo` e `libvulkan1` instalados |
| Python JIDO existente | `.venv` da ponte CARLA contém `pytest` e `confluent_kafka`, mas não `jido_awsim_bridge` |
| Serviços CARLA/painel | `jido-carla`, `jido-londrina`, `jido-painel` inativos na consulta |
| Disco | Aproximadamente 13 TB disponíveis no Dataset1 |

O hardware permitiu seguir com a implantação. Os resultados da execução estão
na atualização abaixo; a tabela registra a situação encontrada na inspeção inicial.

## Implantação de 08/10/2026

**Estado atual:** AWSIM com VLP16, ponte e agrupamento ativos. O ensaio com o
Core existente completou a rota curta; resultados na seção de LiDAR abaixo.

- Ponte instalada em ambiente próprio no servidor; quatro testes passaram
  também no Linux e a conexão com Kafka real foi confirmada.
- Projeto importado no Unity e build Linux HDRP gerado com sucesso, usando
  a cena criada por `JidoTestSceneCreator` e o novo `JidoBatchBuild`.
- Teste físico inicial: 4,70 m de deslocamento, 2,37 m/s após acelerar,
  variação de yaw à esquerda de 0,505 rad e velocidade final zero após
  interromper os comandos. IMU e localização foram recebidas por UDP.
- Recebimento de mensagens novas de IMU e localização verdadeira confirmado
  nos respectivos tópicos Kafka, com o simulador e a ponte rodando no servidor.
- Corrigida a remoção de publishers sem inicialização ROS, que causava
  `NullReferenceException` ao encerrar a pista JIDO.
- Build e teste físico repetidos após a correção; nenhum erro de referência
  nula ou carregamento de biblioteca foi encontrado no log do player.
- Serviços de usuário `jido-awsim` e `jido-awsim-bridge` ativos ao finalizar.
  Telemetria contínua, sem pacotes rejeitados na observação final. Não foi
  configurado início automático no boot.
- Criados scripts de build, execução, teste e controle dos serviços em
  `tools/jido/`. Ver `tools/jido/README.md` para operação.

O primeiro marco abaixo foi a pista local sem LiDAR e sem corrida autônoma do Core.
ENU/eixo traseiro, mapa e dinâmica do shuttle continuam pendentes para
rotas e comparação com CARLA. Não foi necessário atualizar o driver para
compilar e executar essa pista.

### Ensaio inicial com o Core real, antes do LiDAR

Executado `autonomous_core_node` do servidor em tópicos `sil.*`, mantendo
`JIDO_REQUIRE_SENSORS_READY=1`. O Core recebeu localização/IMU do AWSIM,
publicou status, entrou em MANUAL e recebeu comandos e uma rota curta.
O veículo permaneceu parado: acelerador publicado pelo Core foi zero.
O log confirmou `ATUACAO BLOQUEADA` e `Rota PENDENTE`, com
`lidar_obstacles` ainda mudo. O gate de sensores também protege a atuação
manual. Resultado: transporte integrado, movimento pelo Core ainda bloqueado.

O processo Core do ensaio foi encerrado ao terminar. Logs remotos:
`Logs/jido-core.log` e `Logs/jido-core-smoke.json`. A ferramenta usada está
em `tools/jido/core_smoke_test.py`; ela retorna falha enquanto o movimento
não acontecer. Naquele momento, faltava o VLP16 na cena e o `cluster_node` produzindo
`sil.jidocab.lidar.obstacles`, antes de repetir a rota. Não foram publicados
sensores fictícios nem desligada a exigência de prontidão.

### LiDAR habilitado e ensaio aprovado

O VLP16 foi adicionado à cena e o build Linux foi gerado novamente. Na cena
JIDO, o RGL usa o relógio Unity e exclui somente a hierarquia do próprio Lexus
da geometria de raios. O veículo inicia na altura de repouso medida, evitando
que a suspensão congelada pelo freio inicial altere a altura ao começar a andar.
Foi solicitado buffer maior por socket na ponte AWSIM: as perdas UDP observadas
caíram para zero na verificação após o ajuste. Os quatro testes da ponte passaram.

O `cluster_node` existente roda como serviço `jido-awsim-cluster`, com prefixo
`sil.` e altura de sensor de 1,608 m. Chegaram nuvens novas com cerca de 12,6 mil
pontos. Um cubo físico de referência em ENU (10, 8) foi detectado na posição
esperada; o piso deixou de produzir os falsos obstáculos que bloqueavam a rota.

O Core existente foi executado com `JIDO_REQUIRE_SENSORS_READY=1`, sem EKF
concorrente. Resultado final do ensaio:

- Comando manual através do Core: 1,31 m percorridos; parada a 0 m/s após
  interromper os comandos.
- Rota autônoma de 5 m: `NAVIGATING` → `ARRIVED`; 4,71 m percorridos e erro
  final de 0,29 m, dentro do raio de chegada de 0,5 m.
- Segurança `OK`, velocidade zero ao chegar e zero estouros de deadline.
- Revisão Git, árvore de código e hashes de `autonomous_core_node` e
  `cluster_node` iguais aos registrados antes da habilitação. Nenhum código
  do Core foi editado e nenhum executável dele foi recompilado.

O Core do ensaio foi encerrado; AWSIM, ponte e agrupamento permanecem ativos.
Logs remotos: `Logs/jido-core-smoke.json`, `Logs/jido-lidar-check.json` e
`Logs/jido-core.log`. Essa validação é de uma rota curta na pista plana,
com localização verdadeira; Londrina, shuttle, EKF e robustez continuam fora
do escopo validado.

## Primeiro marco: pista JIDO sem ROS

1. Disponibilizar `jido_AWSIM_bridge` no servidor, junto à ponte CARLA usada como
   dependência. Criar ambiente Python próprio para AWSIM e instalar ambos os
   pacotes locais com os extras Kafka e desenvolvimento. Não é necessário
   iniciar o simulador CARLA para usar essa dependência Python.
2. Abrir o projeto remoto no Unity e concluir ativação, importação dos pacotes e
   correção de eventuais erros de compilação. O projeto usa HDRP por padrão.
3. Executar **JIDO → Create UDP Test Track → HDRP**. Conferir referências do
   veículo, Rigidbody e IMU. A pista plana é criada pelo editor e dispensa o mapa
   Shinjuku. Não usar o controlador `AutowareSimulationDemo` nessa cena.
4. Definir a referência geográfica ENU e a distância da origem do veículo ao
   eixo traseiro; os campos começam em zero e precisam ser medidos para rotas.
5. Iniciar a ponte com `jido-awsim-bridge --kafka 127.0.0.1:9092` e dar Play.
   Telemetria usa UDP 25010, comandos 25011 e LiDAR 25012. Manter simulador,
   ponte e Core no servidor; o Mac serve para acesso e visualização.
6. Verificar avanço em linha reta, sentido do esterço e parada quando os
   comandos deixam de chegar. O timeout Unity é de 500 ms. No primeiro ensaio,
   usar localização verdadeira do simulador e deixar `ekf_fusion_node` desligado.

## LiDAR, build e operação

- Depois de validar controle, adicionar **JIDO → Add VLP16 to Current Test Track
  → HDRP** e verificar frequência, altura, eixos e pontos recebidos pelo Core.
- A documentação desta cópia recomenda driver NVIDIA 570 ou superior e Ubuntu
  22.04. O servidor usa 565.77 e Ubuntu 24.04: validar carregamento das bibliotecas
  nativas e Vulkan antes de classificar isso como falha. Atualizar driver apenas
  se necessário, considerando que a máquina é compartilhada.
- Para distribuir um executável, incluir a cena JIDO no perfil/lista de build:
  a configuração atual aponta para a cena Autoware. Gerar build Linux x86_64.
- Para execução sem monitor, a documentação do projeto usa `xvfb-run`; preservar
  suporte gráfico para sensores. A execução ainda precisa ser verificada no
  servidor antes de criar serviço persistente.
- Garantir que outra ponte ou corrida não publique atuação/localização nos
  mesmos tópicos `sil.jidocab.*`. O Kafka existente pode ser reutilizado.
- Para testar EKF depois, usar `--ekf-mode` e `JIDO_TOPIC_PREFIX=sil.` no Core.
  GPS, odometria e rumo são virtuais, derivados da verdade do simulador.

## Demo original Autoware e Londrina

A demo original exige importar `Shinjuku.unitypackage`, instalar/configurar
Autoware e ROS 2 e ajustar DDS. O Unity/AWSIM usa ROS embarcado: não iniciar
esse processo com o ambiente ROS externo carregado. Essa etapa é independente
da pista JIDO inicial.

Não há cena Londrina no checkout inspecionado. A integração atual usa Lexus e
pista plana; cenário, geometria e dinâmica do shuttle, calibração dos sensores
e sincronização temporal ainda precisam ser implementados/medidos antes de
comparações completas com o laboratório CARLA.

## Validação realizada

Os quatro testes existentes em
`../JIDO/jido_AWSIM_bridge/tests/test_udp_adapter.py` passaram no Mac, executados
diretamente com o código Python local e barramento em memória: ida/volta UDP,
roteamento de comandos e IMU, remontagem LiDAR e sensores virtuais no modo EKF.
Isso não valida Kafka real, compilação C#, física Unity ou operação conjunta.

Referências: `docs/JIDO_INTEGRATION.md`,
`docs/DeveloperGuide/SetupUnityProject/index.md`,
`docs/DeveloperGuide/Experimental/RunningHeadless/index.md` e
`../JIDO/jido_carla_bridge/docs/server.md`.
