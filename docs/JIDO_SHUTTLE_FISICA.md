# Física do shuttle JIDO no AWSIM

O shuttle tem um prefab próprio, `JidoShuttleHDRP.prefab`, criado em
`Assets/Awsim/Models/Entity/Vehicle/EgoVehicle/JidoShuttle` no servidor.
O conjunto físico é novo: Rigidbody, colisão da carroceria, quatro
WheelColliders e controlador de aceleração AWSIM. Nenhum componente físico
ou visual do Lexus permanece dentro desse prefab. O código do CORE não muda.

## Geometria medida

Fonte: `SK_Jido_F.fbx` do projeto CARLA Londrina, auditado no Blender.
Os centros abaixo estão no sistema do modelo: x frente, y esquerda, z cima,
com origem no centro da carroceria ao nível do piso.

| Roda | Centro x/y/z (m) | Raio (m) |
|---|---|---|
| Dianteira esquerda | 1,42203 / 1,00978 / 0,38337 | 0,38264 |
| Dianteira direita | 1,42171 / −1,00983 / 0,38264 | 0,38264 |
| Traseira esquerda | −1,42360 / 1,00983 / 0,38266 | 0,38043 |
| Traseira direita | −1,42360 / −1,00983 / 0,38258 | 0,38043 |

O modelo mede aproximadamente 3,849 × 2,261 × 2,135 m. O entre-eixos é
aproximadamente 2,846 m e a bitola, 2,020 m. O importador alinha os ossos das
rodas dianteiras com +Z do Unity e mantém as rodas esquerdas em −X.
As rodas visuais usam pivôs separados que seguem os WheelColliders;
a rotação do pneu e o esterço deformam os ossos correspondentes da malha.

## Parâmetros iniciais de simulação

| Parâmetro | Valor | Origem/limite |
|---|---|---|
| Massa | 1.500 kg | Provisória, mesma referência do CARLA; não é medida do veículo real |
| Centro de massa | 0,85 m acima da origem | Estimativa inicial |
| Curso da suspensão | 0,22 m | Estimativa inicial |
| Mola por roda | 45.000 N/m | Ajuste inicial de simulação |
| Amortecedor por roda | 6.200 Ns/m | Ajuste inicial de simulação |
| Massa de cada roda | 30 kg | Estimativa inicial |
| Aceleração máxima | 1,5 m/s² | Limite inicial de simulação |
| Desaceleração máxima | 3,5 m/s² | Limite inicial de simulação |
| Esterço máximo | 35° | Referência do perfil JIDO existente |
| Atraso de aceleração/esterço | 0,15 / 0,20 s | Ajuste inicial de simulação |

A carroceria usa uma caixa de colisão conservadora, com início 0,15 m acima
do piso de projeto. É uma aproximação do volume externo, não uma colisão
recortada de cada painel. A configuração utiliza o controlador
`AccelVehicle` do AWSIM: força de movimento comandada por aceleração,
suspensão/contato pelo WheelCollider e cancelamento lateral de escorregamento.
Não representa um motor/transmissão nem um modelo detalhado de pneus.

O transporte aguarda dois segundos de assentamento antes de ativar a lógica
que congela o veículo freado. Isso evita travar a suspensão estendida no
primeiro quadro. O filtro `FirstOrderLaggedFloat` do AWSIM passou a usar a
solução exponencial exata, evitando extrapolar o comando após longos
intervalos de repouso em que o valor não era lido. A altura de montagem foi corrigida pelo assentamento medido
na pista; os valores precisam ser revisitados ao atualizar massa/suspensão.

## Compilação e ensaio

```sh
cd /media/ladybug/Dataset1/AWSIM
JIDO_SCENE=shuttle-physics JIDO_BUILD_DIR=Builds/JidoShuttlePhysics bash tools/jido/build.sh
python3 tools/jido/shuttle_physics_check.py
```

O ensaio usa UDP diretamente e ocupa as portas 25010/25011/25013. Ponte,
player dos serviços e CORE devem estar parados. O próprio teste inicia e
encerra seu player; não publica comandos no Kafka.

São verificados assentamento, contato das quatro rodas, aceleração,
sentido do esterço, ré, parada por falta de comandos, acompanhamento visual
das rodas e contato da carroceria com uma barreira. Uma roda pode descarregar
durante impacto; o contato contínuo das quatro é verificado antes da barreira.
Relatórios ficam em `Logs/jido-shuttle-physics-profile.txt`,
`Logs/jido-shuttle-physics-check.json` e `Logs/jido-shuttle-physics-check-player.log`.

Para gerar a cena urbana com esse mesmo prefab:

```sh
JIDO_SCENE=shuttle-shinjuku JIDO_BUILD_DIR=Builds/JidoShuttle bash tools/jido/build.sh
```

Ela fica em `Assets/Awsim/Scenes/Jido/JidoShinjukuShuttleHDRP.unity`.
Esses builds são separados do player anterior em `Builds/Jido`.
A calibração de LiDAR/IMU e da referência traseira foi concluída em
`docs/JIDO_SHUTTLE_SENSORES.md`. Alinhamento geográfico e rotas autônomas
permanecem nas próximas etapas. O LiDAR urbano tem base a 2,30 m da origem da carroceria e centro óptico
a 2,3377 m; não usar a antiga altura de
1,608 m do Lexus para validar obstáculos do shuttle.

## Resultado na pista — 09/10/2026

O ensaio passou com os quatro pneus em contato no trecho normal, sem deriva
de altura em repouso. A altura da origem ficou a −0,00003 m do piso de projeto.
Comando de aceleração de 0,300 m/s²: máximo medido de 0,29946 m/s²; avanço
de 1,286 m em três segundos e velocidade final de 0,859 m/s. Esterço positivo
produziu 0,2119 rad de giro à esquerda. Na ré, velocidade de −0,859 m/s.
A parada por falta de comandos e a parada final atingiram 0 m/s.

A colisão da carroceria impediu atravessar a barreira. A inclinação máxima
foi 1,03°; erro máximo de acompanhamento das rodas visuais foi inferior a
0,000001 m. Esses resultados validam funcionamento/estabilidade da simulação
inicial, sem substituir medições do veículo real.

O teste urbano pode ser executado com:

```sh
python3 tools/jido/shuttle_physics_check.py --city
```

Ele usa `Builds/JidoShuttle/Jido.x86_64`, verifica as rodas, aceleração, esterço,
ré e parada na rua, e grava `Logs/jido-shuttle-city-check.json`. A colisão
controlada com barreira permanece exclusiva da pista dedicada.

## Resultado em Shinjuku — 09/10/2026

O teste urbano também passou: quatro rodas em contato, sem deriva de altura
em repouso; avanço de 0,873 m e giro à esquerda de 0,09298 rad. A ré atingiu
−1,170 m/s; parada por falta de comandos e parada final a 0 m/s. A inclinação
máxima da carroceria foi 1,644°, e as rodas visuais seguiram seus contatos
físicos. A diferença de aceleração em relação à pista plana inclui o efeito
do piso/inclinação, pois o modelo não compensa automaticamente a rampa.

O player urbano está em `Builds/JidoShuttle/Jido.x86_64`. O player anterior
em `Builds/Jido` foi preservado. Ao terminar os ensaios, os players de teste
foram encerrados e os serviços de simulador, ponte, agrupamento e painel
continuaram desligados. Os hashes dos binários do CORE permaneceram iguais.
