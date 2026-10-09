# JIDO no servidor do apartamento

O projeto remoto fica em `/media/ladybug/Dataset1/AWSIM` e a ponte em
`/media/ladybug/Dataset1/jido-testes/jido_AWSIM_bridge`.

## Perfil atual: shuttle em Shinjuku

Em 09/10/2026, o shuttle com física própria, IMU, VLP16 e mapa alinhado completou a rota curta com o CORE existente. Curvas para os dois lados e parada preventiva diante de uma barreira também passaram na pista controlada. Resultados, parâmetros provisórios e limites da validação:

- [Física do shuttle](../../docs/JIDO_SHUTTLE_FISICA.md)
- [Sensores](../../docs/JIDO_SHUTTLE_SENSORES.md)
- [Rota de Shinjuku](../../docs/JIDO_SHINJUKU_ROTA.md)
- [Curvas e obstáculo](../../docs/JIDO_SHUTTLE_MANOBRAS.md)

Para iniciar o player operacional correto, usar `JIDO_PROFILE=shuttle bash tools/jido/server.sh start`. Para encerrá-lo, `JIDO_PROFILE=shuttle bash tools/jido/server.sh stop`. O perfil padrão sem essa opção continua sendo o Lexus descrito nas seções históricas abaixo. O teste de curvas inicia e encerra os próprios serviços; o teste da reta pressupõe o player de Shinjuku ativo e encerra somente o CORE que iniciou.

```sh
JIDO_SCENE=shuttle-shinjuku JIDO_BUILD_DIR=Builds/JidoShuttle bash tools/jido/build.sh
# Pista controlada, em build separado:
JIDO_SCENE=shuttle-validation JIDO_BUILD_DIR=Builds/JidoShuttleValidation bash tools/jido/build.sh
/media/ladybug/Dataset1/jido-testes/jido_AWSIM_bridge/.venv/bin/python tools/jido/shuttle_maneuvers_check.py
```

Antes de regenerar as cenas, importar o mapa oficial em `Assets/Awsim/Externals/Shinjuku` e o modelo próprio `SK_Jido_F.fbx` em `Assets/Awsim/Models/Entity/Vehicle/EgoVehicle/JidoShuttle/`. Esses arquivos estão instalados no servidor e não são distribuídos neste commit. O gerador cria o prefab físico e o perfil a partir desse modelo. Builds, cache Unity e logs também ficam fora do Git. As rotas dependem da cena e da pose assentada; repetir o levantamento após mudar o spawn.

## Operação pelo Mac

```sh
ssh jido-carla 'cd /media/ladybug/Dataset1/AWSIM && bash tools/jido/server.sh status'
ssh jido-carla 'cd /media/ladybug/Dataset1/AWSIM && bash tools/jido/server.sh logs'
ssh jido-carla 'cd /media/ladybug/Dataset1/AWSIM && bash tools/jido/server.sh stop'
ssh jido-carla 'cd /media/ladybug/Dataset1/AWSIM && bash tools/jido/server.sh start'
```

`start` instala e inicia os serviços de usuário `jido-awsim`,
`jido-awsim-bridge` e `jido-awsim-cluster`. Eles continuam rodando após sair do
SSH; não são habilitados automaticamente no boot. O agrupamento usa o
`cluster_node` existente, sem recompilar ou alterar o código do Core.
O nó de condução autônoma só é iniciado durante o ensaio abaixo.

O simulador usa Vulkan e Xvfb, preservando acesso à GPU. A cena inicial é a pista plana
HDRP de 100 × 100 m, com Lexus, IMU, VLP16 e controle UDP. O LiDAR usa o relógio
Unity e exclui a geometria do próprio veículo. Há um objeto de referência de
1 × 2 × 1 m em ENU (10, 8), fora da rota reta do ensaio.
A referência ENU e o deslocamento do eixo traseiro ainda estão em zero:
o ambiente serve para testar transporte e controle local, não rotas geográficas.

