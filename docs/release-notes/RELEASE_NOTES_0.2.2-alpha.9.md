# ResMode 0.2.2-alpha.9

A resolução de jogo agora resiste ao Alt+Tab. Antes o ResMode aplicava a resolução como um modo temporário do Windows, e qualquer programa que chamasse `ChangeDisplaySettings(NULL)`, como um jogo que perde o foco, desfazia o modo e mandava a tela de volta para 1080p no meio da partida. Agora o modo é gravado no Windows enquanto o jogo está aberto, e a mesma chamada não o desfaz.

Ao fechar o jogo, o ResMode grava a resolução normal de volta, como antes. Se o PC for desligado ou o app travar com o jogo aberto, o ResMode devolve a resolução normal na próxima vez que abrir.

MSI para Windows x64, sem assinatura digital. Feche o ResMode antes de atualizar, quando nenhum jogo estiver aberto. As configurações são preservadas.
