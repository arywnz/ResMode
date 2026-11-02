# ResMode 0.2.1-alpha.1

O efeito ao passar o mouse agora colore o fundo do card, incluindo a área do nome do jogo. A cor vem da própria capa e fica mais visível, com um contorno definido. O halo desfocado e o filtro de claridade/escurecimento sobre a imagem foram retirados: a capa permanece nítida durante o hover.

A animação suave, a resposta aos movimentos rápidos e a limpeza do destaque ao sair do card foram preservadas. Os testes verificam a cor e a presença do fundo, a preservação dos pixels da capa e a ausência de desfoque fora do card.

Corrigida a confirmação prematura do reinício do Explorer: a área de trabalho e a barra precisam responder e permanecer estáveis antes de concluir a ação. A correção roda em processo separado para manter o app responsivo e arma uma recuperação independente antes de encerrar o Explorer. Uma eventual nova tentativa é limitada e não acontece se houver trabalho visível do Explorer em andamento. A função fica desligada por padrão em instalações novas.

Instalação MSI por usuário, sem assinatura digital. O Windows ainda pode exibir avisos ou bloquear software unsigned conforme a política do PC. Feche o ResMode antes de atualizar, quando nenhum jogo estiver aberto. Os dados continuam protegidos pelo Windows e são preservados ao atualizar ou desinstalar pelo MSI.