Localização e IMU são publicados nos tópicos `sil.jidocab.fusion.localization`
e `sil.jidocab.sensor.imu`. O primeiro contém verdade do simulador: não iniciar
outro produtor de localização no mesmo tópico. Sem atuação recente, o veículo
fica freado. A ponte e o simulador usam UDP 25010/25011; a nuvem fragmentada usa
25012. A ponte solicita um buffer UDP maior por socket para evitar perda de
fragmentos. O agrupamento recebe `sil.jidocab.lidar.pointcloud.full` e publica
`sil.jidocab.lidar.obstacles`. A altura configurada é 1,608 m, medida para o
veículo assentado na pista plana; outras montagens/pisos exigem calibração.

## Build e teste

```sh
cd /media/ladybug/Dataset1/AWSIM
bash tools/jido/server.sh stop
bash tools/jido/build.sh
python3 tools/jido/smoke_test.py
bash tools/jido/server.sh start
```

O teste ocupa a porta de telemetria e inicia seu próprio player; parar os
serviços antes de executá-lo. Ele verifica IMU, deslocamento, sentido do
esterço e parada por falta de comandos, sem publicar atuação no Kafka.

## Ensaio com o Core existente

Com os três serviços ativos e sem outro Core publicando nos mesmos tópicos:

```sh
cd /media/ladybug/Dataset1/AWSIM
PY=/media/ladybug/Dataset1/jido-testes/jido_AWSIM_bridge/.venv/bin/python
"$PY" tools/jido/lidar_check.py
"$PY" tools/jido/core_smoke_test.py
```

O ensaio usa `autonomous_core_node` já compilado, em tópicos `sil.*`, com a
exigência de sensores ativa. Testa atuação manual curta e seu deadman, depois
uma rota reta de 5 m. Limita a configuração a 1 m/s e encerra o Core ao terminar.
O entre-eixos usado é 2,7878773 m, derivado dos WheelColliders do prefab. Nenhum
arquivo de código do Core é editado. Logs e resultado ficam em
`Logs/jido-core.log` e `Logs/jido-core-smoke.json`.

Resultado em 08/10/2026: deslocamento manual de 1,31 m, parada a 0 m/s, rota
autônoma concluída com estado `ARRIVED` e erro final de 0,29 m.

Logs: `Logs/jido-build.log`, `Logs/jido-player.log`, `Logs/jido-smoke.json` e
`journalctl --user -u jido-awsim-bridge`. O build fica em
`Builds/Jido/Jido.x86_64`.

Dependências de execução: Unity 6000.0.61f1 com suporte Linux para compilar,
driver NVIDIA/Vulkan, Xvfb, Kafka e a ponte Python instalada em ambiente próprio.

## Cena Nishi-Shinjuku

O pacote oficial foi importado no servidor em `Assets/Awsim/Externals/Shinjuku`.
A cena dedicada fica em `Assets/Awsim/Scenes/Jido/JidoShinjukuHDRP.unity`,
com o ambiente HDRP, Lexus RX450h 2015, IMU, VLP16 e os mesmos canais UDP/Kafka.
O controlador Autoware/ROS do demo não participa dessa cena.
Nesta etapa, o ambiente é estático: tráfego dinâmico e rotas autônomas ainda
precisam de configuração e validação. A referência geográfica é derivada do
MGRS original do mapa; alinhamento entre a grade e ENU deve ser validado antes
de usar rotas geográficas.

Para compilar Shinjuku no servidor:

```sh
cd /media/ladybug/Dataset1/AWSIM
bash tools/jido/server.sh stop
JIDO_SCENE=shinjuku bash tools/jido/build.sh
```

Sem `JIDO_SCENE=shinjuku`, o build continua gerando a pista plana. Cada build
atualiza o player em `Builds/Jido/Jido.x86_64`; `server.sh start` executa o
último cenário compilado. O relatório da cena fica em
`Logs/jido-shinjuku-scene.txt`. O ensaio específico do cenário urbano é
`tools/jido/shinjuku_check.py`, com resultado em `Logs/jido-shinjuku-check.json`.
Os ensaios `lidar_check.py` e `core_smoke_test.py` são próprios da pista plana
e não devem ser usados em Shinjuku sem adaptar seus objetos/rotas.

Os modelos Shinjuku são distribuídos sob CC BY-NC 4.0:
https://autowarefoundation.github.io/AWSIM/License/

