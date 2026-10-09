# Calibração dos sensores do shuttle JIDO no AWSIM

A calibração é da configuração de simulação. As posições vieram do modelo
3D e da montagem definida no Unity; não são medidas de hardware do shuttle real.
Nenhum arquivo de código ou binário do CORE foi alterado.

## Frames e montagem

Convenção JIDO: x para a frente, y para a esquerda e z para cima.
A localização publicada passou a representar o centro do eixo traseiro ao
nível do piso de projeto (`rear_axle_ground`). O deslocamento medido entre a
origem da carroceria e esse ponto é 1,4236033 m para trás. A velocidade é
avaliada no mesmo ponto, incluindo o efeito da rotação da carroceria.
A orientação publica yaw, pitch e roll em radianos, na ordem ZYX.

| Elemento | Frente do eixo traseiro | Altura nominal | Orientação relativa ao corpo |
|---|---|---|---|
| Base do VLP16 | 1,4236033 m | 2,300 m | Sem rotação adicional |
| Centro óptico do VLP16 | 1,4236033 m | 2,3377 m | Sem rotação adicional |
| IMU | 1,4236033 m | 1,000 m | Alinhada ao corpo |

O VLP16 acrescenta 37,7 mm entre a base do prefab e seu centro de medição.
Os pontos do LiDAR permanecem relativos ao centro óptico, em x frente / y
esquerda / z cima. Não foram transladados para o eixo traseiro: o agrupador
existente trabalha no frame do sensor. A carroceria própria está excluída do
traçado de raios para evitar autodetecção. Essa opção também omite a
ocultação de raios pela própria carroceria; a cobertura física de uma
montagem real exige um teste específico com essa ocultação habilitada.

A IMU envia força específica: parada em piso nivelado, z positivo próximo
de +9,81 m/s². O sensor AWSIM adicionava gravidade descendente à aceleração;
o adaptador JIDO corrige essa convenção ao publicar. O sinal do giroscópio
respeita a troca de orientação entre Unity e JIDO: giro à esquerda dá wz
positivo. A aceleração corresponde à localização física da IMU; não foi
aplicada compensação de braço de alavanca para o eixo traseiro. Validação
com fusão EKF completa continua separada do teste de verdade do simulador.

## Perfil de execução

`tools/jido/profiles/shuttle.env` é gerado pelo criador do prefab a partir da
geometria importada. Contém a altura óptica de 2,3377 m, a largura medida de
2,26090169 m e o entre-eixos de 2,84547448 m.

O agrupador recebe a janela de obstáculos de 0,10 a 2,00 m acima do piso:
z do sensor entre −2,2377 e −0,3377 m. O piso fica abaixo dessa janela.
Para condução futura, `JIDO_PP_EIXO_TRASEIRO_M=0`, pois a localização já foi
transladada ao eixo traseiro. Aplicar novamente 1,4236 m no controlador daria
um deslocamento duplo. O perfil deve ser carregado também pelo processo de
condução quando forem feitos os testes de rota; este passo não iniciou esse nó.

```sh
cd /media/ladybug/Dataset1/AWSIM
JIDO_PROFILE=shuttle bash tools/jido/server.sh start
bash tools/jido/server.sh stop
```

`start` com esse perfil escolhe `Builds/JidoShuttle/Jido.x86_64` e usa a altura
correta no `cluster_node` existente. Sem `JIDO_PROFILE=shuttle`, a configuração
anterior do Lexus continua disponível.

## Verificação com alvos conhecidos

A cena `JidoShuttleSensorsHDRP.unity` tem piso plano e três cubos medidos:
frente (12, 0), esquerda (8, 6) e direita (8, −6), relativos à origem inicial
da carroceria, em metros. Como o LiDAR enxerga superfícies, o centro dos
pontos de um cubo pode ficar até aproximadamente 0,6 m do centro geométrico.
A estimativa do plano do chão exclui a região ocupada pelo alvo frontal.

```sh
JIDO_SCENE=shuttle-sensors JIDO_BUILD_DIR=Builds/JidoShuttleSensors bash tools/jido/build.sh
JIDO_PROFILE=shuttle JIDO_PLAYER="$PWD/Builds/JidoShuttleSensors/Jido.x86_64" bash tools/jido/server.sh start
/media/ladybug/Dataset1/jido-testes/jido_AWSIM_bridge/.venv/bin/python tools/jido/shuttle_sensors_check.py
bash tools/jido/server.sh stop
```

Resultado em 09/10/2026: passou. Altura inferida pelo chão de 2,33618 m,
com erro RMS do plano de 4,63 mm, versus 2,3377 m configurados. Foram
detectados exatamente os três alvos; o primeiro retorno do corredor ficou
a 11,36 m, correspondente à face do cubo frontal. A referência publicada
coincidiu com o ponto físico dos contatos traseiros com erro de 0,964 mm.

A IMU em repouso publicou az=9,8099985 m/s², com giroscópios a zero. Durante
a curva, o giro integrado do giroscópio foi 0,30135 rad e a orientação do
veículo mudou 0,30483 rad, confirmando unidade e sinal. O veículo parou a
0 m/s depois de cessarem os comandos.

Relatórios: `Logs/jido-shuttle-sensor-geometry.json`,
`Logs/jido-shuttle-sensors-check.json` e, para Shinjuku,
`Logs/jido-shuttle-city-sensors-check.json`.

## Resultado urbano — 09/10/2026

A verificação em Shinjuku passou com 177 nuvens e 168 mensagens de obstáculos.
A nuvem final teve 27.275 pontos. A referência ficou a 0,978 mm dos contatos
traseiros. A altura óptica vertical medida foi 2,33747 m e a altura da IMU,
0,99990 m; pequenas diferenças vêm da inclinação da carroceria.

A IMU em repouso indicou (0,11684; −0,07536; 9,80902) m/s², coerente com
pitch/roll da rua, e giroscópios a zero. O giro integrado foi 0,088589 rad,
versus 0,088443 rad da orientação publicada. O plano ajustado no trecho
frontal teve RMS de 9,27 mm; sua altura extrapolada de 2,289 m reflete o
piso urbano que varia ao longo do trecho, sem redefinir a montagem física.

O corredor frontal verificado estava livre (`fwd_cru_pts=0`), com obstáculos
laterais presentes. A parada após cessarem os comandos atingiu 0 m/s.
Isso valida os sinais e a filtragem nesse trecho e na cena plana com alvos;
não é uma validação de toda a cidade, de fusão EKF ou de condução autônoma.
Os serviços foram desligados ao terminar.
