# ResMode 0.2.2-alpha.7

Alguns jogos devolvem a tela para a resolução do Windows quando perdem o foco, por exemplo ao dar Alt+Tab ou clicar no outro monitor. Como a resolução de jogo é temporária, o Windows e o jogo voltavam para 1080p e ficavam assim até o fim da partida.

Agora, com o jogo ainda aberto, se a tela voltar para a resolução do desktop, o ResMode liga a resolução de jogo de novo depois de 2 segundos e devolve as janelas do outro monitor (como o Parsec) ao lugar. Ele não briga com o jogo: só age quando a tela volta exatamente ao modo do desktop, e para de tentar por alguns minutos se o jogo desfizer a resolução 5 vezes seguidas.

MSI para Windows x64, sem assinatura digital. Feche o ResMode antes de atualizar, quando nenhum jogo estiver aberto. As configurações são preservadas.