Validação em 09/10/2026: 106 nuvens completas, 105 mensagens de obstáculos,
334 mensagens IMU e 185 de localização no ensaio; última nuvem com 27.078
pontos. O veículo avançou 0,67 m e parou a 0 m/s após cessarem os comandos.
Os serviços foram desligados ao terminar. Nenhum binário/código do Core mudou.

O publicador UDP envia fragmentos em uma thread com intervalos curtos para
evitar exceder a fila padrão do Linux, mantendo apenas a última varredura
pendente. A ponte AWSIM usa um produtor dedicado com compressão Zstd e limite
de 4 MiB para a nuvem completa. O tópico `sil.jidocab.lidar.pointcloud.full`
foi configurado com `max.message.bytes=4194304`; para reproduzir a configuração
sem alterar outros parâmetros do tópico:

```sh
/media/ladybug/Dataset1/jido-testes/jido_AWSIM_bridge/.venv/bin/python tools/jido/configure_lidar_topic.py
```

A montagem do sensor foi mantida; a altura de 1,608 m do agrupamento vem da
pista plana. Calibração de altura/inclinação no piso urbano e alinhamento
geográfico permanecem etapas necessárias para validar condução autônoma.

## Prévia visual do shuttle JIDO

O modelo `SK_Jido_F.fbx` foi copiado do projeto Londrina para
`Assets/Awsim/Models/Entity/Vehicle/EgoVehicle/JidoShuttle` no servidor.
A cena `JidoShinjukuShuttlePreviewHDRP.unity` mostra o shuttle com materiais
HDRP e LiDAR no teto, nas dimensões aproximadas 3,849 × 2,261 × 2,137 m.
A imagem real renderizada está em `Logs/jido-shuttle-shinjuku.png` no servidor
e em `docs/previews/jido-shuttle-shinjuku.png` nesta cópia do projeto.

Essa cena é uma prévia de aparência: a carroceria do Lexus fica oculta e a
física anterior permanece como base provisória. Rodas, colisão, massa,
entre-eixos e montagem do sensor exigem calibração para simular o shuttle.
O player operacional já compilado não foi substituído por essa prévia.

Para reconstruir a prévia no servidor (com renderização GPU, sem `-nographics`):

```sh
cd /media/ladybug/Dataset1/AWSIM
xvfb-run -a /home/ladybug/Unity/Hub/Editor/6000.0.61f1/Editor/Unity \
  -batchmode -quit -force-vulkan -projectPath "$PWD" \
  -executeMethod Awsim.Jido.Editor.JidoShuttlePreview.CreateAndRender \
  -logFile "$PWD/Logs/jido-shuttle-preview.log"
```

## Física própria do shuttle

O prefab físico JIDO foi criado a partir do `SK_Jido_F.fbx`, com rodas e
colisão próprias. Veja `docs/JIDO_SHUTTLE_FISICA.md` para medidas, parâmetros
provisórios, builds separados e validação. O ensaio da pista passou em
09/10/2026: avanço de 1,286 m, ré a −0,859 m/s, quatro rodas em contato no
trecho normal, parada a zero e colisão da carroceria bloqueada pela barreira.
Massa, centro de massa e suspensão ainda são referências iniciais de simulação.
Sensores/rota/integração autônoma do shuttle permanecem nas próximas etapas.

## Sensores do shuttle calibrados

A montagem e os eixos estão descritos em `docs/JIDO_SHUTTLE_SENSORES.md`.
A referência de localização é o eixo traseiro; LiDAR com centro óptico a
2,3377 m e IMU a 1 m, com força específica positiva para cima em repouso.
O perfil `tools/jido/profiles/shuttle.env` inclui a altura, largura e
entre-eixos medidos e evita aplicar duas vezes o deslocamento traseiro.

Para executar a cena urbana atualizada com os serviços:

```sh
JIDO_PROFILE=shuttle bash tools/jido/server.sh start
/media/ladybug/Dataset1/jido-testes/jido_AWSIM_bridge/.venv/bin/python tools/jido/shuttle_sensors_check.py --city
bash tools/jido/server.sh stop
```

Os ensaios antigos do Core com rota na pista do Lexus não são adequados para
essa configuração: perfil de veículo e rota geográfica precisam ser usados
na etapa seguinte. A fusão EKF completa não é parte deste ensaio de sensores.
